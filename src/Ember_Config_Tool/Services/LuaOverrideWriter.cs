using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Ember_Config_Tool.Services;

public sealed record ConflictWarning(string Path, string Message);

public static partial class LuaOverrideWriter
{
    public const string BeginMarker = "-- BEGIN EMBER CONFIG TOOL MANAGED BLOCK";
    public const string EndMarker = "-- END EMBER CONFIG TOOL MANAGED BLOCK";

    public static SaveResult SaveAtomic(string overridePath, OverrideDocument document, IReadOnlyCollection<string>? importedManualPaths = null)
    {
        var directory = Path.GetDirectoryName(overridePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("The override path has no parent directory.");
        }

        Directory.CreateDirectory(directory);
        var existing = File.Exists(overridePath) ? File.ReadAllText(overridePath) : "";
        var cleanedExisting = importedManualPaths is { Count: > 0 }
            ? OverrideStateLoader.RemoveImportedManualAssignments(existing, importedManualPaths)
            : existing;
        var conflicts = DetectConflicts(cleanedExisting, document);
        var rendered = RenderFile(cleanedExisting, document);
        var tempPath = Path.Combine(directory, $".User_Config_Overrides.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(tempPath, rendered, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (File.Exists(overridePath))
            {
                File.Replace(tempPath, overridePath, null);
            }
            else
            {
                File.Move(tempPath, overridePath);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        return new SaveResult(conflicts);
    }

    public static string RenderFile(string existingContent, OverrideDocument document)
    {
        var manual = RemoveManagedBlock(existingContent).TrimEnd();
        var block = RenderManagedBlock(document).TrimEnd();
        if (string.IsNullOrWhiteSpace(manual))
        {
            return block + Environment.NewLine;
        }

        return manual + Environment.NewLine + Environment.NewLine + block + Environment.NewLine;
    }

    public static string RenderManagedBlock(OverrideDocument document)
    {
        var writer = new StringBuilder();
        writer.AppendLine(BeginMarker);
        writer.AppendLine("-- schema_version = 1");
        writer.AppendLine("-- Tool-managed overrides only.");

        foreach (var scalar in document.Scalars
                     .Where(item => item.State != ConfigValueState.Unset)
                     .OrderBy(item => item.Definition.Key, StringComparer.Ordinal))
        {
            writer.AppendLine($"{scalar.Definition.Key} = {RenderStateValue(scalar.State, scalar.Value, scalar.Definition.IsInteger)}");
        }

        foreach (var table in document.Tables.OrderBy(item => item.Definition.Key, StringComparer.Ordinal))
        {
            WriteTableOverride(writer, table);
        }

        writer.AppendLine(EndMarker);
        return writer.ToString();
    }

    public static IReadOnlyList<ConflictWarning> DetectConflicts(string existingContent, OverrideDocument document)
    {
        var warnings = new List<ConflictWarning>();
        var manual = RemoveManagedBlock(existingContent);
        if (string.IsNullOrWhiteSpace(manual))
        {
            return warnings;
        }

        var managedPaths = ManagedPaths(document).ToList();
        warnings.AddRange(DetectManagedStyleRowPatchConflicts(manual, managedPaths));
        manual = RemoveManagedStyleRowPatchLoops(manual);

        IReadOnlyList<LuaAssignment> assignments;
        try
        {
            assignments = LuaLiteralParser.ParseAssignments(manual, "User_Config_Overrides.lua");
        }
        catch (LuaParseException ex)
        {
            warnings.Add(new ConflictWarning("manual", $"Manual content uses unsupported Lua syntax; conflict detection is incomplete. {ex.Message}"));
            return warnings;
        }

        foreach (var assignment in assignments)
        {
            foreach (var managed in managedPaths)
            {
                if (Conflicts(assignment, managed))
                {
                    warnings.Add(new ConflictWarning(assignment.PathText, $"Manual assignment conflicts with managed override '{managed.DisplayPath}'."));
                    break;
                }
            }
        }

        return warnings;
    }

    private static IEnumerable<ConflictWarning> DetectManagedStyleRowPatchConflicts(string manual, IReadOnlyList<ManagedPath> managedPaths)
    {
        foreach (Match loopMatch in RowPatchLoopRegex().Matches(manual))
        {
            var table = loopMatch.Groups["table"].Value;
            var body = loopMatch.Groups["body"].Value;
            foreach (Match branchMatch in RowPatchBranchRegex().Matches(body))
            {
                var identity = UnescapeLuaString(branchMatch.Groups["identity"].Value);
                var section = branchMatch.Groups["section"].Value;
                foreach (Match assignmentMatch in RowPatchAssignmentRegex().Matches(section))
                {
                    var field = assignmentMatch.Groups["field"].Value;
                    var display = $"{table}[{identity}].{field}";
                    if (managedPaths.Any(path =>
                            path.Root.Equals(table, StringComparison.Ordinal) &&
                            (path.RowIdentity?.Equals(identity, StringComparison.Ordinal) == true ||
                             path.DisplayPath.Equals(display, StringComparison.Ordinal))))
                    {
                        yield return new ConflictWarning(display, $"Manual row patch conflicts with managed override '{display}'.");
                    }
                }
            }
        }
    }

    private static string RemoveManagedStyleRowPatchLoops(string manual)
    {
        return RowPatchLoopRegex().Replace(manual, "");
    }

    public static string RemoveManagedBlock(string content)
    {
        var start = content.IndexOf(BeginMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            return content;
        }

        var end = content.IndexOf(EndMarker, start, StringComparison.Ordinal);
        if (end < 0)
        {
            return content[..start];
        }

        end += EndMarker.Length;
        while (end < content.Length && (content[end] == '\r' || content[end] == '\n'))
        {
            end++;
        }

        return content[..start] + content[end..];
    }

    public static string RenderValue(LuaValue value, bool forceInteger = false)
    {
        return value switch
        {
            LuaNilValue => "nil",
            LuaBooleanValue boolean => boolean.Value ? "true" : "false",
            LuaNumberValue number => RenderNumber(number.Value, forceInteger),
            LuaStringValue text => RenderString(text.Value),
            LuaTableValue table => RenderInlineTable(table),
            _ => throw new InvalidOperationException($"Unsupported Lua value kind: {value.Kind}")
        };
    }

    private static void WriteTableOverride(StringBuilder writer, TableOverride table)
    {
        if (table.Definition.IsWholeTable)
        {
            if (!table.WholeTableEnabled)
            {
                return;
            }

            writer.AppendLine();
            writer.AppendLine($"{table.Definition.Key} = {{");
            foreach (var row in table.Rows)
            {
                if (ShouldOmitWholeTableRow(row))
                {
                    continue;
                }

                writer.Append("    { ");
                var cells = row.Cells.Where(cell => !cell.Column.ReadOnly).ToList();
                for (var i = 0; i < cells.Count; i++)
                {
                    var cell = cells[i];
                    writer.Append($"{cell.Column.Key} = {RenderStateValue(cell.State == ConfigValueState.Unset ? ConfigValueState.Value : cell.State, cell.Value, cell.Column.IsInteger)}");
                    if (i < cells.Count - 1)
                    {
                        writer.Append(", ");
                    }
                }
                writer.AppendLine(" },");
            }
            writer.AppendLine("}");
            return;
        }

        if (table.Definition.IsNestedField)
        {
            foreach (var row in table.Rows)
            {
                foreach (var cell in row.Cells.Where(cell => cell.State != ConfigValueState.Unset && !cell.Column.ReadOnly).OrderBy(cell => cell.Column.Key, StringComparer.Ordinal))
                {
                    writer.AppendLine($"{table.Definition.Key}.{row.Identity}.{cell.Column.Key} = {RenderStateValue(cell.State, cell.Value, cell.Column.IsInteger)}");
                }
            }

            return;
        }

        if (table.Definition.IsRowPatchByIdentity)
        {
            var rows = table.Rows
                .Select(row => row with { Cells = row.Cells.Where(cell => cell.State != ConfigValueState.Unset && !cell.Column.ReadOnly).OrderBy(cell => cell.Column.Key, StringComparer.Ordinal).ToList() })
                .Where(row => row.Cells.Count > 0)
                .ToList();
            if (rows.Count == 0)
            {
                return;
            }

            writer.AppendLine();
            writer.AppendLine($"for _, row in ipairs({table.Definition.Key} or {{}}) do");
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var keyword = i == 0 ? "if" : "elseif";
                writer.AppendLine($"    {keyword} row.{table.Definition.RowIdentity} == {RenderString(row.Identity)} then");
                foreach (var cell in row.Cells)
                {
                    writer.AppendLine($"        row.{cell.Column.Key} = {RenderStateValue(cell.State, cell.Value, cell.Column.IsInteger)}");
                }
            }

            writer.AppendLine("    end");
            writer.AppendLine("end");
        }
    }

    private static bool ShouldOmitWholeTableRow(TableRowOverride row)
    {
        var identityCell = row.Cells.FirstOrDefault(cell =>
            cell.Column.Key.Equals("targetGuid", StringComparison.Ordinal) ||
            cell.Column.Key.Equals("materialId", StringComparison.Ordinal));
        var identityIsBlank = string.IsNullOrWhiteSpace(row.Identity) ||
                              identityCell?.Value is LuaStringValue { Value.Length: 0 };
        if (!identityIsBlank)
        {
            return false;
        }

        var enabled = row.Cells.FirstOrDefault(cell => cell.Column.Key.Equals("enabled", StringComparison.Ordinal));
        return enabled?.Value is not LuaBooleanValue { Value: true };
    }

    private static string RenderStateValue(ConfigValueState state, LuaValue? value, bool forceInteger)
    {
        if (state == ConfigValueState.Nil)
        {
            return "nil";
        }

        if (value is null)
        {
            return "nil";
        }

        return RenderValue(value, forceInteger);
    }

    private static string RenderInlineTable(LuaTableValue table)
    {
        var parts = new List<string>();
        foreach (var entry in table.Entries)
        {
            var rendered = RenderValue(entry.Value);
            if (entry.IdentifierKey is not null)
            {
                parts.Add($"{entry.IdentifierKey} = {rendered}");
            }
            else if (entry.Key is not null)
            {
                parts.Add($"[{RenderValue(entry.Key)}] = {rendered}");
            }
            else
            {
                parts.Add(rendered);
            }
        }

        return "{ " + string.Join(", ", parts) + " }";
    }

    private static string RenderString(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private static string RenderNumber(decimal value, bool forceInteger)
    {
        if (forceInteger)
        {
            value = decimal.Truncate(value);
        }

        return value.ToString("0.#############################", CultureInfo.InvariantCulture);
    }

    private static IEnumerable<ManagedPath> ManagedPaths(OverrideDocument document)
    {
        foreach (var scalar in document.Scalars.Where(item => item.State != ConfigValueState.Unset))
        {
            yield return new ManagedPath(scalar.Definition.Key, scalar.Definition.Key, null);
        }

        foreach (var table in document.Tables)
        {
            if (table.Definition.IsWholeTable && table.WholeTableEnabled)
            {
                yield return new ManagedPath(table.Definition.Key, table.Definition.Key, null);
                foreach (var row in table.Rows)
                {
                    yield return new ManagedPath(table.Definition.Key, $"{table.Definition.Key}[{row.Identity}]", row.Identity);
                }
            }
            else if (table.Definition.IsNestedField)
            {
                foreach (var row in table.Rows)
                {
                    foreach (var cell in row.Cells.Where(cell => cell.State != ConfigValueState.Unset))
                    {
                        yield return new ManagedPath(table.Definition.Key, $"{table.Definition.Key}.{row.Identity}.{cell.Column.Key}", row.Identity);
                    }
                }
            }
            else if (table.Definition.IsRowPatchByIdentity)
            {
                foreach (var row in table.Rows)
                {
                    foreach (var cell in row.Cells.Where(cell => cell.State != ConfigValueState.Unset))
                    {
                        yield return new ManagedPath(table.Definition.Key, $"{table.Definition.Key}[{row.Identity}].{cell.Column.Key}", row.Identity);
                    }
                }
            }
        }
    }

    private static bool Conflicts(LuaAssignment assignment, ManagedPath managed)
    {
        if (assignment.PathText.Equals(managed.DisplayPath, StringComparison.Ordinal) ||
            assignment.PathText.Equals(managed.Root, StringComparison.Ordinal) ||
            managed.DisplayPath.StartsWith(assignment.PathText + ".", StringComparison.Ordinal))
        {
            return true;
        }

        if (assignment.PathText.Equals(managed.Root, StringComparison.Ordinal) && managed.RowIdentity is not null)
        {
            return TableContainsIdentity(assignment.Value, managed.RowIdentity);
        }

        if (assignment.Value is LuaTableValue table && assignment.PathText.Equals(managed.Root, StringComparison.Ordinal) && managed.RowIdentity is not null)
        {
            return TableContainsIdentity(table, managed.RowIdentity);
        }

        return false;
    }

    private static bool TableContainsIdentity(LuaValue value, string rowIdentity)
    {
        if (value is not LuaTableValue table)
        {
            return false;
        }

        foreach (var row in table.Entries.Select(entry => entry.Value).OfType<LuaTableValue>())
        {
            foreach (var entry in row.Entries)
            {
                if (entry.Value is LuaStringValue text && text.Value.Equals(rowIdentity, StringComparison.Ordinal))
                {
                    return true;
                }

                if (entry.Value is LuaNumberValue number && number.ToCanonicalString().Equals(rowIdentity, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed record ManagedPath(string Root, string DisplayPath, string? RowIdentity);

    private static string UnescapeLuaString(string value)
    {
        return value
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\'", "'", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"for\s+_,\s*row\s+in\s+ipairs\s*\(\s*(?<table>[A-Za-z_][A-Za-z0-9_]*)\s*(?:or\s*\{\})?\s*\)\s*do(?<body>.*?)\nend", RegexOptions.Singleline)]
    private static partial Regex RowPatchLoopRegex();

    [GeneratedRegex(@"(?:if|elseif)\s+row\.(?<identityField>[A-Za-z_][A-Za-z0-9_]*)\s*==\s*(?:""(?<identity>(?:\\""|[^""])*)""|'(?<identity>(?:\\'|[^'])*)')\s+then(?<section>.*?)(?=elseif\s+row\.|$)", RegexOptions.Singleline)]
    private static partial Regex RowPatchBranchRegex();

    [GeneratedRegex(@"row\.(?<field>[A-Za-z_][A-Za-z0-9_]*)\s*=")]
    private static partial Regex RowPatchAssignmentRegex();
}

public sealed record SaveResult(IReadOnlyList<ConflictWarning> Conflicts);
