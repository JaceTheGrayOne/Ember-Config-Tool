using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ember_Config_Tool.Services;

public sealed class ConfigDataset
{
    public ConfigDataset(
        string sourceName,
        IReadOnlyList<LuaAssignment> assignments,
        string? configFolder,
        IReadOnlyDictionary<string, string>? defaultHintsBySourceLine = null)
    {
        SourceName = sourceName;
        Assignments = assignments;
        ConfigFolder = configFolder;
        AssignmentsByKey = assignments.ToDictionary(assignment => assignment.PathText, StringComparer.Ordinal);
        DefaultHintsBySourceLine = defaultHintsBySourceLine ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public string SourceName { get; }
    public string? ConfigFolder { get; }
    public IReadOnlyList<LuaAssignment> Assignments { get; }
    public IReadOnlyDictionary<string, LuaAssignment> AssignmentsByKey { get; }
    public IReadOnlyDictionary<string, string> DefaultHintsBySourceLine { get; }

    public LuaValue? ValueFor(string key)
    {
        return AssignmentsByKey.TryGetValue(key, out var assignment) ? assignment.Value : null;
    }

    public string? DefaultHintFor(SourceLocation location)
    {
        return location.Line > 0 && DefaultHintsBySourceLine.TryGetValue(SourceLineKey(location.FilePath, location.Line), out var hint)
            ? hint
            : null;
    }

    public static string SourceLineKey(string filePath, int line) => $"{filePath}\u001f{line}";
}

public static class ConfigSourceLoader
{
    private static readonly Regex DefaultCommentPattern = new(
        @"--\s*default\s*(?:=|:)\s*(?<value>[^|]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static ConfigDataset LoadFromModFolder(string modFolder)
    {
        var configFolder = Path.Combine(modFolder, "src", "Config");
        return LoadFromConfigFolder(configFolder, modFolder);
    }

    public static ConfigDataset LoadFromConfigFolder(string configFolder, string sourceName)
    {
        if (!Directory.Exists(configFolder))
        {
            throw new DirectoryNotFoundException($"Config folder was not found: {configFolder}");
        }

        var assignments = new List<LuaAssignment>();
        var defaultHints = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(configFolder, "*.lua").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var text = File.ReadAllText(file);
            assignments.AddRange(LuaLiteralParser.ParseAssignments(text, file));
            AddDefaultHints(defaultHints, file, text);
        }

        return new ConfigDataset(sourceName, assignments, configFolder, defaultHints);
    }

    public static ConfigDataset LoadSnapshot()
    {
        var contentFolder = Path.Combine(AppContext.BaseDirectory, "Assets", "ConfigSnapshot", "Config");
        if (Directory.Exists(contentFolder) && Directory.GetFiles(contentFolder, "*.lua").Length > 0)
        {
            return LoadFromConfigFolder(contentFolder, "Embedded snapshot");
        }

        var assembly = Assembly.GetExecutingAssembly();
        var assignments = new List<LuaAssignment>();
        var defaultHints = new Dictionary<string, string>(StringComparer.Ordinal);
        var prefix = "Ember_Config_Tool.Assets.ConfigSnapshot.Config";
        foreach (var resourceName in assembly.GetManifestResourceNames()
                     .Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                     .Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded snapshot resource was not found: {resourceName}");
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            assignments.AddRange(LuaLiteralParser.ParseAssignments(text, resourceName));
            AddDefaultHints(defaultHints, resourceName, text);
        }

        if (assignments.Count == 0)
        {
            throw new InvalidOperationException("No embedded snapshot config files were found.");
        }

        return new ConfigDataset("Embedded snapshot", assignments, null, defaultHints);
    }

    private static void AddDefaultHints(Dictionary<string, string> target, string filePath, string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var match = DefaultCommentPattern.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var hint = CleanDefaultHint(match.Groups["value"].Value);
            if (string.IsNullOrWhiteSpace(hint))
            {
                continue;
            }

            var lineNumber = index + 1;
            target[ConfigDataset.SourceLineKey(filePath, lineNumber)] = hint;

            if (!IsCommentOnlyDefaultLine(line))
            {
                continue;
            }

            var targetLine = NextConfigLine(lines, index + 1);
            if (targetLine > 0)
            {
                target[ConfigDataset.SourceLineKey(filePath, targetLine)] = hint;
            }
        }
    }

