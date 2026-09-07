using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DelayedSecretVault.Models;

namespace DelayedSecretVault.Services;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new TimeSpanJsonConverter() }
    };

    private readonly string _settingsPath;

    public SettingsService(string? storageDirectory = null)
    {
        var dir = storageDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DelayedSecretVault");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
    }

    public string SettingsPath => _settingsPath;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return AppSettings.CreateDefaults();
            }

            var json = File.ReadAllText(_settingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (loaded is null)
            {
                return AppSettings.CreateDefaults();
            }

            loaded.EnforceInvariants();
            return loaded;
        }
        catch
        {
            return AppSettings.CreateDefaults();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.EnforceInvariants();
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var tempPath = _settingsPath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Copy(tempPath, _settingsPath, overwrite: true);
        File.Delete(tempPath);
    }

    private sealed class TimeSpanJsonConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var text = reader.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return TimeSpan.Zero;
            }

            if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            throw new JsonException($"Invalid TimeSpan value: {text}");
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture));
        }
    }
}
