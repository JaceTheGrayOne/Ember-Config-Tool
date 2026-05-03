using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ember_Config_Tool.Services;

public sealed class ConfigPreset
{
    public int SchemaVersion { get; set; } = 1;
    public string ToolVersion { get; set; } = "1.0.0";
    public string ConfigFingerprint { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Notes { get; set; } = "";
    public Dictionary<string, PresetSettingValue> Settings { get; set; } = new(StringComparer.Ordinal);
}

public sealed class PresetSettingValue
{
    public ConfigValueState State { get; set; }
    public JsonNode? Value { get; set; }
}

public sealed record PresetSummary(string DisplayName, string Path);

public static class PresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string PresetFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ember", "ConfigTool", "Presets");

    public static IReadOnlyList<PresetSummary> ListPresets()
    {
        Directory.CreateDirectory(PresetFolder);
        var presets = new List<PresetSummary>();
        foreach (var file in Directory.GetFiles(PresetFolder, "*.json").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var preset = Load(file);
                presets.Add(new PresetSummary(string.IsNullOrWhiteSpace(preset.DisplayName) ? Path.GetFileNameWithoutExtension(file) : preset.DisplayName, file));
            }
            catch (JsonException)
            {
            }
            catch (IOException)
            {
            }
        }

        return presets;
    }

    public static string Save(ConfigPreset preset)
    {
        Directory.CreateDirectory(PresetFolder);
        preset.UpdatedUtc = DateTimeOffset.UtcNow;
        if (preset.CreatedUtc == default)
        {
            preset.CreatedUtc = preset.UpdatedUtc;
        }

        var fileName = SanitizeFileName(string.IsNullOrWhiteSpace(preset.DisplayName) ? "Ember Preset" : preset.DisplayName) + ".json";
        var path = Path.Combine(PresetFolder, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(preset, JsonOptions));
        return path;
    }

    public static ConfigPreset Load(string path)
    {
        return JsonSerializer.Deserialize<ConfigPreset>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException($"Preset file is empty: {path}");
    }

    public static void Export(ConfigPreset preset, string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(preset, JsonOptions));
    }

    public static ConfigPreset Import(string path)
    {
        var preset = Load(path);
        Save(preset);
        return preset;
    }

    public static JsonNode? ToJsonValue(LuaValue? value)
    {
        return value switch
        {
            null => null,
            LuaNilValue => null,
            LuaBooleanValue boolean => JsonValue.Create(boolean.Value),
            LuaNumberValue number => JsonValue.Create(number.Value),
            LuaStringValue text => JsonValue.Create(text.Value),
            LuaTableValue table => TableToJson(table),
            _ => null
        };
    }

    public static LuaValue? FromJsonValue(JsonNode? value, string valueType)
    {
        if (value is null)
        {
            return LuaNilValue.Instance;
        }

        if (valueType.Equals("boolean", StringComparison.OrdinalIgnoreCase))
        {
            return new LuaBooleanValue(value.GetValue<bool>());
        }

        if (valueType.Equals("number", StringComparison.OrdinalIgnoreCase) || valueType.Equals("integer", StringComparison.OrdinalIgnoreCase))
        {
            var number = ReadDecimal(value);
            return new LuaNumberValue(number, number.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return new LuaStringValue(value.GetValue<string>());
    }

    private static decimal ReadDecimal(JsonNode value)
    {
        if (value is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<decimal>(out var decimalValue))
            {
                return decimalValue;
            }

            if (jsonValue.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            if (jsonValue.TryGetValue<long>(out var longValue))
            {
                return longValue;
            }

            if (jsonValue.TryGetValue<double>(out var doubleValue))
            {
                return Convert.ToDecimal(doubleValue, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return value.GetValue<decimal>();
    }

    private static JsonNode TableToJson(LuaTableValue table)
    {
        if (table.Entries.All(entry => entry.IdentifierKey is null && entry.Key is null))
        {
            var array = new JsonArray();
            foreach (var entry in table.Entries)
            {
                array.Add(ToJsonValue(entry.Value));
            }

            return array;
        }

        var obj = new JsonObject();
        var index = 0;
        foreach (var entry in table.Entries)
        {
            var key = entry.IdentifierKey ?? entry.Key?.ToCanonicalString() ?? (++index).ToString(System.Globalization.CultureInfo.InvariantCulture);
            obj[key] = ToJsonValue(entry.Value);
        }

        return obj;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var cleaned = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Ember Preset" : cleaned;
    }
}