    private static int NextConfigLine(string[] lines, int startIndex)
    {
        for (var index = startIndex; index < lines.Length; index++)
        {
            var trimmed = lines[index].TrimStart();
            if (trimmed.Length == 0 || trimmed.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            return index + 1;
        }

        return 0;
    }

    private static bool IsCommentOnlyDefaultLine(string line)
    {
        return line.TrimStart().StartsWith("--", StringComparison.Ordinal);
    }

    private static string CleanDefaultHint(string value)
    {
        return value.Trim().TrimEnd(',');
    }
}

public sealed class ConfigManifest
{
    public ConfigManifest(ConfigDataset dataset)
    {
        TopLevelKeys = dataset.Assignments.Select(assignment => assignment.PathText).Order(StringComparer.Ordinal).ToList();
        TableColumns = BuildTableColumns(dataset);
        CanonicalRows = BuildCanonicalRows(dataset);
    }

    public IReadOnlyList<string> TopLevelKeys { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> TableColumns { get; }
    public IReadOnlyList<string> CanonicalRows { get; }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildTableColumns(ConfigDataset dataset)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var assignment in dataset.Assignments.Where(assignment => assignment.Value is LuaTableValue))
        {
            var columns = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var row in EnumerateRows((LuaTableValue)assignment.Value))
            {
                foreach (var field in row.Entries.Where(entry => entry.IdentifierKey is not null).Select(entry => entry.IdentifierKey!))
                {
                    columns.Add(field);
                }
            }

            result[assignment.PathText] = columns.ToList();
        }

