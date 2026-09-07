# Delayed Secret Vault

A small Windows desktop app that stores named secrets locally and gates the **entire vault** behind a single intentional time delay.

Primary use: administrator passwords for tools like AdGuard, DNS filtering, or router admin—where you deliberately do not want immediate access.

## What this is

**Behavioral friction**, not adversarial security.

One vault, one lock, one countdown. Unlocking grants access until you lock again or restart. Restarting the app always returns to Locked and discards any in-progress wait.

## Build and run

Requirements: .NET 8 SDK on Windows.

```bash
dotnet build DelayedSecretVault.sln -c Debug
dotnet run --project src/DelayedSecretVault -c Debug
```

Debug builds use a **10 second** access-delay floor for local development.

```powershell
.\scripts\Run-Dev.ps1
```

```bash
dotnet build DelayedSecretVault.sln -c Release
dotnet test DelayedSecretVault.sln -c Release
dotnet run --project src/DelayedSecretVault -c Release
```

Release/prod builds use a **60 minute** access-delay floor.

```powershell
.\scripts\Run-Prod.ps1
```

### Deploy zip

Publish a Release package and pack required artifacts:

```powershell
.\scripts\Deploy.ps1
```

Creates `artifacts/publish/` and a timestamped zip under `artifacts/`. Self-contained `win-x64` by default. For framework-dependent:

```powershell
.\scripts\Deploy.ps1 -FrameworkDependent
```

## Storage

`%LOCALAPPDATA%\DelayedSecretVault\`

| File | Contents |
|---|---|
| `vault.json` | Secret names (plaintext) + DPAPI ciphertext in `value` (Base64) |
| `settings.json` | Non-sensitive durations only |

Example vault entry shape:

```json
{
  "version": 1,
  "entries": [
    {
      "id": "...",
      "name": "AdGuard Admin",
      "value": "<base64 DPAPI ciphertext>"
    }
  ]
}
```

`value` is Base64 of DPAPI ciphertext, never plaintext.

If `settings.json` is missing, built-in defaults are used (environment minimum wait, 30 s reveal, 30 s clipboard clear). The file is created when settings are saved while the vault is unlocked.

Access delay has a hard floor of **10 seconds in Debug** and **60 minutes in Release**. Settings may only extend it; values below the floor are rejected in the UI and clamped on load/save.

Access state (Locked / Waiting / Confirmation / Unlocked), countdowns, and session timestamps are **never** persisted.

## Defaults

| Setting | Default |
|---|---|
| Vault access delay | 10 seconds (Debug) / 60 minutes (Release); extendable only |
| Secret reveal duration | 30 seconds |
| Clipboard clear duration | 30 seconds |

Protected settings can be changed only while Unlocked.

## Flow

```text
Locked → Request access → Waiting (countdown) → Confirmation → Unlocked → Lock now → Locked
```

- No skip, unlock-now, master password, or emergency bypass
- Unlocked stays unlocked until you press **Lock now** or restart the app
- Killing the process loses countdown progress (by design)
- Reveal decrypts one secret at a time; auto-hides after the reveal duration without locking the vault
- Clipboard clear only removes content this app copied, and only if it is still unchanged

## Threat model

### Protects against

- Impulsive password retrieval
- Changing your mind during a short period of temptation
- Trivially disabling filtering software because the password is immediately available

### Does not protect against

- Malware, debuggers, or memory inspection
- A Windows administrator
- Someone modifying the application binary or rebuilding from source
- A determined technical user deliberately bypassing the mechanism
- Compromise of the Windows account used for DPAPI

This distinction is intentional. The app provides **behavioral delay, not adversarial isolation from the computer owner.**

There is no anti-debugging, anti-tampering, kernel driver, or background service.

### Known bypasses (honest)

- While Unlocked, you can extend the access delay, but not set it below the build floor (10 s Debug / 60 min Release) through normal settings
- Editing `settings.json` by hand to a lower delay is clamped back to the build floor on load
- Process memory may retain plaintext after reveal (.NET does not provide cryptographic erasure of managed strings)
- Rebuilding from source removes the friction
- DPAPI is bound to the current Windows user
- Restarting only resets the wait; it does not strengthen security

## Architecture

Auditable layout under `src/DelayedSecretVault/`:

- `Models/` — `SecretEntry`, `VaultData`, `VaultState`, `AppSettings`
- `Services/` — repository, DPAPI encryption, vault access state machine, settings, clipboard
- `ViewModels/` — thin UI coordination
- `VaultAccessService` is the only component that may transition toward Unlocked

## License / intent

Personal self-control tool. Keep the surface area small; do not turn it into a password manager.
