using System.IO;
using System.Text.RegularExpressions;

namespace Ember_Config_Tool.Services;

public sealed class OverrideLoadResult
{
    public OverrideDocument Document { get; init; } = new();
    public List<ToolDiagnostic> Diagnostics { get; init; } = [];
    public List<string> ImportedManualPaths { get; init; } = [];
    public bool HasManagedBlock { get; init; }
    public bool CanSave => Diagnostics.All(diagnostic => !diagnostic.IsBlocking);

    public static OverrideLoadResult Empty { get; } = new();
}

public static partial class OverrideStateLoader
{
    private const int CurrentSchemaVersion = 1;

    public static OverrideLoadResult LoadFromFile(string overridePath, ConfigMetadata metadata)
    {
        if (!File.Exists(overridePath))
        {
            return OverrideLoadResult.Empty;
        }

        return LoadFromText(File.ReadAllText(overridePath), metadata, overridePath);
    }

    public static OverrideLoadResult LoadFromText(string content, ConfigMetadata metadata, string sourceName)
    {
        var begin = content.IndexOf(LuaOverrideWriter.BeginMarker, StringComparison.Ordinal);
        if (begin >= 0)
        {
            var end = content.IndexOf(LuaOverrideWriter.EndMarker, begin, StringComparison.Ordinal);
            if (end < 0)
            {
                return new OverrideLoadResult
                {
                    HasManagedBlock = true,
                    Diagnostics = [new ToolDiagnostic(DiagnosticSeverity.Blocking, sourceName, "Managed block is missing its end marker.")]
                };
            }

            var blockStart = begin + LuaOverrideWriter.BeginMarker.Length;
            var block = content[blockStart..end];
            var result = ParseManagedOrManual(block, metadata, sourceName, managed: true);
            result.Diagnostics.InsertRange(0, VersionDiagnostics(block, sourceName));
            return new OverrideLoadResult
            {
                Document = result.Document,
                Diagnostics = result.Diagnostics,
                HasManagedBlock = true
            };
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return OverrideLoadResult.Empty;
        }

        var imported = ParseManagedOrManual(content, metadata, sourceName, managed: false);
        if (imported.Document.HasAnyOverride)
        {
            imported.Diagnostics.Add(new ToolDiagnostic(
                DiagnosticSeverity.Warning,
                sourceName,
                "Supported manual assignments were imported into tool state and will move into the managed block on Save."));
        }

        return imported;
    }