        return result;
    }

    private static IReadOnlyList<string> BuildCanonicalRows(ConfigDataset dataset)
    {
        var rows = new List<string>();
        foreach (var assignment in dataset.Assignments.OrderBy(assignment => assignment.PathText, StringComparer.Ordinal))
        {
            rows.Add($"{assignment.PathText}={assignment.Value.Kind}:{assignment.Value.ToCanonicalString()}");
            if (assignment.Value is LuaTableValue table)
            {
                foreach (var path in FlattenTable(assignment.PathText, table))
                {
                    rows.Add(path);
                }
            }
        }

        return rows.Order(StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<LuaTableValue> EnumerateRows(LuaTableValue table)
    {
        foreach (var entry in table.Entries)
        {
            if (entry.Value is LuaTableValue child)
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<string> FlattenTable(string prefix, LuaTableValue table)
    {
        var arrayIndex = 0;
        foreach (var entry in table.Entries)
        {
            var key = entry.IdentifierKey;
            if (key is null && entry.Key is LuaStringValue stringKey)
            {
                key = stringKey.Value;
            }
            else if (key is null && entry.Key is LuaNumberValue numberKey)
            {
                key = numberKey.ToCanonicalString();
            }
            else if (key is null)
            {
                arrayIndex++;
                key = $"[{arrayIndex}]";
            }

            var path = $"{prefix}.{key}";
            yield return $"{path}={entry.Value.Kind}:{entry.Value.ToCanonicalString()}";
            if (entry.Value is LuaTableValue child)
            {
                foreach (var nested in FlattenTable(path, child))
                {
                    yield return nested;
                }
            }
        }
    }
}

public static class MetadataValidator
{
    public static List<string> Validate(ConfigDataset dataset, ConfigMetadata metadata)
    {
        var issues = metadata.ValidateShape();
        ConfigContractManifest? contract = null;
        try
        {
            contract = ConfigContractManifest.LoadDefault();
            ValidateContract(issues, dataset, metadata, contract);
        }
        catch (InvalidOperationException ex)
        {
            issues.Add(ex.Message);
        }

        var settings = metadata.SettingsByKey;
        var tables = metadata.TablesByKey;

        foreach (var assignment in dataset.Assignments)
        {
            if (settings.ContainsKey(assignment.PathText) || tables.ContainsKey(assignment.PathText))
            {
                continue;
            }

            issues.Add($"Config key '{assignment.PathText}' has no metadata disposition.");
        }

        foreach (var setting in metadata.Settings)
        {
            if (!dataset.AssignmentsByKey.ContainsKey(setting.Key))
            {
                issues.Add($"Metadata setting '{setting.Key}' is not present in the config source.");
            }
        }

        foreach (var table in metadata.Tables)
        {
            if (!dataset.AssignmentsByKey.TryGetValue(table.Key, out var assignment))
            {
                issues.Add($"Metadata table '{table.Key}' is not present in the config source.");
                continue;
            }

            if (assignment.Value is not LuaTableValue tableValue)
            {
                issues.Add($"Metadata table '{table.Key}' points at a non-table config value.");
                continue;
            }

            ValidateTableColumns(issues, table, tableValue);
        }

        foreach (var key in metadata.Settings.Where(setting => setting.IsWritable).Select(setting => setting.Key)
                     .Concat(metadata.Tables.Where(table => table.IsPublic).Select(table => table.Key)))
        {
            if (key.Contains("Clamp", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("SafetySkip", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"Safety key '{key}' must not be writable.");
            }
        }

        return issues;
    }

    private static void ValidateContract(List<string> issues, ConfigDataset dataset, ConfigMetadata metadata, ConfigContractManifest contract)
    {
        if (contract.SchemaVersion != 1)
        {
            issues.Add($"Config contract manifest schema version {contract.SchemaVersion} is not supported.");
        }

        var contractScalars = contract.ScalarsByKey;
        var contractTables = contract.TablesByKey;

        foreach (var setting in metadata.Settings)
        {
            if (!contractScalars.TryGetValue(setting.Key, out var scalar))
            {
                issues.Add($"Contract manifest is missing scalar '{setting.Key}'.");
                continue;
            }

            if (!scalar.Disposition.Equals(setting.Disposition, StringComparison.OrdinalIgnoreCase) ||
                !scalar.ValueType.Equals(setting.ValueType, StringComparison.OrdinalIgnoreCase) ||
                scalar.Nullable != setting.Nullable ||
                !string.Equals(scalar.MasterToggle ?? "", setting.MasterToggle ?? "", StringComparison.Ordinal))
            {
                issues.Add($"Contract manifest scalar '{setting.Key}' does not match UI metadata.");
            }
        }

        foreach (var scalar in contract.Scalars)
        {
            if (!metadata.SettingsByKey.ContainsKey(scalar.Key))
            {
                issues.Add($"Contract manifest scalar '{scalar.Key}' is not present in UI metadata.");
            }
        }

        foreach (var table in metadata.Tables)
        {
            if (!contractTables.TryGetValue(table.Key, out var contractTable))
            {
                issues.Add($"Contract manifest is missing table '{table.Key}'.");
                continue;
            }

            if (!contractTable.Disposition.Equals(table.Disposition, StringComparison.OrdinalIgnoreCase) ||
                !contractTable.Writer.Equals(table.Writer, StringComparison.OrdinalIgnoreCase) ||
                !contractTable.RowIdentity.Equals(table.RowIdentity, StringComparison.Ordinal) ||
                !string.Equals(contractTable.MasterToggle ?? "", table.MasterToggle ?? "", StringComparison.Ordinal))
            {
                issues.Add($"Contract manifest table '{table.Key}' does not match UI metadata.");
            }

            var contractColumns = contractTable.Columns.ToDictionary(column => column.Key, StringComparer.Ordinal);
            foreach (var column in table.Columns)
            {
                if (!contractColumns.TryGetValue(column.Key, out var contractColumn))
                {
                    issues.Add($"Contract manifest is missing column '{table.Key}.{column.Key}'.");
                    continue;
                }

                if (!contractColumn.ValueType.Equals(column.ValueType, StringComparison.OrdinalIgnoreCase) ||
                    contractColumn.Nullable != column.Nullable ||
                    contractColumn.ReadOnly != column.ReadOnly)
                {
                    issues.Add($"Contract manifest column '{table.Key}.{column.Key}' does not match UI metadata.");
                }
            }

            foreach (var contractColumn in contractTable.Columns)
            {
                if (!table.Columns.Any(column => column.Key.Equals(contractColumn.Key, StringComparison.Ordinal)))
                {
                    issues.Add($"Contract manifest column '{table.Key}.{contractColumn.Key}' is not present in UI metadata.");
                }
            }
        }

        foreach (var contractTable in contract.Tables)
        {
            if (!metadata.TablesByKey.ContainsKey(contractTable.Key))
            {
                issues.Add($"Contract manifest table '{contractTable.Key}' is not present in UI metadata.");
            }
        }

        foreach (var assignment in dataset.Assignments)
        {
            if (!contractScalars.ContainsKey(assignment.PathText) && !contractTables.ContainsKey(assignment.PathText))
            {
                issues.Add($"Config key '{assignment.PathText}' lacks a checked contract disposition.");
            }
        }
    }

    private static void ValidateTableColumns(List<string> issues, TableDefinition table, LuaTableValue tableValue)
    {
        var metadataColumns = table.Columns.Select(column => column.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var row in tableValue.Entries.Select(entry => entry.Value).OfType<LuaTableValue>())
        {
            if (table.RowShapes.Count > 0 && table.ResolveRowShape(row) is null)
            {
                var identity = TableRowIdentityForMessage(table, row);
                issues.Add($"{table.Key}[{identity}] does not match a known row shape.");
                continue;
            }

            var validColumns = table.ColumnsForRow(row).Select(column => column.Key).ToHashSet(StringComparer.Ordinal);
            foreach (var entry in row.Entries.Where(entry => entry.IdentifierKey is not null))
            {
                var key = entry.IdentifierKey!;
                if (key.Equals(table.RowIdentity, StringComparison.Ordinal) ||
                    key.Equals("Type", StringComparison.Ordinal) && table.RowShapes.Count > 0 ||
                    metadataColumns.Contains(key))
                {
                    if (table.RowShapes.Count > 0 &&
                        metadataColumns.Contains(key) &&
                        !validColumns.Contains(key) &&
                        !table.Columns.First(column => column.Key.Equals(key, StringComparison.Ordinal)).ReadOnly)
                    {
                        issues.Add($"{table.Key}.{key} is not valid for row shape '{table.ResolveRowShape(row)?.Id}'.");
                    }

                    continue;
                }

                issues.Add($"{table.Key}.{key} has no table column metadata disposition.");
            }
        }

        if (!table.IsWholeTable && !table.IsNestedField && !table.IsRowPatchByIdentity)
        {
            issues.Add($"{table.Key} uses unsupported table writer mode '{table.Writer}'.");
        }
    }

    private static string TableRowIdentityForMessage(TableDefinition table, LuaTableValue row)
    {
        if (!string.IsNullOrWhiteSpace(table.RowIdentity) && row.TryGetField(table.RowIdentity, out var value))
        {
            return value.ToCanonicalString();
        }

        return "?";
    }
}

public static class ConfigFingerprint
{
    public static string Compute(ConfigDataset dataset, ConfigMetadata metadata)
    {
        var manifest = new ConfigManifest(dataset);
        var payload = new
        {
            manifest = manifest.CanonicalRows,
            contract = ConfigContractManifest.FromMetadata(metadata),
            settings = metadata.Settings
                .OrderBy(setting => setting.Key, StringComparer.Ordinal)
                .Select(setting => new
                {
                    setting.Key,
                    setting.Disposition,
                    setting.Control,
                    setting.ValueType,
                    setting.Nullable,
                    setting.Min,
                    setting.Max,
                    setting.Step,
                    setting.MasterToggle,
                    setting.UiHidden,
                    ValueOptions = setting.ValueOptions.Select(option => new { option.Label, option.Value }).OrderBy(option => option.Value, StringComparer.Ordinal).ToList(),
                    Options = setting.Options.Order(StringComparer.Ordinal).ToList()
                }),
            tables = metadata.Tables
                .OrderBy(table => table.Key, StringComparer.Ordinal)
                .Select(table => new
                {
                    table.Key,
                    table.Disposition,
                    table.Writer,
                    table.RowIdentity,
                    table.MasterToggle,
                    table.UiHidden,
                    Columns = table.Columns.OrderBy(column => column.Key, StringComparer.Ordinal).Select(column => new
                    {
                        column.Key,
                        column.ValueType,
                        column.Nullable,
                        column.ReadOnly,
                        column.Min,
                        column.Max,
                        column.Step,
                        Options = column.Options.Order(StringComparer.Ordinal).ToList()
                    })
                })
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
