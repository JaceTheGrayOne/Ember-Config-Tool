using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ember_Config_Tool.Services;

public sealed class ConfigMetadata
{
    public int SchemaVersion { get; init; }
    public List<GroupDefinition> Groups { get; init; } = [];
    public List<SettingDefinition> Settings { get; init; } = [];
    public List<TableDefinition> Tables { get; init; } = [];

    public IReadOnlyDictionary<string, SettingDefinition> SettingsByKey =>
        Settings.ToDictionary(setting => setting.Key, StringComparer.Ordinal);

    public IReadOnlyDictionary<string, TableDefinition> TablesByKey =>
        Tables.ToDictionary(table => table.Key, StringComparer.Ordinal);

    public IEnumerable<SettingDefinition> PublicSettings =>
        Settings.Where(setting => setting.IsPublic && !setting.UiHidden);

    public IEnumerable<TableDefinition> PublicTables =>
        Tables.Where(table => table.IsPublic && !table.UiHidden);

    public static ConfigMetadata LoadDefault()
    {
        var contentPath = Path.Combine(AppContext.BaseDirectory, "Assets", "ConfigUiMetadata.json");
        if (File.Exists(contentPath))
        {
            return LoadFromJson(File.ReadAllText(contentPath));
        }

        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "Ember_Config_Tool.Assets.ConfigUiMetadata.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Embedded ConfigUiMetadata.json was not found.");
        using var reader = new StreamReader(stream);
        return LoadFromJson(reader.ReadToEnd());
    }

    public static ConfigMetadata LoadFromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return JsonSerializer.Deserialize<ConfigMetadata>(json, options)
            ?? throw new InvalidOperationException("Config UI metadata is empty.");
    }

    public List<string> ValidateShape()
    {
        var issues = new List<string>();
        AddDuplicates(issues, Groups.Select(group => group.Id), "group");
        AddDuplicates(issues, Settings.Select(setting => setting.Key), "setting");
        AddDuplicates(issues, Tables.Select(table => table.Key), "table");

        var groupIds = Groups.Select(group => group.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var setting in Settings)
        {
            if (string.IsNullOrWhiteSpace(setting.Key))
            {
                issues.Add("A metadata setting has no key.");
            }

            if (!string.IsNullOrWhiteSpace(setting.Group) && !groupIds.Contains(setting.Group))
            {
                issues.Add($"{setting.Key} references unknown group '{setting.Group}'.");
            }

            if (setting.IsPublic && setting.Control.Equals("number", StringComparison.OrdinalIgnoreCase) && setting.Min > setting.Max)
            {
                issues.Add($"{setting.Key} has min greater than max.");
            }
        }

        foreach (var table in Tables)
        {
            if (!groupIds.Contains(table.Group))
            {
                issues.Add($"{table.Key} references unknown group '{table.Group}'.");
            }

            if (string.IsNullOrWhiteSpace(table.Writer))
            {
                issues.Add($"{table.Key} has no table writer mode.");
            }

            AddDuplicates(issues, table.Columns.Select(column => column.Key), $"{table.Key} column");
            AddDuplicates(issues, table.RowShapes.Select(shape => shape.Id), $"{table.Key} row shape");

            var columnKeys = table.Columns.Select(column => column.Key).ToHashSet(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(table.RowActivationColumn))
            {
                var activationColumn = table.Columns.FirstOrDefault(column => column.Key.Equals(table.RowActivationColumn, StringComparison.Ordinal));
                if (activationColumn is null)
                {
                    issues.Add($"{table.Key} row activation column '{table.RowActivationColumn}' is not a defined column.");
                }
                else if (!activationColumn.IsBoolean)
                {
                    issues.Add($"{table.Key} row activation column '{table.RowActivationColumn}' must be boolean.");
                }
            }

            foreach (var shape in table.RowShapes)
            {
                if (string.IsNullOrWhiteSpace(shape.Id))
                {
                    issues.Add($"{table.Key} has a row shape without an id.");
                }

                foreach (var column in shape.Columns)
                {
                    if (!columnKeys.Contains(column))
                    {
                        issues.Add($"{table.Key} row shape '{shape.Id}' references unknown column '{column}'.");
                    }
                }
            }
        }

        return issues;
    }

    private static void AddDuplicates(List<string> issues, IEnumerable<string> values, string label)
    {
        foreach (var group in values.Where(value => !string.IsNullOrWhiteSpace(value)).GroupBy(value => value, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            issues.Add($"Duplicate {label}: {group.Key}");
        }
    }
}

