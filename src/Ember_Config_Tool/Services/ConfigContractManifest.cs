using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ember_Config_Tool.Services;

public sealed class ConfigContractManifest
{
    public int SchemaVersion { get; init; } = 1;
    public List<ContractScalar> Scalars { get; init; } = [];
    public List<ContractTable> Tables { get; init; } = [];

    public IReadOnlyDictionary<string, ContractScalar> ScalarsByKey =>
        Scalars.ToDictionary(scalar => scalar.Key, StringComparer.Ordinal);

    public IReadOnlyDictionary<string, ContractTable> TablesByKey =>
        Tables.ToDictionary(table => table.Key, StringComparer.Ordinal);

    public static ConfigContractManifest LoadDefault()
    {
        var contentPath = Path.Combine(AppContext.BaseDirectory, "Assets", "ConfigContractManifest.json");
        if (File.Exists(contentPath))
        {
            return LoadFromJson(File.ReadAllText(contentPath));
        }

        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "Ember_Config_Tool.Assets.ConfigContractManifest.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Embedded ConfigContractManifest.json was not found.");
        using var reader = new StreamReader(stream);
        return LoadFromJson(reader.ReadToEnd());
    }

    public static ConfigContractManifest LoadFromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return JsonSerializer.Deserialize<ConfigContractManifest>(json, options)
            ?? throw new InvalidOperationException("Config contract manifest is empty.");
    }

    public static ConfigContractManifest FromMetadata(ConfigMetadata metadata)
    {
        return new ConfigContractManifest
        {
            Scalars = metadata.Settings
                .OrderBy(setting => setting.Key, StringComparer.Ordinal)
                .Select(setting => new ContractScalar
                {
                    Key = setting.Key,
                    Disposition = setting.Disposition,
                    ValueType = setting.ValueType,
                    Nullable = setting.Nullable,
                    MasterToggle = setting.MasterToggle,
                    Reason = setting.Reason
                })
                .ToList(),
            Tables = metadata.Tables
                .OrderBy(table => table.Key, StringComparer.Ordinal)
                .Select(table => new ContractTable
                {
                    Key = table.Key,
                    Disposition = table.Disposition,
                    Writer = table.Writer,
                    RowIdentity = table.RowIdentity,
                    MasterToggle = table.MasterToggle,
                    Columns = table.Columns
                        .OrderBy(column => column.Key, StringComparer.Ordinal)
                        .Select(column => new ContractTableColumn
                        {
                            Key = column.Key,
                            ValueType = column.ValueType,
                            Nullable = column.Nullable,
                            ReadOnly = column.ReadOnly
                        })
                        .ToList(),
                    RowShapes = table.RowShapes
                        .OrderBy(shape => shape.Id, StringComparer.Ordinal)
                        .Select(shape => new ContractRowShape
                        {
                            Id = shape.Id,
                            TypeValue = shape.TypeValue,
                            RequiredFields = shape.RequiredFields.Order(StringComparer.Ordinal).ToList(),
                            Columns = shape.Columns.Order(StringComparer.Ordinal).ToList()
                        })
                        .ToList()
                })
                .ToList()
        };
    }
}

public sealed class ContractScalar
{
    public string Key { get; init; } = "";
    public string Disposition { get; init; } = "";
    public string ValueType { get; init; } = "";
    public bool Nullable { get; init; }
    public string? MasterToggle { get; init; }
    public string? Reason { get; init; }
}

public sealed class ContractTable
{
    public string Key { get; init; } = "";
    public string Disposition { get; init; } = "";
    public string Writer { get; init; } = "";
    public string RowIdentity { get; init; } = "";
    public string? MasterToggle { get; init; }
    public List<ContractTableColumn> Columns { get; init; } = [];
    public List<ContractRowShape> RowShapes { get; init; } = [];
}

public sealed class ContractTableColumn
{
    public string Key { get; init; } = "";
    public string ValueType { get; init; } = "";
    public bool Nullable { get; init; }
    public bool ReadOnly { get; init; }
}

public sealed class ContractRowShape
{
    public string Id { get; init; } = "";
    public string? TypeValue { get; init; }
    public List<string> RequiredFields { get; init; } = [];
    public List<string> Columns { get; init; } = [];
}