    public static string RemoveImportedManualAssignments(string content, IReadOnlyCollection<string> importedPaths)
    {
        if (importedPaths.Count == 0 || string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        var cleaned = RemoveImportedRowPatchLoops(content, importedPaths);
        var lines = cleaned.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        var removed = new bool[lines.Count];

        for (var index = 0; index < lines.Count; index++)
        {
            var trimmed = lines[index].TrimStart();
            var path = importedPaths.FirstOrDefault(item => AssignmentLineStartsPath(trimmed, item));
            if (path is null)
            {
                continue;
            }

            var braceDepth = BraceDelta(lines[index]);
            removed[index] = true;
            while (braceDepth > 0 && index + 1 < lines.Count)
            {
                index++;
                removed[index] = true;
                braceDepth += BraceDelta(lines[index]);
            }
        }

        var kept = lines.Where((_, index) => !removed[index]);
        return string.Join(Environment.NewLine, kept).TrimEnd() + Environment.NewLine;
    }

    private static OverrideLoadResult ParseManagedOrManual(string content, ConfigMetadata metadata, string sourceName, bool managed)
    {
        var diagnostics = new List<ToolDiagnostic>();
        var importedPaths = new List<string>();
        var collector = new OverrideCollector(metadata, diagnostics, importedPaths, managed);

        foreach (var patch in ParseRowPatchLoops(content, sourceName, diagnostics))
        {
            collector.AddRowPatch(patch.TableKey, patch.Identity, patch.Field, patch.Value);
        }

        var withoutLoops = RowPatchLoopRegex().Replace(content, "");
        try
        {
            foreach (var assignment in LuaLiteralParser.ParseAssignments(withoutLoops, sourceName))
            {
                collector.AddAssignment(assignment);
            }
        }
        catch (LuaParseException ex)
        {
            diagnostics.Add(new ToolDiagnostic(
                managed ? DiagnosticSeverity.Blocking : DiagnosticSeverity.Warning,
                sourceName,
                managed
                    ? $"Managed block cannot be parsed. {ex.Message}"
                    : $"Manual content uses unsupported Lua syntax; import and conflict detection are incomplete. {ex.Message}"));
        }

        return new OverrideLoadResult
        {
            Document = collector.ToDocument(),
            Diagnostics = diagnostics,
            ImportedManualPaths = importedPaths.Distinct(StringComparer.Ordinal).ToList(),
            HasManagedBlock = managed
        };
    }

    private static IEnumerable<ToolDiagnostic> VersionDiagnostics(string block, string sourceName)
    {
        var match = Regex.Match(block, @"schema_version\s*=\s*(?<version>\d+)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            yield return new ToolDiagnostic(DiagnosticSeverity.Warning, sourceName, "Managed block has no schema version; parsing as compatible.");
            yield break;
        }

        var version = int.Parse(match.Groups["version"].Value, System.Globalization.CultureInfo.InvariantCulture);
        if (version != CurrentSchemaVersion)
        {
            yield return new ToolDiagnostic(DiagnosticSeverity.Warning, sourceName, $"Managed block schema version {version} differs from current version {CurrentSchemaVersion}; parsing as compatible.");
        }
    }

    private static IEnumerable<RowPatchAssignment> ParseRowPatchLoops(string content, string sourceName, List<ToolDiagnostic> diagnostics)
    {
        var parsedPatches = new List<RowPatchAssignment>();
        foreach (Match loop in RowPatchLoopRegex().Matches(content))
        {
            var table = loop.Groups["table"].Value;
            var body = loop.Groups["body"].Value;
            foreach (Match branch in RowPatchBranchRegex().Matches(body))
            {
                var identity = UnescapeLuaString(branch.Groups["identity"].Value);
                var section = branch.Groups["section"].Value;
                foreach (Match assignment in RowPatchAssignmentRegex().Matches(section))
                {
                    var field = assignment.Groups["field"].Value;
                    var valueText = assignment.Groups["value"].Value.Trim();
                    try
                    {
                        var parsed = LuaLiteralParser.ParseAssignments($"value = {valueText}", sourceName).Single();
                        parsedPatches.Add(new RowPatchAssignment(table, identity, field, parsed.Value));
                    }
                    catch (Exception ex) when (ex is LuaParseException or InvalidOperationException)
                    {
                        diagnostics.Add(new ToolDiagnostic(DiagnosticSeverity.Warning, $"{table}[{identity}].{field}", $"Could not import row patch value. {ex.Message}"));
                    }
                }
            }
        }

        return parsedPatches;
    }

    private static string RemoveImportedRowPatchLoops(string content, IReadOnlyCollection<string> importedPaths)
    {
        return RowPatchLoopRegex().Replace(content, match =>
        {
            var table = match.Groups["table"].Value;
            return importedPaths.Any(path => path.StartsWith(table + "[", StringComparison.Ordinal))
                ? ""
                : match.Value;
        });
    }

    private static bool AssignmentLineStartsPath(string trimmedLine, string importedPath)
    {
        if (importedPath.Contains('[', StringComparison.Ordinal))
        {
            importedPath = importedPath[..importedPath.IndexOf('[', StringComparison.Ordinal)];
        }

        return trimmedLine.StartsWith(importedPath + " ", StringComparison.Ordinal) ||
               trimmedLine.StartsWith(importedPath + "=", StringComparison.Ordinal);
    }

    private static int BraceDelta(string line)
    {
        var delta = 0;
        var inString = false;
        var quote = '\0';
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inString)
            {
                if (ch == '\\')
                {
                    i++;
                    continue;
                }

                if (ch == quote)
                {
                    inString = false;
                }

                continue;
            }

            if (ch is '"' or '\'')
            {
                inString = true;
                quote = ch;
            }
            else if (ch == '{')
            {
                delta++;
            }
            else if (ch == '}')
            {
                delta--;
            }
        }