public sealed class GroupDefinition
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
}

public sealed class SettingDefinition
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string Group { get; init; } = "";
    public string Disposition { get; init; } = "internal";
    public string Control { get; init; } = "text";
    public string ValueType { get; init; } = "string";
    public bool Nullable { get; init; }
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public decimal? Step { get; init; }
    public string? MasterToggle { get; init; }
    public string? Reason { get; init; }
    public string? Description { get; init; }
    public string? VanillaDefault { get; init; }
    public bool UiHidden { get; init; }
    public List<string> Options { get; init; } = [];
    public List<ValueOptionDefinition> ValueOptions { get; init; } = [];

    public bool IsPublic => Disposition.Equals("public", StringComparison.OrdinalIgnoreCase);
    public bool IsWritable => IsPublic;
    public bool IsBoolean => ValueType.Equals("boolean", StringComparison.OrdinalIgnoreCase);
    public bool IsNumber => ValueType.Equals("number", StringComparison.OrdinalIgnoreCase) || ValueType.Equals("integer", StringComparison.OrdinalIgnoreCase);
    public bool IsInteger => ValueType.Equals("integer", StringComparison.OrdinalIgnoreCase);
}

public sealed class ValueOptionDefinition
{
    public string Label { get; init; } = "";
    public string Value { get; init; } = "";

    public override string ToString()
    {
        return Label;
    }
}

public sealed class TableDefinition
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string Group { get; init; } = "";
    public string Disposition { get; init; } = "internal";
    public string Writer { get; init; } = "";
    public string RowIdentity { get; init; } = "";
    public string? MasterToggle { get; init; }
    public string? RowActivationColumn { get; init; }
    public string? WholeTableWarning { get; init; }
    public bool UiHidden { get; init; }
    public List<TableColumnDefinition> Columns { get; init; } = [];
    public List<TableRowShapeDefinition> RowShapes { get; init; } = [];

    public bool IsPublic => Disposition.Equals("public", StringComparison.OrdinalIgnoreCase);
    public bool IsWholeTable => Writer.Equals("wholeTable", StringComparison.OrdinalIgnoreCase);
    public bool IsNestedField => Writer.Equals("nestedField", StringComparison.OrdinalIgnoreCase);
    public bool IsRowPatchByIdentity => Writer.Equals("rowPatchByIdentity", StringComparison.OrdinalIgnoreCase);

    public TableRowShapeDefinition? ResolveRowShape(LuaTableValue row)
    {
        if (RowShapes.Count == 0)
        {
            return null;
        }

        if (row.TryGetField("Type", out var typeValue) && typeValue is LuaStringValue type)
        {
            var byType = RowShapes.FirstOrDefault(shape =>
                !string.IsNullOrWhiteSpace(shape.TypeValue) &&
                shape.TypeValue.Equals(type.Value, StringComparison.Ordinal));
            if (byType is not null)
            {
                return byType;
            }
        }

        return RowShapes.FirstOrDefault(shape =>
            shape.TypeValue is null &&
            shape.RequiredFields.All(field => row.TryGetField(field, out _)));
    }

    public IReadOnlyList<TableColumnDefinition> ColumnsForRow(LuaTableValue row)
    {
        var shape = ResolveRowShape(row);
        if (shape is null)
        {
            return Columns;
        }

        var byKey = Columns.ToDictionary(column => column.Key, StringComparer.Ordinal);
        return shape.Columns
            .Where(byKey.ContainsKey)
            .Select(key => byKey[key])
            .ToList();
    }
}

public sealed class TableColumnDefinition
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string ValueType { get; init; } = "string";
    public bool Nullable { get; init; }
    public bool ReadOnly { get; init; }
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public decimal? Step { get; init; }
    public List<string> Options { get; init; } = [];

    public bool IsBoolean => ValueType.Equals("boolean", StringComparison.OrdinalIgnoreCase);
    public bool IsNumber => ValueType.Equals("number", StringComparison.OrdinalIgnoreCase) || ValueType.Equals("integer", StringComparison.OrdinalIgnoreCase);
    public bool IsInteger => ValueType.Equals("integer", StringComparison.OrdinalIgnoreCase);
}

public sealed class TableRowShapeDefinition
{
    public string Id { get; init; } = "";
    public string? TypeValue { get; init; }
    public List<string> RequiredFields { get; init; } = [];
    public List<string> Columns { get; init; } = [];
}
