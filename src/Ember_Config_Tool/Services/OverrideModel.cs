namespace Ember_Config_Tool.Services;

public enum ConfigValueState
{
    Unset,
    Nil,
    Value
}

public sealed record ScalarOverride(SettingDefinition Definition, ConfigValueState State, LuaValue? Value);

public sealed record TableCellOverride(TableColumnDefinition Column, ConfigValueState State, LuaValue? Value);

public sealed record TableRowOverride(string Identity, IReadOnlyList<TableCellOverride> Cells);

public sealed record TableOverride(TableDefinition Definition, bool WholeTableEnabled, IReadOnlyList<TableRowOverride> Rows);

public sealed class OverrideDocument
{
    public List<ScalarOverride> Scalars { get; } = [];
    public List<TableOverride> Tables { get; } = [];

    public bool HasAnyOverride =>
        Scalars.Any(item => item.State != ConfigValueState.Unset) ||
        Tables.Any(table => table.WholeTableEnabled || table.Rows.Any(row => row.Cells.Any(cell => cell.State != ConfigValueState.Unset)));
}