        return delta;
    }

    private static string UnescapeLuaString(string value)
    {
        var parsed = LuaLiteralParser.ParseAssignments($"value = \"{value}\"", "row-identity").Single();
        return parsed.Value is LuaStringValue text ? text.Value : value;
    }

    private sealed class OverrideCollector
    {
        private readonly ConfigMetadata _metadata;
        private readonly List<ToolDiagnostic> _diagnostics;
        private readonly List<string> _importedPaths;
        private readonly bool _managed;
        private readonly Dictionary<string, ScalarOverride> _scalars = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TableBuilder> _tables = new(StringComparer.Ordinal);
        private readonly HashSet<string> _seenManagedPaths = new(StringComparer.Ordinal);

        public OverrideCollector(ConfigMetadata metadata, List<ToolDiagnostic> diagnostics, List<string> importedPaths, bool managed)
        {
            _metadata = metadata;
            _diagnostics = diagnostics;
            _importedPaths = importedPaths;
            _managed = managed;
        }

        public void AddAssignment(LuaAssignment assignment)
        {
            var path = assignment.PathText;
            if (assignment.Path.Count == 1 && _metadata.SettingsByKey.TryGetValue(path, out var setting))
            {
                AddScalar(setting, ValueState(assignment.Value), ValueForState(assignment.Value), path);
                return;
            }

            if (assignment.Path.Count == 1 && _metadata.TablesByKey.TryGetValue(path, out var wholeTable))
            {
                if (!wholeTable.IsWholeTable)
                {
                    AddDiagnostic(path, $"Managed whole-table assignment is not allowed for writer mode '{wholeTable.Writer}'.");
                    return;
                }

                if (assignment.Value is not LuaTableValue tableValue)
                {
                    AddDiagnostic(path, "Whole-table override must be a table literal.");
                    return;
                }

                AddWholeTable(wholeTable, tableValue, path);
                return;
            }

            if (assignment.Path.Count == 3 &&
                _metadata.TablesByKey.TryGetValue(assignment.Path[0], out var table) &&
                table.IsNestedField)
            {
                AddTableCell(table, assignment.Path[1], assignment.Path[2], assignment.Value, path);
                return;
            }

            AddDiagnostic(path, "Assignment is not supported by the config tool contract.");
        }

        public void AddRowPatch(string tableKey, string identity, string field, LuaValue value)
        {
            if (!_metadata.TablesByKey.TryGetValue(tableKey, out var table) || !table.IsRowPatchByIdentity)
            {
                AddDiagnostic($"{tableKey}[{identity}].{field}", "Row patch targets an unsupported table.");
                return;
            }

            AddTableCell(table, identity, field, value, $"{tableKey}[{identity}].{field}");
        }

        public OverrideDocument ToDocument()
        {
            var document = new OverrideDocument();
            document.Scalars.AddRange(_scalars.Values.OrderBy(item => item.Definition.Key, StringComparer.Ordinal));
            document.Tables.AddRange(_tables.Values.Select(builder => builder.ToOverride()).OrderBy(item => item.Definition.Key, StringComparer.Ordinal));
            return document;
        }

        private void AddScalar(SettingDefinition setting, ConfigValueState state, LuaValue? value, string path)
        {
            if (!setting.IsWritable)
            {
                AddDiagnostic(path, "Non-public setting is not allowed in tool-managed state.");
                return;
            }

            if (!TrackPath(path))
            {
                return;
            }

            _scalars[setting.Key] = new ScalarOverride(setting, state, value);
            MarkImported(path);
        }

        private void AddWholeTable(TableDefinition table, LuaTableValue tableValue, string path)
        {
            if (!table.IsPublic)
            {
                AddDiagnostic(path, "Non-public table is not allowed in tool-managed state.");
                return;
            }

            if (!TrackPath(path))
            {
                return;
            }

            var rows = new List<TableRowOverride>();
            var index = 0;
            foreach (var entry in tableValue.Entries)
            {
                index++;
                if (entry.Value is not LuaTableValue rowTable)
                {
                    AddDiagnostic(path, "Whole-table override rows must be table literals.");
                    continue;
                }

                var identity = TableRowIdentity(table, rowTable) ?? index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var cells = new List<TableCellOverride>();
                foreach (var column in table.Columns.Where(column => !column.ReadOnly))
                {
                    rowTable.TryGetField(column.Key, out var value);
                    cells.Add(new TableCellOverride(column, ValueState(value), ValueForState(value)));
                }

                rows.Add(new TableRowOverride(identity, cells));
            }

            _tables[table.Key] = new TableBuilder(table, wholeTableEnabled: true, rows);
            MarkImported(path);
        }

        private void AddTableCell(TableDefinition table, string identity, string field, LuaValue value, string path)
        {
            if (!table.IsPublic)
            {
                AddDiagnostic(path, "Non-public table is not allowed in tool-managed state.");
                return;
            }

            var column = table.Columns.FirstOrDefault(column => column.Key.Equals(field, StringComparison.Ordinal));
            if (column is null)
            {
                AddDiagnostic(path, "Table field is not in the checked metadata contract.");
                return;
            }

            if (column.ReadOnly)
            {
                AddDiagnostic(path, _managed ? "Managed block writes to read-only fields are not allowed." : "Read-only manual assignment was ignored.");
                return;
            }

            if (!TrackPath(path))
            {
                return;
            }

            if (!_tables.TryGetValue(table.Key, out var builder))
            {
                builder = new TableBuilder(table, wholeTableEnabled: false, []);
                _tables[table.Key] = builder;
            }

            if (!builder.AddCell(identity, new TableCellOverride(column, ValueState(value), ValueForState(value))))
            {
                AddDiagnostic(path, "Duplicate table cell assignment.");
                return;
            }

            MarkImported(path);
        }

        private bool TrackPath(string path)
        {
            if (!_managed)
            {
                return true;
            }

            if (_seenManagedPaths.Add(path))
            {
                return true;
            }

            _diagnostics.Add(new ToolDiagnostic(DiagnosticSeverity.Blocking, path, "Duplicate assignment in managed block."));
            return false;
        }

        private void AddDiagnostic(string path, string message)
        {
            _diagnostics.Add(new ToolDiagnostic(_managed ? DiagnosticSeverity.Blocking : DiagnosticSeverity.Warning, path, message));
        }

        private void MarkImported(string path)
        {
            if (!_managed)
            {
                _importedPaths.Add(path);
            }
        }

        private static ConfigValueState ValueState(LuaValue value) => value is LuaNilValue ? ConfigValueState.Nil : ConfigValueState.Value;
        private static LuaValue? ValueForState(LuaValue value) => value is LuaNilValue ? null : value;

        private static string? TableRowIdentity(TableDefinition table, LuaTableValue row)
        {
            if (!row.TryGetField(table.RowIdentity, out var identityValue))
            {
                return null;
            }

            return identityValue switch
            {
                LuaStringValue text => text.Value,
                LuaNumberValue number => number.ToCanonicalString(),
                LuaBooleanValue boolean => boolean.Value ? "true" : "false",
                _ => null
            };
        }
    }

    private sealed class TableBuilder
    {
        private readonly Dictionary<string, List<TableCellOverride>> _cellsByIdentity = new(StringComparer.Ordinal);

        public TableBuilder(TableDefinition definition, bool wholeTableEnabled, IReadOnlyList<TableRowOverride> rows)
        {
            Definition = definition;
            WholeTableEnabled = wholeTableEnabled;
            foreach (var row in rows)
            {
                _cellsByIdentity[row.Identity] = row.Cells.ToList();
            }
        }

        public TableDefinition Definition { get; }
        public bool WholeTableEnabled { get; }

        public bool AddCell(string identity, TableCellOverride cell)
        {
            if (!_cellsByIdentity.TryGetValue(identity, out var cells))
            {
                cells = [];
                _cellsByIdentity[identity] = cells;
            }

            if (cells.Any(existing => existing.Column.Key.Equals(cell.Column.Key, StringComparison.Ordinal)))
            {
                return false;
            }

            cells.Add(cell);
            return true;
        }

        public TableOverride ToOverride()
        {
            var rows = _cellsByIdentity
                .Select(pair => new TableRowOverride(pair.Key, pair.Value.OrderBy(cell => cell.Column.Key, StringComparer.Ordinal).ToList()))
                .ToList();
            return new TableOverride(Definition, WholeTableEnabled, rows);
        }
    }

    private sealed record RowPatchAssignment(string TableKey, string Identity, string Field, LuaValue Value);

    [GeneratedRegex(@"for\s+_,\s*row\s+in\s+ipairs\s*\(\s*(?<table>[A-Za-z_][A-Za-z0-9_]*)\s*(?:or\s*\{\})?\s*\)\s*do(?<body>.*?)\nend", RegexOptions.Singleline)]
    private static partial Regex RowPatchLoopRegex();

    [GeneratedRegex(@"(?:if|elseif)\s+row\.[A-Za-z_][A-Za-z0-9_]*\s*==\s*(?:""(?<identity>(?:\\""|[^""])*)""|'(?<identity>(?:\\'|[^'])*)')\s+then(?<section>.*?)(?=elseif\s+row\.|$)", RegexOptions.Singleline)]
    private static partial Regex RowPatchBranchRegex();

    [GeneratedRegex(@"row\.(?<field>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>[^\r\n;]+)")]
    private static partial Regex RowPatchAssignmentRegex();
}
