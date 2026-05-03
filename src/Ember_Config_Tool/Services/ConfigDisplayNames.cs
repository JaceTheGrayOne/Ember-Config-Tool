using System.Text.RegularExpressions;

namespace Ember_Config_Tool.Services;

public sealed class ConfigDisplayNames
{
    private readonly ConfigMetadata _metadata;
    private readonly IReadOnlyDictionary<string, string> _labelsByKey;

    public ConfigDisplayNames(ConfigMetadata metadata)
    {
        _metadata = metadata;
        _labelsByKey = BuildLabels(metadata);
    }

    public string LabelForKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "";
        }

        return _labelsByKey.TryGetValue(key, out var label) && !string.IsNullOrWhiteSpace(label)
            ? label
            : key;
    }

    public string DisplayPath(string? technicalPath)
    {
        if (string.IsNullOrWhiteSpace(technicalPath))
        {
            return "";
        }

        var path = technicalPath.Trim();
        if (_metadata.SettingsByKey.TryGetValue(path, out var setting))
        {
            return setting.Label;
        }

        if (_metadata.TablesByKey.TryGetValue(path, out var table))
        {
            return table.Label;
        }

        var bracketMatch = Regex.Match(path, @"^(?<table>[A-Za-z_][A-Za-z0-9_]*)\[(?<row>[^\]]*)\]\.(?<column>[A-Za-z_][A-Za-z0-9_]*)$");
        if (bracketMatch.Success &&
            _metadata.TablesByKey.TryGetValue(bracketMatch.Groups["table"].Value, out var bracketTable))
        {
            var column = bracketTable.Columns.FirstOrDefault(item => item.Key.Equals(bracketMatch.Groups["column"].Value, StringComparison.Ordinal));
            return $"{bracketTable.Label} / {FriendlyRowIdentity(bracketMatch.Groups["row"].Value)} / {column?.Label ?? bracketMatch.Groups["column"].Value}";
        }

        var dotMatch = Regex.Match(path, @"^(?<table>[A-Za-z_][A-Za-z0-9_]*)\.(?<row>[^.]+)\.(?<column>[A-Za-z_][A-Za-z0-9_]*)$");
        if (dotMatch.Success &&
            _metadata.TablesByKey.TryGetValue(dotMatch.Groups["table"].Value, out var dotTable))
        {
            var column = dotTable.Columns.FirstOrDefault(item => item.Key.Equals(dotMatch.Groups["column"].Value, StringComparison.Ordinal));
            return $"{dotTable.Label} / {FriendlyRowIdentity(dotMatch.Groups["row"].Value)} / {column?.Label ?? dotMatch.Groups["column"].Value}";
        }

        return ReplaceKnownKeys(path);
    }

    public string DisplayMessage(string? technicalMessage)
    {
        if (string.IsNullOrWhiteSpace(technicalMessage))
        {
            return "";
        }

        return ReplaceKnownKeys(technicalMessage);
    }

    public ToolDiagnostic ToDisplayDiagnostic(ToolDiagnostic diagnostic)
    {
        var technicalPath = string.IsNullOrWhiteSpace(diagnostic.TechnicalPath)
            ? diagnostic.DisplayPath
            : diagnostic.TechnicalPath;
        var technicalMessage = string.IsNullOrWhiteSpace(diagnostic.TechnicalMessage)
            ? diagnostic.DisplayMessage
            : diagnostic.TechnicalMessage;

        return new ToolDiagnostic(
            diagnostic.Severity,
            DisplayPath(diagnostic.DisplayPath),
            technicalPath,
            DisplayMessage(diagnostic.DisplayMessage),
            technicalMessage);
    }

    public string DisplayPresetPath(string path)
    {
        return DisplayPath(path);
    }

    public static string FriendlyRowIdentity(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
        {
            return "New row";
        }

        var tier = Regex.Match(identity, @"^T(?<tier>\d+)(?:_.*)?$", RegexOptions.IgnoreCase);
        return tier.Success
            ? $"Tier {tier.Groups["tier"].Value}"
            : identity;
    }

    private string ReplaceKnownKeys(string text)
    {
        var replaced = text;
        foreach (var pair in _labelsByKey.OrderByDescending(pair => pair.Key.Length))
        {
            if (string.IsNullOrWhiteSpace(pair.Value) || pair.Key.Equals(pair.Value, StringComparison.Ordinal))
            {
                continue;
            }

            replaced = Regex.Replace(
                replaced,
                $@"(?<![A-Za-z0-9_]){Regex.Escape(pair.Key)}(?![A-Za-z0-9_])",
                pair.Value);
        }

        return replaced;
    }

    private static IReadOnlyDictionary<string, string> BuildLabels(ConfigMetadata metadata)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var setting in metadata.Settings)
        {
            labels[setting.Key] = setting.Label;
        }

        foreach (var table in metadata.Tables)
        {
            labels[table.Key] = table.Label;
            foreach (var column in table.Columns)
            {
                labels[$"{table.Key}.{column.Key}"] = $"{table.Label} {column.Label}";
            }
        }

        return labels;
    }
}
