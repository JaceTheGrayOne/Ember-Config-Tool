using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Ember_Config_Tool;
using Ember_Config_Tool.Controls;
using Ember_Config_Tool.Services;
using Ember_Config_Tool.ViewModels;
using EmberApp = Ember_Config_Tool.App;

var tests = new List<(string Name, Action Body)>
{
    ("Lua parser handles supported literals", ParserSupportedLiterals),
    ("Lua parser rejects unsupported syntax", ParserRejectsUnsupportedSyntax),
    ("Snapshot parses and metadata classifies every key", SnapshotMetadataValidation),
    ("Metadata exposes and blocks expected keys", MetadataExposureRules),
    ("Config locator distinguishes source trees from deployed mods", ConfigLocatorSourceTreeRules),
    ("Override writer renders sparse scalar and table modes", OverrideWriterModes),
    ("Override writer preserves manual content and detects conflicts", OverrideConflictDetection),
    ("Override loader hydrates, saves, and reloads managed state", OverrideLoaderHydratesSavesReloads),
    ("Override loader blocks malformed and duplicate managed blocks", OverrideLoaderBlocksMalformedManagedBlocks),
    ("Manual import moves supported assignments into managed block", ManualImportMovesSupportedAssignments),
    ("Row patch importer accepts supported grammar variants", RowPatchImporterVariants),
    ("Table model enforces row shapes, read-only, and unset nullable defaults", TableModelShapeReadOnlyAndUnsetRules),
    ("Scalar model hides override state for normal non-nullable values", ScalarModelDirectEditRules),
    ("Master toggles emit dependent scalar defaults", MasterToggleImplicitScalarOverrideRules),
    ("Whole-table list editing validates destructive row changes", WholeTableListEditingRules),
    ("Presets preserve unset nil value states", PresetRoundTrip),
    ("Presets reject unsafe, read-only, and malformed entries", PresetSafetyRules),
    ("Dependency warnings are warn-only", DependencyWarningsAreWarnOnly),
    ("Numeric policy snaps, clamps, formats, and validates text", NumericPolicyRules),
    ("Config number editor commits without load promotion", ConfigNumberEditorCommitRules),
    ("Display names and diagnostics prefer friendly labels", DisplayNameAndDiagnosticRules),
    ("Fishing tiers are grouped and plainly labeled", FishingTierDisplayRules),
    ("Table state labels preserve internal semantics", TableFriendlyLabelRules),
    ("XAML resources and layout contracts are encoded", XamlResourceAndLayoutContracts),
    ("WPF table and header layout smoke passes", WpfControlAndLayoutSmoke),
    ("Contract manifest and snapshot parity are enforced", ContractAndSnapshotParity),
    ("Nested repo ignore hygiene is present", SourceHygieneRules)
};

var failures = new List<string>();
foreach (var (name, body) in tests)
{
    try
    {
        body();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.Message}");
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine(ex);
    }
}

if (failures.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("Failures:");
    foreach (var failure in failures)
    {
        Console.WriteLine($"- {failure}");
    }

    Environment.Exit(1);
}

Console.WriteLine();
Console.WriteLine($"All {tests.Count} tests passed.");

static void ParserSupportedLiterals()
{
    const string lua = """
        -- comment
        Enable_Test = true
        Nullable_Value = nil
        Text_Value = "hello \"ember\""
        Number_Value = 12.5
        TestTable = {
            T1 = { speed = 1.5, enabled = false, },
            { Name = "row", Min = 1, Max = 2, Enabled = true },
        }
        """;

    var assignments = LuaLiteralParser.ParseAssignments(lua, "inline.lua");
    Assert(assignments.Count == 5, "Expected five assignments.");
    Assert(assignments.Single(item => item.PathText == "Nullable_Value").Value is LuaNilValue, "nil should parse.");
    var table = (LuaTableValue)assignments.Single(item => item.PathText == "TestTable").Value;
    Assert(table.Entries.Count == 2, "Table rows should parse.");
}

static void ParserRejectsUnsupportedSyntax()
{
    const string lua = "Value = function() return 1 end";
    AssertThrows<LuaParseException>(() => LuaLiteralParser.ParseAssignments(lua, "bad.lua"));
}

static void SnapshotMetadataValidation()
{
    var metadata = ConfigMetadata.LoadDefault();
    var dataset = ConfigSourceLoader.LoadSnapshot();
    var issues = MetadataValidator.Validate(dataset, metadata);
    Assert(issues.Count == 0, string.Join(Environment.NewLine, issues));

    var manifest = new ConfigManifest(dataset);
    Assert(manifest.TopLevelKeys.Contains("LOG_LEVEL"), "LOG_LEVEL missing from manifest.");
    Assert(manifest.TableColumns["GliderConfig"].Contains("yawAngleSpeed"), "GliderConfig fields missing.");
    Assert(manifest.TableColumns["ExpandedGameSettings_Config"].Contains("Enabled"), "Expanded settings fields missing.");

    var parentConfig = Path.GetFullPath(Path.Combine(FindToolRoot(), "..", "..", "mods", "Ember", "src", "Config"));
    if (Directory.Exists(parentConfig))
    {
        var liveDataset = ConfigSourceLoader.LoadFromConfigFolder(parentConfig, "live Ember checkout");
        var liveIssues = MetadataValidator.Validate(liveDataset, metadata);
        Assert(liveIssues.Count == 0, "Live Ember config validation failed: " + string.Join(Environment.NewLine, liveIssues));
    }
}

static void MetadataExposureRules()
{
    var metadata = ConfigMetadata.LoadDefault();
    var settings = metadata.SettingsByKey;
    Assert(settings["LOG_LEVEL"].IsPublic, "LOG_LEVEL must be public.");
    Assert(settings["LOG_LEVEL"].ValueOptions.Select(option => option.Label).SequenceEqual(["None", "Limited", "Detailed"]), "LOG_LEVEL value labels must be None, Limited, Detailed.");
    Assert(settings["LOG_LEVEL"].ValueOptions.Select(option => option.Value).SequenceEqual(["0", "1", "2"]), "LOG_LEVEL value options must map to 0, 1, 2.");
    Assert(settings["FastTravel_CustomMarker"].Disposition.Equals("unsupported", StringComparison.OrdinalIgnoreCase), "Custom marker must be unsupported.");
    Assert(!settings["Fog_Density_MaxClamp"].IsWritable, "Clamp keys must not be writable.");
    Assert(!settings["PlacementTweaks_SafetySkip"].IsWritable, "Safety skip strings must not be writable.");
    Assert(metadata.TablesByKey["GliderConfig"].Writer == "nestedField", "Glider writer mode mismatch.");
    Assert(metadata.TablesByKey["ExpandedGameSettings_Config"].Writer == "rowPatchByIdentity", "Expanded settings writer mode mismatch.");
    Assert(metadata.TablesByKey["BlockMaterialReplacements"].Writer == "wholeTable", "Block replacement writer mode mismatch.");
    Assert(settings["Enable_VoxelMaterialReplacer"].IsPublic && settings["Enable_VoxelMaterialReplacer"].UiHidden, "Terrain material replacer should remain public but hidden from the generic UI.");
    Assert(settings["Enable_BlockMaterialReplacer"].IsPublic && settings["Enable_BlockMaterialReplacer"].UiHidden, "Block material replacer should remain public but hidden from the generic UI.");
    Assert(settings["Enable_TerraformingTweaks"].IsPublic && settings["Enable_TerraformingTweaks"].UiHidden, "Terraforming properties should remain public but hidden from the generic UI.");
    Assert(metadata.TablesByKey["VoxelMaterialReplacements"].IsPublic && metadata.TablesByKey["VoxelMaterialReplacements"].UiHidden, "Terrain replacement table should remain public but hidden from the generic UI.");
    Assert(metadata.TablesByKey["BlockMaterialReplacements"].IsPublic && metadata.TablesByKey["BlockMaterialReplacements"].UiHidden, "Block replacement table should remain public but hidden from the generic UI.");
    Assert(metadata.TablesByKey["TerraformingTweaks"].IsPublic && metadata.TablesByKey["TerraformingTweaks"].UiHidden, "Terraforming properties table should remain public but hidden from the generic UI.");
    Assert(!metadata.PublicSettings.Any(setting => setting.Key is "Enable_VoxelMaterialReplacer" or "Enable_BlockMaterialReplacer" or "Enable_TerraformingTweaks"), "Hidden Terraforming settings should not be exposed to the editor.");
    Assert(!metadata.PublicTables.Any(table => table.Key is "VoxelMaterialReplacements" or "BlockMaterialReplacements" or "TerraformingTweaks"), "Hidden Terraforming tables should not be exposed to the editor.");
    Assert(settings["GroundRelative_X"].Nullable, "Updraft nullable setting missing nullable metadata.");
    Assert(settings["Tier5_ReduceRoundsBy"].MasterToggle == "Enable_Fishing_Tweaks", "Fishing dependency missing.");
    var snapshot = ConfigSourceLoader.LoadSnapshot();
    Assert(snapshot.DefaultHintsBySourceLine.Count > 0, "Explicit config default hints should be extracted from Lua comments.");

    var vm = new MainViewModel(new AppOptions());
    var terraforming = vm.Groups.Single(group => group.Id == "terraforming");
    Assert(terraforming.Items.Any(item => item.Key == "Enable_TerrainDropTweaks"), "Visible Terrain Drop Tweaks section should remain in Terraforming.");
    Assert(!terraforming.Items.Any(item => item.Key is "Enable_VoxelMaterialReplacer" or "Enable_BlockMaterialReplacer" or "Enable_TerraformingTweaks" or "VoxelMaterialReplacements" or "BlockMaterialReplacements" or "TerraformingTweaks"), "Hidden Terraforming sections should not be present in the editor model.");
}

static void ConfigLocatorSourceTreeRules()
{
    var root = Path.Combine(Path.GetTempPath(), $"ember-config-locator-{Guid.NewGuid():N}");
    try
    {
        var snapshot = Path.Combine(FindToolRoot(), "src", "Ember_Config_Tool", "Assets", "ConfigSnapshot");
        var deployedRoot = Path.Combine(root, "game", "Enshrouded");
        var deployedMod = Path.Combine(deployedRoot, "mods", "Ember");
        CopyDirectory(snapshot, Path.Combine(deployedMod, "src"));

        var deployedSelection = ConfigLocator.ResolveSelectedFolder(deployedMod);
        Assert(deployedSelection.IsValid, "Deployed mod folder should resolve.");
        Assert(!deployedSelection.IsSourceTree, "Deployed mod folder should not be treated as a source tree.");
        Assert(string.IsNullOrWhiteSpace(deployedSelection.Warning), "Deployed mod folder should not show a source-tree warning.");

        var deployedRootSelection = ConfigLocator.ResolveSelectedFolder(deployedRoot);
        Assert(deployedRootSelection.IsValid, "Deployed game root should normalize to mods\\Ember.");
        Assert(!deployedRootSelection.IsSourceTree, "Deployed game root should not be treated as a source tree.");
        Assert(string.IsNullOrWhiteSpace(deployedRootSelection.Warning), "Deployed game root should not show a source-tree warning.");

        var workspaceRoot = Path.Combine(root, "workspace");
        var workspaceMod = Path.Combine(workspaceRoot, "mods", "Ember");
        CopyDirectory(snapshot, Path.Combine(workspaceMod, "src"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "docs"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "config"));
        File.WriteAllText(Path.Combine(workspaceRoot, "README.md"), "# Ember");
        File.WriteAllText(Path.Combine(workspaceRoot, "config", "ProjectSettings.template.json"), "{}");

        var workspaceSelection = ConfigLocator.ResolveSelectedFolder(workspaceRoot);
        Assert(workspaceSelection.IsValid && workspaceSelection.IsSourceTree, "Workspace root should resolve as a source tree.");
        Assert(!string.IsNullOrWhiteSpace(workspaceSelection.Warning), "Workspace root should show a source-tree warning.");

        var workspaceModSelection = ConfigLocator.ResolveSelectedFolder(workspaceMod);
        Assert(workspaceModSelection.IsValid && workspaceModSelection.IsSourceTree, "Workspace mod folder should resolve as a source tree.");
        Assert(!string.IsNullOrWhiteSpace(workspaceModSelection.Warning), "Workspace mod folder should show a source-tree warning.");
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

static void OverrideWriterModes()
{
    var metadata = ConfigMetadata.LoadDefault();
    var doc = new OverrideDocument();
    doc.Scalars.Add(new ScalarOverride(metadata.SettingsByKey["LOG_LEVEL"], ConfigValueState.Value, new LuaNumberValue(2, "2")));
    doc.Scalars.Add(new ScalarOverride(metadata.SettingsByKey["GroundRelative_X"], ConfigValueState.Nil, null));

    var glider = metadata.TablesByKey["GliderConfig"];
    doc.Tables.Add(new TableOverride(
        glider,
        false,
        [
            new TableRowOverride("T1",
            [
                new TableCellOverride(glider.Columns.Single(column => column.Key == "yawAngleSpeed"), ConfigValueState.Value, new LuaNumberValue(88, "88"))
            ])
        ]));

    var expanded = metadata.TablesByKey["ExpandedGameSettings_Config"];
    doc.Tables.Add(new TableOverride(
        expanded,
        false,
        [
            new TableRowOverride("playerMana",
            [
                new TableCellOverride(expanded.Columns.Single(column => column.Key == "Enabled"), ConfigValueState.Value, new LuaBooleanValue(true)),
                new TableCellOverride(expanded.Columns.Single(column => column.Key == "Max"), ConfigValueState.Value, new LuaNumberValue(1500, "1500"))
            ])
        ]));

    var block = metadata.TablesByKey["BlockMaterialReplacements"];
    doc.Tables.Add(new TableOverride(
        block,
        true,
        [
            new TableRowOverride("sample-guid",
            [
                new TableCellOverride(block.Columns.Single(column => column.Key == "enabled"), ConfigValueState.Value, new LuaBooleanValue(true)),
                new TableCellOverride(block.Columns.Single(column => column.Key == "targetGuid"), ConfigValueState.Value, new LuaStringValue("sample-guid")),
                new TableCellOverride(block.Columns.Single(column => column.Key == "materialIndex"), ConfigValueState.Nil, null)
            ])
        ]));

    var rendered = LuaOverrideWriter.RenderManagedBlock(doc);
    Assert(rendered.Contains("LOG_LEVEL = 2", StringComparison.Ordinal), "Scalar value missing.");
    Assert(rendered.Contains("GroundRelative_X = nil", StringComparison.Ordinal), "Explicit nil missing.");
    Assert(rendered.Contains("GliderConfig.T1.yawAngleSpeed = 88", StringComparison.Ordinal), "Nested glider assignment missing.");
    Assert(rendered.Contains("for _, row in ipairs(ExpandedGameSettings_Config or {}) do", StringComparison.Ordinal), "Row patch loop missing.");
    Assert(rendered.Contains("row.Name == \"playerMana\"", StringComparison.Ordinal), "Row identity patch missing.");
    Assert(rendered.Contains("BlockMaterialReplacements = {", StringComparison.Ordinal), "Whole table write missing.");
    Assert(rendered.Contains("materialIndex = nil", StringComparison.Ordinal), "Whole table nil cell missing.");
}

static void OverrideConflictDetection()
{
    var metadata = ConfigMetadata.LoadDefault();
    var doc = new OverrideDocument();
    doc.Scalars.Add(new ScalarOverride(metadata.SettingsByKey["LOG_LEVEL"], ConfigValueState.Value, new LuaNumberValue(2, "2")));
    var glider = metadata.TablesByKey["GliderConfig"];
    doc.Tables.Add(new TableOverride(
        glider,
        false,
        [
            new TableRowOverride("T1",
            [
                new TableCellOverride(glider.Columns.Single(column => column.Key == "yawAngleSpeed"), ConfigValueState.Value, new LuaNumberValue(88, "88"))
            ])
        ]));
    var expanded = metadata.TablesByKey["ExpandedGameSettings_Config"];
    doc.Tables.Add(new TableOverride(
        expanded,
        false,
        [
            new TableRowOverride("playerMana",
            [
                new TableCellOverride(expanded.Columns.Single(column => column.Key == "Max"), ConfigValueState.Value, new LuaNumberValue(1500, "1500"))
            ])
        ]));

    var existing = """
        -- manual content
        LOG_LEVEL = 1
        GliderConfig = {
            T1 = { yawAngleSpeed = 70 },
        }
        for _, row in ipairs(ExpandedGameSettings_Config or {}) do
            if row.Name == "playerMana" then
                row.Max = 1200
            end
        end
        """;

    var conflicts = LuaOverrideWriter.DetectConflicts(existing, doc);
    Assert(conflicts.Count >= 3, "Expected scalar, table, and row patch conflicts.");
    var rendered = LuaOverrideWriter.RenderFile(existing, doc);
    Assert(rendered.StartsWith("-- manual content", StringComparison.Ordinal), "Manual content should remain first.");
    Assert(rendered.IndexOf(LuaOverrideWriter.BeginMarker, StringComparison.Ordinal) > rendered.IndexOf("GliderConfig =", StringComparison.Ordinal), "Managed block should be appended after manual content.");
}

static void OverrideLoaderHydratesSavesReloads()
{
    var realOverride = RealRepoOverridePath();
    var realExisted = File.Exists(realOverride);
    var realStamp = realExisted ? File.GetLastWriteTimeUtc(realOverride) : DateTime.MinValue;
    using var fixture = DisposableModFixture();
    var overridePath = ConfigLocator.OverridesPath(fixture.ModFolder);
    File.WriteAllText(overridePath, ManagedOverrideSeed(), new System.Text.UTF8Encoding(false));

    var vm = new MainViewModel(new AppOptions { ModFolder = fixture.ModFolder });
    Assert(vm.BlockingIssues.Count == 0, string.Join(Environment.NewLine, vm.BlockingIssues));
    Assert(FindSetting(vm, "LOG_LEVEL").State == ConfigValueState.Value, "LOG_LEVEL override did not load.");
    Assert(FindSetting(vm, "LOG_LEVEL").ValueText == "2", "LOG_LEVEL value did not load.");
    Assert(FindSetting(vm, "GroundRelative_X").State == ConfigValueState.Nil, "Explicit nil did not load.");
    var yaw = FindTable(vm, "GliderConfig").Rows.Single(row => row.Identity == "T1").Cells.Single(cell => cell.Column.Key == "yawAngleSpeed");
    Assert(yaw.State == ConfigValueState.Value && yaw.ValueText == "88", "Nested table override did not load.");
    var mana = FindTable(vm, "ExpandedGameSettings_Config").Rows.Single(row => row.Identity == "playerMana").Cells.Single(cell => cell.Column.Key == "Max");
    Assert(mana.State == ConfigValueState.Value && mana.ValueText == "1500", "Row patch override did not load.");

    vm.SaveCommand.Execute(null);
    Assert(vm.BlockingIssues.Count == 0, "No-op save should not create blocking issues.");
    var saved = File.ReadAllText(overridePath);
    Assert(saved.Contains("-- manual before", StringComparison.Ordinal), "Manual content before managed block was not preserved.");
    Assert(saved.Contains("-- manual after", StringComparison.Ordinal), "Manual content after managed block was not preserved.");
    Assert(saved.Contains("LOG_LEVEL = 2", StringComparison.Ordinal), "No-op save erased scalar override.");
    Assert(saved.Contains("GliderConfig.T1.yawAngleSpeed = 88", StringComparison.Ordinal), "No-op save erased nested override.");
    Assert(saved.Contains("BlockMaterialReplacements = {", StringComparison.Ordinal), "No-op save erased hidden block replacement override.");
    Assert(saved.Contains("materialIndex = 183", StringComparison.Ordinal), "No-op save erased hidden block replacement value.");

    vm.SaveCommand.Execute(null);
    var savedAgain = File.ReadAllText(overridePath);
    Assert(savedAgain.Contains("BlockMaterialReplacements = {", StringComparison.Ordinal), "Second save erased preserved hidden block replacement override.");
    Assert(savedAgain.Contains("materialIndex = 183", StringComparison.Ordinal), "Second save erased preserved hidden block replacement value.");

    var reloaded = new MainViewModel(new AppOptions { ModFolder = fixture.ModFolder });
    Assert(FindSetting(reloaded, "LOG_LEVEL").ValueText == "2", "Reload did not preserve scalar value.");
    var reloadedYaw = FindTable(reloaded, "GliderConfig").Rows.Single(row => row.Identity == "T1").Cells.Single(cell => cell.Column.Key == "yawAngleSpeed");
    Assert(reloadedYaw.State == ConfigValueState.Value && reloadedYaw.ValueText == "88", "Reload did not preserve table value.");

    Assert(File.Exists(realOverride) == realExisted, "Test created or removed the real repo override file.");
    if (realExisted)
    {
        Assert(File.GetLastWriteTimeUtc(realOverride) == realStamp, "Test modified the real repo override file.");
    }
}

static void OverrideLoaderBlocksMalformedManagedBlocks()
{
    using var fixture = DisposableModFixture();
    var overridePath = ConfigLocator.OverridesPath(fixture.ModFolder);
    File.WriteAllText(overridePath, $"{LuaOverrideWriter.BeginMarker}{Environment.NewLine}LOG_LEVEL = 2", new System.Text.UTF8Encoding(false));
    var before = File.ReadAllText(overridePath);
    var malformed = new MainViewModel(new AppOptions { ModFolder = fixture.ModFolder });
    Assert(!malformed.CanSave, "Malformed managed block should disable Save.");
    Assert(malformed.BlockingIssues.Any(issue => issue.Contains("end marker", StringComparison.OrdinalIgnoreCase)), "Missing end marker diagnostic not shown.");
    malformed.SaveCommand.Execute(null);
    Assert(File.ReadAllText(overridePath) == before, "Malformed managed block should not be overwritten by Save.");

    File.WriteAllText(overridePath, $"""
        {LuaOverrideWriter.BeginMarker}
        -- schema_version = 1
        LOG_LEVEL = 1
        LOG_LEVEL = 2
        {LuaOverrideWriter.EndMarker}
        """, new System.Text.UTF8Encoding(false));
    var duplicate = new MainViewModel(new AppOptions { ModFolder = fixture.ModFolder });
    Assert(!duplicate.CanSave, "Duplicate managed assignment should disable Save.");
    Assert(duplicate.BlockingIssues.Any(issue => issue.Contains("Duplicate assignment", StringComparison.OrdinalIgnoreCase)), "Duplicate assignment diagnostic not shown.");

    File.WriteAllText(overridePath, string.Join(Environment.NewLine, new[]
    {
        LuaOverrideWriter.BeginMarker,
        "-- schema_version = 1",
        "for _, row in ipairs(ExpandedGameSettings_Config or {}) do",
        "    if row.Name == \"enableDurability\" then",
        "        row.Type = \"Boolean\"",
        "    end",
        "end",
        LuaOverrideWriter.EndMarker,
        ""
    }), new System.Text.UTF8Encoding(false));
    var readOnly = new MainViewModel(new AppOptions { ModFolder = fixture.ModFolder });
    Assert(!readOnly.CanSave, "Read-only managed table field should block Save.");
    Assert(readOnly.BlockingIssues.Any(issue => issue.Contains("read-only", StringComparison.OrdinalIgnoreCase)), "Read-only managed diagnostic not shown.");
}

static void ManualImportMovesSupportedAssignments()
{
    using var fixture = DisposableModFixture();
    var overridePath = ConfigLocator.OverridesPath(fixture.ModFolder);
    File.WriteAllText(overridePath, """
        -- keep manual
        LOG_LEVEL = 1
        GliderConfig.T1.yawAngleSpeed = 77
        """, new System.Text.UTF8Encoding(false));

    var vm = new MainViewModel(new AppOptions { ModFolder = fixture.ModFolder });
    Assert(FindSetting(vm, "LOG_LEVEL").ValueText == "1", "Manual scalar was not imported.");
    Assert(vm.WarningIssues.Any(issue => issue.Contains("imported", StringComparison.OrdinalIgnoreCase)), "Manual import warning not shown.");
    vm.SaveCommand.Execute(null);

    var saved = File.ReadAllText(overridePath);
    Assert(saved.StartsWith("-- keep manual", StringComparison.Ordinal), "Manual comments should remain.");
    Assert(saved.Contains(LuaOverrideWriter.BeginMarker, StringComparison.Ordinal), "Managed block was not created.");
    Assert(CountOccurrences(saved, "LOG_LEVEL = 1") == 1, "Imported scalar should appear only once.");
    Assert(saved.IndexOf("LOG_LEVEL = 1", StringComparison.Ordinal) > saved.IndexOf(LuaOverrideWriter.BeginMarker, StringComparison.Ordinal), "Imported scalar should move into managed block.");
    Assert(!saved.Split(LuaOverrideWriter.BeginMarker)[0].Contains("GliderConfig.T1.yawAngleSpeed", StringComparison.Ordinal), "Imported nested assignment remained in manual content.");
}

static void RowPatchImporterVariants()
{
    var metadata = ConfigMetadata.LoadDefault();
    var result = OverrideStateLoader.LoadFromText(string.Join(Environment.NewLine, new[]
    {
        "for _, row in ipairs(ExpandedGameSettings_Config or {}) do",
        "    if row.Name == 'playerMana' then",
        "        row.Max = 1400",
        "        row.Enabled = true",
        "    elseif row.Name == \"enableDurability\" then",
        "        row.Value = false",
        "    end",
        "end",
        ""
    }), metadata, "manual.lua");
    Assert(result.Diagnostics.All(diagnostic => !diagnostic.IsBlocking), string.Join(Environment.NewLine, result.Diagnostics));
    var expanded = result.Document.Tables.Single(table => table.Definition.Key == "ExpandedGameSettings_Config");
    var mana = expanded.Rows.Single(row => row.Identity == "playerMana");
    Assert(mana.Cells.Any(cell => cell.Column.Key == "Max" && cell.Value is LuaNumberValue { Value: 1400 }), "Single-quoted row patch identity did not import.");
    Assert(mana.Cells.Any(cell => cell.Column.Key == "Enabled" && cell.Value is LuaBooleanValue { Value: true }), "Reordered row patch assignment did not import.");
    var durability = expanded.Rows.Single(row => row.Identity == "enableDurability");
    Assert(durability.Cells.Any(cell => cell.Column.Key == "Value" && cell.Value is LuaBooleanValue { Value: false }), "elseif row patch branch did not import.");
}

static void TableModelShapeReadOnlyAndUnsetRules()
{
    var vm = new MainViewModel(new AppOptions());
    var expanded = FindTable(vm, "ExpandedGameSettings_Config");
    var scalarRow = expanded.Rows.Single(row => row.Identity == "playerMana");
    Assert(scalarRow.Cells.Select(cell => cell.Column.Key).Order(StringComparer.Ordinal).SequenceEqual(new[] { "Enabled", "Max", "Min", "Steps" }.Order(StringComparer.Ordinal)), "Scalar expanded settings row has invalid cells.");
    var booleanRow = expanded.Rows.Single(row => row.Identity == "enableDurability");
    Assert(booleanRow.Cells.Select(cell => cell.Column.Key).Order(StringComparer.Ordinal).SequenceEqual(new[] { "Enabled", "Value" }.Order(StringComparer.Ordinal)), "Boolean expanded settings row has invalid cells.");
    Assert(expanded.Definition.Columns.Single(column => column.Key == "Type").ReadOnly, "Type column should be read-only in metadata.");

    var block = CreateTableViewModel("BlockMaterialReplacements");
    var nullableDefault = block.Rows.SelectMany(row => row.Cells).First(cell => cell.Column.Key == "materialIndex" && cell.DefaultValue is LuaNilValue);
    Assert(nullableDefault.State == ConfigValueState.Unset, "Fresh nullable table cell should remain Unset.");
    var preset = new Dictionary<string, PresetSettingValue>(StringComparer.Ordinal);
    block.CapturePreset(preset);
    var nilPresetCells = preset.Where(pair => pair.Key.Contains("materialIndex", StringComparison.Ordinal) && pair.Value.State == ConfigValueState.Nil).ToList();
    Assert(nilPresetCells.Count == 0, "Fresh preset capture should not leak explicit nil table cells.");

    var metadata = ConfigMetadata.LoadDefault();
    const string future = """
        ExpandedGameSettings_Config = {
            { Name = "future", Mystery = 1, Enabled = true },
        }
        """;
    var dataset = new ConfigDataset("future", LuaLiteralParser.ParseAssignments(future, "future.lua"), null);
    var issues = MetadataValidator.Validate(dataset, metadata);
    Assert(issues.Any(issue => issue.Contains("known row shape", StringComparison.OrdinalIgnoreCase)), "Unknown row shape should fail validation.");
}

static void ScalarModelDirectEditRules()
{
    var vm = new MainViewModel(new AppOptions());
    var stackSize = FindSetting(vm, "StackSize_MaxStack");
    Assert(!stackSize.UsesOverrideStateSelector, "Normal non-nullable scalar should not show an override-state selector.");
    Assert(stackSize.State == ConfigValueState.Unset, "Normal scalar should still start as an unset sparse override internally.");
    stackSize.ValueText = "500";
    Assert(stackSize.State == ConfigValueState.Value, "Direct scalar edit should promote to an explicit value override.");

    var expandedToggle = FindSetting(vm, "Enable_ExpandedGameSettings");
    Assert(!expandedToggle.BooleanEnabled && expandedToggle.State == ConfigValueState.Unset && !expandedToggle.EffectiveBool, "Boolean toggles should start inactive even if the source config value is true.");
    expandedToggle.BooleanEnabled = true;
    Assert(expandedToggle.BooleanEnabled && expandedToggle.State == ConfigValueState.Value && expandedToggle.EffectiveBool, "Checking a boolean toggle should activate an explicit true override.");
    expandedToggle.BooleanEnabled = false;
    Assert(!expandedToggle.BooleanEnabled && expandedToggle.State == ConfigValueState.Unset && !expandedToggle.EffectiveBool, "Clearing a boolean toggle should return to inactive/unset.");

    var updraft = FindSetting(vm, "GroundRelative_X");
    Assert(updraft.UsesOverrideStateSelector, "Nullable scalar should keep the override-state selector.");

    var stackDefault = FindSetting(vm, "StackSize_MaxStack");
    Assert(stackDefault.DefaultText == "Default = Item Specific", "Scalar default hints should come from explicit Lua Default comments.");
    var spellDefault = FindSetting(vm, "SpellCastTime_Multiplier");
    Assert(spellDefault.DefaultText == "Default = 1.0", "Colon-form scalar default comments should be parsed.");
    var gemDefault = FindSetting(vm, "GemTweaks_Probability_Uncommon");
    Assert(gemDefault.DefaultText == "Default = 0.05", "Preceding scalar default comments should attach to the following assignment.");
    var fishingDefault = FindSetting(vm, "Tier1_RodStrength");
    Assert(fishingDefault.DefaultText == "Default = 1.5", "Inline scalar default comments should be parsed.");
}

static void MasterToggleImplicitScalarOverrideRules()
{
    using var stackFixture = DisposableModFixture();
    var stackVm = new MainViewModel(new AppOptions { ModFolder = stackFixture.ModFolder });
    var stackMaster = FindSetting(stackVm, "Enable_StackSizeTweaks");
    var stackSize = FindSetting(stackVm, "StackSize_MaxStack");
    stackMaster.BooleanEnabled = true;
    Assert(stackSize.State == ConfigValueState.Unset, "Dependent scalar default should not be promoted in the UI before save.");

    stackVm.SaveCommand.Execute(null);
    Assert(stackVm.BlockingIssues.Count == 0, string.Join(Environment.NewLine, stackVm.BlockingIssues));
    var stackSaved = File.ReadAllText(ConfigLocator.OverridesPath(stackFixture.ModFolder));
    Assert(stackSaved.Contains("Enable_StackSizeTweaks = true", StringComparison.Ordinal), "Master toggle was not saved.");
    Assert(stackSaved.Contains("StackSize_MaxStack = 65535", StringComparison.Ordinal), "Enabled master toggle should save the dependent scalar default value.");
    Assert(stackSize.State == ConfigValueState.Unset, "Implicit dependent scalar save should not mutate the editor override state.");

    using var placementFixture = DisposableModFixture();
    var placementVm = new MainViewModel(new AppOptions { ModFolder = placementFixture.ModFolder });
    FindSetting(placementVm, "Enable_PlacementTweaks").BooleanEnabled = true;
    placementVm.SaveCommand.Execute(null);
    Assert(placementVm.BlockingIssues.Count == 0, string.Join(Environment.NewLine, placementVm.BlockingIssues));
    var placementSaved = File.ReadAllText(ConfigLocator.OverridesPath(placementFixture.ModFolder));
    Assert(placementSaved.Contains("Enable_PlacementTweaks = true", StringComparison.Ordinal), "Placement master toggle was not saved.");
    Assert(!placementSaved.Contains("Enable_BuildingTweaks =", StringComparison.Ordinal), "Explicit child toggle should not be saved just because its master is enabled.");
    Assert(!placementSaved.Contains("PlacementTweaks_BuildInFog =", StringComparison.Ordinal), "Build In Shroud should remain explicit under the placement master.");
    Assert(!placementSaved.Contains("PlacementTweaks_NoBuildZoneNeeded =", StringComparison.Ordinal), "Place Outside Altar Zones should remain explicit under the placement master.");
}

static void WholeTableListEditingRules()
{
    var block = CreateTableViewModel("BlockMaterialReplacements");
    var originalCount = block.Rows.Count;
    Assert(!block.AddRowCommand.CanExecute(null), "Add should be disabled until whole-table override is enabled.");

    block.WholeTableEnabled = true;
    Assert(block.AddRowCommand.CanExecute(null), "Add should be enabled after whole-table override is enabled.");
    block.AddRowCommand.Execute(null);
    Assert(block.Rows.Count == originalCount + 1, "Add row did not append a row.");
    var added = block.Rows.Last();
    Assert(added.IsIdentityEditable, "Added row identity should be editable.");
    added.Identity = "not-a-guid";
    Assert(block.Validate().Any(issue => issue.Contains("targetGuid must be a GUID", StringComparison.Ordinal)), "Invalid targetGuid should be rejected.");
    added.Identity = "11111111-1111-1111-1111-111111111111";
    Assert(block.RequiresWholeTableConfirmation, "List row changes should require confirmation.");
    Assert(block.Validate().Any(issue => issue.Contains("confirm whole-table", StringComparison.OrdinalIgnoreCase)), "Missing whole-table confirmation should block validation.");
    block.WholeTableConfirmed = true;
    Assert(!block.Validate().Any(issue => issue.Contains("confirm whole-table", StringComparison.OrdinalIgnoreCase)), "Whole-table confirmation should clear confirmation issue.");

    added.DuplicateCommand.Execute(null);
    var duplicate = block.Rows.Last();
    duplicate.Identity = added.Identity;
    Assert(block.Validate().Any(issue => issue.Contains("duplicate row identity", StringComparison.OrdinalIgnoreCase)), "Duplicate non-empty identities should be rejected.");
    duplicate.Identity = "22222222-2222-2222-2222-222222222222";
    duplicate.MoveUpCommand.Execute(null);
    duplicate.RemoveCommand.Execute(null);
    Assert(!block.Rows.Any(row => row.Identity == "22222222-2222-2222-2222-222222222222"), "Remove should delete row from UI immediately.");

    var document = new OverrideDocument();
    document.Tables.Add(block.ToOverride());
    var rendered = LuaOverrideWriter.RenderManagedBlock(document);
    Assert(!rendered.Contains("targetGuid = \"\"", StringComparison.Ordinal), "Disabled blank placeholders should be omitted from whole-table output.");
}

static void PresetRoundTrip()
{
    var preset = new ConfigPreset
    {
        DisplayName = "Test Preset",
        ConfigFingerprint = "fingerprint",
        Settings =
        {
            ["LOG_LEVEL"] = new PresetSettingValue { State = ConfigValueState.Value, Value = JsonValue.Create(2) },
            ["GroundRelative_X"] = new PresetSettingValue { State = ConfigValueState.Nil },
            ["Enable_FogTweaks"] = new PresetSettingValue { State = ConfigValueState.Unset }
        }
    };

    var temp = Path.Combine(Path.GetTempPath(), $"ember-preset-{Guid.NewGuid():N}.json");
    try
    {
        PresetStore.Export(preset, temp);
        var loaded = PresetStore.Load(temp);
        Assert(loaded.Settings["LOG_LEVEL"].State == ConfigValueState.Value, "Value state did not round-trip.");
        Assert(loaded.Settings["GroundRelative_X"].State == ConfigValueState.Nil, "Nil state did not round-trip.");
        Assert(loaded.Settings["Enable_FogTweaks"].State == ConfigValueState.Unset, "Unset state did not round-trip.");
    }
    finally
    {
        if (File.Exists(temp))
        {
            File.Delete(temp);
        }
    }
}

static void PresetSafetyRules()
{
    var vm = new MainViewModel(new AppOptions());
    var blockGuid = "33333333-3333-3333-3333-333333333333";
    var preset = new ConfigPreset
    {
        DisplayName = "Safety",
        ConfigFingerprint = "mismatch",
        Settings =
        {
            ["LOG_LEVEL"] = new PresetSettingValue { State = ConfigValueState.Value },
            ["PlacementTweaks_SafetySkip"] = new PresetSettingValue { State = ConfigValueState.Value, Value = JsonValue.Create("unsafe") },
            ["ExpandedGameSettings_Config[enableDurability].Type"] = new PresetSettingValue { State = ConfigValueState.Value, Value = JsonValue.Create("Scalar") },
            ["BlockMaterialReplacements"] = new PresetSettingValue { State = ConfigValueState.Value },
            [$"BlockMaterialReplacements[{blockGuid}].targetGuid"] = new PresetSettingValue { State = ConfigValueState.Value, Value = JsonValue.Create(blockGuid) },
            [$"BlockMaterialReplacements[{blockGuid}].materialIndex"] = new PresetSettingValue { State = ConfigValueState.Value, Value = JsonValue.Create(183) },
            [$"BlockMaterialReplacements[{blockGuid}].enabled"] = new PresetSettingValue { State = ConfigValueState.Value, Value = JsonValue.Create(true) }
        }
    };

    vm.ApplyPreset(preset);
    Assert(vm.WarningIssues.Any(issue => issue.Contains("fingerprint", StringComparison.OrdinalIgnoreCase)), "Fingerprint mismatch warning missing.");
    Assert(vm.WarningIssues.Any(issue => issue.Contains("unsafe", StringComparison.OrdinalIgnoreCase) || issue.Contains("non-public", StringComparison.OrdinalIgnoreCase)), "Unsafe preset key warning missing.");
    Assert(vm.WarningIssues.Any(issue => issue.Contains("read-only", StringComparison.OrdinalIgnoreCase)), "Read-only preset key warning missing.");
    Assert(vm.WarningIssues.Any(issue => issue.Contains("hidden preset key", StringComparison.OrdinalIgnoreCase) && issue.Contains("Block Material", StringComparison.OrdinalIgnoreCase)), "Hidden block material preset warning missing.");
    Assert(vm.WarningIssues.Any(issue => issue.Contains("missing a value", StringComparison.OrdinalIgnoreCase)), "Malformed value preset warning missing.");
    Assert(FindSetting(vm, "LOG_LEVEL").State == ConfigValueState.Unset, "Malformed scalar preset should not alter state.");
    Assert(!vm.Groups.SelectMany(group => group.Items).OfType<TableSettingViewModel>().Any(table => table.Key == "BlockMaterialReplacements"), "Hidden block material table should not be present in the editor.");
}

static void DependencyWarningsAreWarnOnly()
{
    using var fixture = DisposableModFixture();
    var vm = new MainViewModel(new AppOptions { ModFolder = fixture.ModFolder });
    FindSetting(vm, "Enable_FogTweaks").ApplyOverride(ConfigValueState.Value, new LuaBooleanValue(false));
    FindSetting(vm, "Ambient_Fog_Density").ApplyOverride(ConfigValueState.Value, new LuaNumberValue(1, "1"));
    Assert(vm.WarningIssues.Any(issue => issue.Contains("Inactive until Fog Tweaks is enabled.", StringComparison.Ordinal)), "Friendly inactive dependency warning missing.");
    Assert(vm.WarningDiagnostics.Any(issue => issue.TechnicalText.Contains("Enable_FogTweaks", StringComparison.Ordinal)), "Technical dependency detail should retain the raw key.");
    Assert(vm.BlockingIssues.Count == 0, string.Join(Environment.NewLine, vm.BlockingIssues));
    vm.SaveCommand.Execute(null);
    Assert(vm.BlockingIssues.Count == 0, "Warn-only dependency should not block Save.");

    var clean = new MainViewModel(new AppOptions());
    clean.SelectedGroup = clean.Groups.Single(group => group.Id == "expanded");
    Assert(!FindSetting(clean, "Enable_ExpandedGameSettings").BooleanEnabled, "Expanded Game Settings master toggle should start inactive.");
    var expanded = FindTable(clean, "ExpandedGameSettings_Config");
    Assert(!expanded.HasExplicitOverride, "Expanded settings table should start with no active overrides.");
    Assert(!clean.WarningIssues.Any(issue => issue.Contains("Expanded Game Settings", StringComparison.Ordinal)), "Inactive expanded settings should not warn when no child override is active.");
}

static void NumericPolicyRules()
{
    Assert(ConfigNumberPolicy.SnapClamp(7.8m, 0m, 10m, 1m) == 8m, "Integer step snap failed.");
    Assert(ConfigNumberPolicy.SnapClamp(12m, 0m, 10m, 1m) == 10m, "Clamp to max failed.");
    Assert(ConfigNumberPolicy.SnapClamp(-2m, 0m, 10m, 1m) == 0m, "Clamp to min failed.");
    Assert(ConfigNumberPolicy.SnapClamp(1.25m, 0m, 10m, 0.5m) == 1.5m, "Midpoint rounding should be away from zero.");
    Assert(ConfigNumberPolicy.SnapClamp(1.24m, 0m, 10m, 0.5m) == 1.0m, "Half step snap failed.");
    Assert(ConfigNumberPolicy.SnapClamp(1.26m, 0m, 10m, 0.1m) == 1.3m, "Tenth step snap failed.");
    Assert(ConfigNumberPolicy.SnapClamp(1.025m, 0m, 10m, 0.05m) == 1.05m, "Five-hundredths midpoint snap failed.");
    Assert(ConfigNumberPolicy.SnapClamp(1.234m, 0m, 10m, 0.01m) == 1.23m, "Hundredths snap failed.");
    Assert(ConfigNumberPolicy.SnapClamp(0.00015m, 0m, 1m, 0.0001m) == 0.0002m, "Small decimal step snap failed.");
    Assert(ConfigNumberPolicy.SnapClamp(-1.24m, -2m, 2m, 0.5m) == -1.0m, "Negative snap failed.");
    Assert(ConfigNumberPolicy.SnapClamp(4m, 10m, 0m, 1m) == 10m, "Reversed bounds should collapse to minimum.");
    Assert(ConfigNumberPolicy.SnapClamp(4m, 0m, 10m, 0m) == 4m, "Zero step should fall back safely.");
    Assert(ConfigNumberPolicy.LargeStep(null, 0.25m) == 1.25m, "Default large step should be step * 5.");
    Assert(ConfigNumberPolicy.LargeStep(3m, 0.25m) == 3m, "Configured large step should win.");
    Assert(ConfigNumberPolicy.FormatDisplay(5m, 1m, true) == "5", "Integer display format failed.");
    Assert(ConfigNumberPolicy.FormatDisplay(1.5m, 0.5m, false) == "1.5", "One-decimal display format failed.");
    Assert(ConfigNumberPolicy.FormatDisplay(1.25m, 0.05m, false) == "1.25", "Two-decimal display format failed.");
    Assert(ConfigNumberPolicy.FormatDisplay(0.1234m, 0.0001m, false) == "0.1234", "Four-decimal display format failed.");
    Assert(ConfigNumberPolicy.DecimalFromDouble(double.NaN, 7m) == 7m, "NaN WPF boundary value should fall back.");
    Assert(ConfigNumberPolicy.DecimalFromDouble(double.PositiveInfinity, 7m) == 7m, "Infinite WPF boundary value should fall back.");

    var commit = ConfigNumberPolicy.ParseAndNormalize("1.23456789", 0m, 0m, 2m, 0.01m, integer: false);
    Assert(commit.Success && commit.Value == 1.23m && commit.DisplayText == "1.23", "Text commit should normalize to the closest permitted step.");
    var clamped = ConfigNumberPolicy.ParseAndNormalize("99", 0m, 0m, 10m, 1m, integer: true);
    Assert(clamped.Success && clamped.Value == 10m && clamped.DisplayText == "10", "Out-of-range text should clamp on commit.");
    var integerCommit = ConfigNumberPolicy.ParseAndNormalize("1.9", 0m, 0m, 10m, 1m, integer: true);
    Assert(integerCommit.Success && integerCommit.Value == 2m && integerCommit.DisplayText == "2", "Integer text commit should snap before rendering.");
    var invalid = ConfigNumberPolicy.ParseAndNormalize("abc", 4m, 0m, 10m, 1m, integer: false);
    Assert(!invalid.Success && invalid.Value == 4m && invalid.ValidationMessage is not null, "Invalid text should not change the committed value.");
}

static void ConfigNumberEditorCommitRules()
{
    RunSta(() =>
    {
        var app = (System.Windows.Application.Current as EmberApp) ?? new EmberApp();
        if (app.TryFindResource("FocusRingBrush") is null)
        {
            app.InitializeComponent();
        }

        var setting = new SettingViewModel(
            new SettingDefinition
            {
                Key = "Test_Number",
                Label = "Test Number",
                Group = "misc",
                Control = "number",
                ValueType = "number",
                Min = 0m,
                Max = 10m,
                Step = 0.5m
            },
            new LuaNumberValue(1.24m, "1.24"));

        var editor = new ConfigNumberEditor
        {
            Minimum = setting.NumberMinimum,
            Maximum = setting.NumberMaximum,
            Increment = setting.NumberIncrement,
            IsInteger = setting.IsInteger
        };
        editor.SetBinding(
            ConfigNumberEditor.ValueTextProperty,
            new Binding(nameof(SettingViewModel.ValueText))
            {
                Source = setting,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        var window = new Window
        {
            Content = editor,
            Width = 520,
            Height = 120,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000
        };

        window.Show();
        window.UpdateLayout();

        try
        {
            Assert(setting.ValueText == "1.24", "Numeric editor should not rewrite source text during initialization.");
            Assert(setting.State == ConfigValueState.Unset, "Numeric editor initialization should not promote an unset scalar.");

            var slider = FindVisualDescendant<Slider>(editor) ?? throw new InvalidOperationException("Number editor slider was not found.");
            var textBox = FindVisualDescendant<TextBox>(editor) ?? throw new InvalidOperationException("Number editor text box was not found.");

            slider.Value = 7.8;
            window.UpdateLayout();
            Assert(setting.ValueText == "8", "Slider changes should snap and update ValueText.");
            Assert(setting.State == ConfigValueState.Value, "Slider changes should preserve direct-edit promotion semantics.");

            textBox.Text = "7.26";
            editor.CommitPendingText();
            Assert(setting.ValueText == "7.5", "Committed text should snap to the configured increment.");

            textBox.Text = "abc";
            editor.CommitPendingText();
            Assert(setting.ValueText == "7.5", "Invalid text should not corrupt the model.");
            Assert(textBox.Text == "7.5", "Invalid text should recover to the last committed value.");

            editor.IsEnabled = false;
            textBox.Text = "9";
            editor.CommitPendingText();
            Assert(setting.ValueText == "7.5", "Disabled numeric editor should not commit text edits.");
        }
        finally
        {
            window.Close();
        }
    });
}

static void DisplayNameAndDiagnosticRules()
{
    var metadata = ConfigMetadata.LoadDefault();
    var names = new ConfigDisplayNames(metadata);
    Assert(names.LabelForKey("Enable_StackSizeTweaks") == "Stack Size Tweaks", "Master toggle label missing.");
    Assert(names.DisplayPath("ExpandedGameSettings_Config[playerMana].Max").Contains("Expanded Game Settings", StringComparison.Ordinal), "Table display path should use table label.");
    Assert(metadata.SettingsByKey["LOG_LEVEL"].ValueOptions.First().ToString() == "None", "Value option display fallback should use the label.");

    var vm = new MainViewModel(new AppOptions());
    FindSetting(vm, "Enable_StackSizeTweaks").ApplyOverride(ConfigValueState.Value, new LuaBooleanValue(false));
    var stackSize = FindSetting(vm, "StackSize_MaxStack");
    stackSize.ApplyOverride(ConfigValueState.Value, new LuaNumberValue(500, "500"));
    Assert(stackSize.DependencyText == "Inactive until Stack Size Tweaks is enabled.", "Dependency text should use the friendly master-toggle label.");
    Assert(vm.WarningIssues.Any(issue => issue.Contains("Stack Size Tweaks", StringComparison.Ordinal)), "Dependency warning should be friendly.");
    Assert(!vm.WarningIssues.Any(issue => issue.Contains("Enable_StackSizeTweaks", StringComparison.Ordinal)), "Visible dependency warnings should not expose raw master keys.");
    Assert(vm.WarningDiagnostics.Any(issue => issue.TechnicalText.Contains("Enable_StackSizeTweaks", StringComparison.Ordinal)), "Raw keys should remain available as technical details.");

    stackSize.ValueText = "not-a-number";
    Assert(vm.BlockingIssues.Any(issue => issue.Contains("Max Stack Size", StringComparison.Ordinal)), "Validation diagnostic should use a friendly setting path.");
    Assert(!vm.BlockingIssues.Any(issue => issue.StartsWith("BLOCKING: StackSize_MaxStack", StringComparison.Ordinal)), "Visible validation should not start with a raw key.");
    Assert(vm.BlockingDiagnostics.Any(issue => issue.TechnicalText.Contains("StackSize_MaxStack", StringComparison.Ordinal)), "Validation technical detail should keep the raw key.");

    var preset = new ConfigPreset
    {
        DisplayName = "Safety",
        ConfigFingerprint = "mismatch",
        Settings =
        {
            ["PlacementTweaks_SafetySkip"] = new PresetSettingValue { State = ConfigValueState.Value, Value = JsonValue.Create("unsafe") }
        }
    };
    vm.ApplyPreset(preset);
    Assert(vm.WarningIssues.Any(issue => issue.Contains("Preset fingerprint", StringComparison.Ordinal)), "Preset fingerprint warning missing.");
    Assert(!vm.WarningIssues.Any(issue => issue.Contains("PlacementTweaks_SafetySkip", StringComparison.Ordinal)), "Visible preset warnings should not expose raw unsafe keys.");

    var diagnostic = new ToolDiagnostic(DiagnosticSeverity.Warning, "StackSize_MaxStack", "StackSize_MaxStack: value is above 1000.");
    var display = names.ToDisplayDiagnostic(diagnostic);
    Assert(display.DisplayPath == "Max Stack Size", "Diagnostic display path should be friendly.");
    Assert(display.TechnicalPath == "StackSize_MaxStack", "Diagnostic technical path should preserve raw key.");
}

static void FishingTierDisplayRules()
{
    var vm = new MainViewModel(new AppOptions());
    var fishing = vm.Groups.Single(group => group.Id == "fishing");
    var sections = fishing.Sections.ToList();

    Assert(sections.Count == 6, "Fishing should render the master toggle section plus five tier sections.");
    Assert(sections[0].Label == "Fishing Tweaks", "Fishing master section should remain first.");
    Assert(sections[0].Items.Select(item => item.Key).SequenceEqual(["Enable_Fishing_Tweaks"]), "Fishing master section should only contain the master toggle.");

    var expectedHeaders = new[]
    {
        ("Tier 1", "Common Fish"),
        ("Tier 2", "Uncommon Fish"),
        ("Tier 3", "Rare Fish"),
        ("Tier 4", "Epic Fish"),
        ("Tier 5", "Legendary Fish")
    };

    var expectedKeysBySuffix = new[]
    {
        "AdvancedGame",
        "ReduceRoundsBy",
        "QuickTimeEvent",
        "RodStrength",
        "RodEndurance"
    };

    var expectedLabels = new[]
    {
        "Advanced Game",
        "Reduce Rounds By",
        "Quick Time Event",
        "Rod Strength",
        "Rod Endurance"
    };

    for (var tierIndex = 0; tierIndex < expectedHeaders.Length; tierIndex++)
    {
        var section = sections[tierIndex + 1];
        var (badge, header) = expectedHeaders[tierIndex];
        var tierNumber = tierIndex + 1;

        Assert(section.BadgeLabel == badge, $"Fishing tier {tierNumber} badge missing.");
        Assert(section.HeaderText == header, $"Fishing tier {tierNumber} header missing.");
        Assert(section.Items.Select(item => item.Key).SequenceEqual(expectedKeysBySuffix.Select(suffix => $"Tier{tierNumber}_{suffix}")), $"Fishing tier {tierNumber} field order is wrong.");
        Assert(section.Items.Select(item => item.DisplayLabel).SequenceEqual(expectedLabels), $"Fishing tier {tierNumber} labels should be plain field names.");
    }
}

static void TableFriendlyLabelRules()
{
    var vm = new MainViewModel(new AppOptions());
    var nullableScalar = FindSetting(vm, "GroundRelative_X");
    Assert(nullableScalar.StateOptions.Select(option => option.Label).SequenceEqual(["Use Default", "Set None", "Custom"]), "Nullable scalar labels should be friendly.");
    Assert(nullableScalar.StateOptions.Select(option => option.TechnicalName).SequenceEqual(["Unset", "Nil", "Value"]), "Nullable scalar technical names should preserve internal semantics.");
    Assert(nullableScalar.StateOptions.Select(option => option.ToString()).SequenceEqual(["Use Default", "Set None", "Custom"]), "State option display fallback should use labels.");

    var block = CreateTableViewModel("BlockMaterialReplacements");
    var nullableCell = block.Rows.SelectMany(row => row.Cells).First(cell => cell.Column.Key == "materialIndex");
    Assert(nullableCell.StateOptions.Select(option => option.Label).SequenceEqual(["Use Default", "Set None", "Custom"]), "Nullable table cell labels should be friendly.");
    Assert(!nullableCell.StateOptions.Any(option => option.Label is "Unset" or "Nil" or "Value"), "State labels should not expose raw enum names.");

    var readOnlyCell = new TableCellViewModel(
        new TableColumnDefinition { Key = "Type", Label = "Type", ValueType = "string", ReadOnly = true },
        new LuaStringValue("Scalar"));
    Assert(readOnlyCell.StateOptions.Count == 1 && readOnlyCell.StateOptions[0].State == ConfigValueState.Unset, "Read-only cells should expose only Use Default state.");

    var row = block.Rows.First();
    Assert(row.DisplayIdentity.TechnicalIdentity == row.Identity, "Row identity display model should preserve technical identity.");
    Assert(!string.IsNullOrWhiteSpace(row.DisplayIdentity.DisplayText), "Row identity display text should be populated.");
    var blockMaterial = row.Cells.Single(cell => cell.Column.Key == "materialIndex");
    Assert(blockMaterial.IsNumber, "Unbounded material ID cells should use manual numeric text entry.");
    blockMaterial.ValueText = "100";
    Assert(blockMaterial.ValueText == "100", "Manual numeric table cells should accept direct text input.");

    var expanded = FindTable(vm, "ExpandedGameSettings_Config");
    var manaMax = expanded.Rows.Single(row => row.Identity == "playerMana").Cells.Single(cell => cell.Column.Key == "Max");
    Assert(manaMax.IsNumber, "Expanded settings Max should use manual numeric text entry.");
    Assert(manaMax.ValueText == "1000", "Expanded settings Max should not be clamped by fallback UI bounds.");
    Assert(!manaMax.ShowsDefaultHint, "Expanded settings should not invent default hints from current config values.");
    Assert(!expanded.Rows.Single(row => row.Identity == "playerMana").Cells.Any(cell => cell.Column.Key == "Value"), "Numeric expanded settings rows should not expose the boolean Value column.");
    var playerEnabled = expanded.Rows.Single(row => row.Identity == "playerHealth").Cells.Single(cell => cell.Column.Key == "Enabled");
    Assert(playerEnabled.IsBoolean && !playerEnabled.ShowsDefaultHint, "Boolean table cells should not show default hint text.");
    Assert(!playerEnabled.ShowsOverrideActivation, "Boolean table cells should use the checkbox as the value editor, not a second activation checkbox.");
    Assert(!playerEnabled.BooleanEnabled && playerEnabled.State == ConfigValueState.Unset, "Boolean table cells should start inactive even if the source config value is true.");
    var playerMin = expanded.Rows.Single(row => row.Identity == "playerHealth").Cells.Single(cell => cell.Column.Key == "Min");
    var playerSteps = expanded.Rows.Single(row => row.Identity == "playerHealth").Cells.Single(cell => cell.Column.Key == "Steps");
    Assert(playerMin.UsesExternalActivation && playerSteps.UsesExternalActivation, "Expanded numeric cells should be activated by the row Enabled checkbox.");
    Assert(!playerMin.ShowsOverrideActivation && !playerSteps.ShowsOverrideActivation, "Expanded Min/Max/Steps should not render separate activation checkboxes.");
    Assert(!playerMin.CanEditValue && playerMin.EffectiveState == ConfigValueState.Unset, "Expanded numeric cells should start inactive until the row is enabled.");
    playerEnabled.BooleanEnabled = true;
    Assert(playerEnabled.State == ConfigValueState.Value && playerEnabled.BoolValue, "Checking a boolean table cell should write an explicit true value.");
    Assert(playerMin.CanEditValue && playerSteps.CanEditValue, "Checking a row Enabled box should enable Min/Max/Steps editing.");
    var playerHealthOverride = expanded.ToOverride().Rows.Single(row => row.Identity == "playerHealth");
    Assert(playerHealthOverride.Cells.Any(cell => cell.Column.Key == "Min" && cell.State == ConfigValueState.Value), "Enabled expanded rows should write Min without a separate Min checkbox.");
    Assert(playerHealthOverride.Cells.Any(cell => cell.Column.Key == "Steps" && cell.State == ConfigValueState.Value), "Enabled expanded rows should write Steps without a separate Steps checkbox.");
    playerEnabled.BooleanEnabled = false;
    Assert(playerEnabled.State == ConfigValueState.Unset && !playerEnabled.BoolValue, "Clearing a boolean table cell should return it to inactive/unset.");
    Assert(!playerMin.CanEditValue && playerMin.EffectiveState == ConfigValueState.Unset, "Clearing row Enabled should deactivate Min/Max/Steps.");
    var durabilityValue = expanded.Rows.Single(row => row.Identity == "enableDurability").Cells.Single(cell => cell.Column.Key == "Value");
    Assert(durabilityValue.Column.Label == "Setting Enabled", "Expanded boolean value column should use a clear label.");
    Assert(durabilityValue.UsesExternalActivation && !durabilityValue.BooleanEditorEnabled, "Expanded boolean value cells should also be gated by row Enabled.");
    var durabilityEnabled = expanded.Rows.Single(row => row.Identity == "enableDurability").Cells.Single(cell => cell.Column.Key == "Enabled");
    durabilityEnabled.BooleanEnabled = true;
    Assert(durabilityValue.BooleanEditorEnabled, "Checking row Enabled should enable the boolean value cell.");

    var glider = FindTable(vm, "GliderConfig");
    Assert(glider.Rows[0].IdentityDisplayText == "Tier 1", "Glider T1 should display as Tier 1.");
    Assert(glider.Rows[3].IdentityDisplayText == "Tier 4", "Glider T4_REWARD should display as Tier 4.");
    var yaw = glider.Rows.First().Cells.Single(cell => cell.Column.Key == "yawAngleSpeed");
    Assert(yaw.DefaultText == "Default = 70.0", "Glider cell should expose the explicit vanilla default hint, not the configured source value.");
    Assert(!yaw.OverrideActive && !yaw.CanEditValue, "Glider cell should start inactive and use the default.");
    yaw.OverrideActive = true;
    Assert(yaw.State == ConfigValueState.Value && yaw.CanEditValue, "Checking override should activate the value input.");
    yaw.OverrideActive = false;
    Assert(yaw.State == ConfigValueState.Unset && !yaw.CanEditValue, "Clearing override should return to default state.");

    block.WholeTableEnabled = true;
    block.WholeTableConfirmed = true;
    nullableCell.State = ConfigValueState.Nil;
    var document = new OverrideDocument();
    document.Tables.Add(block.ToOverride());
    var rendered = LuaOverrideWriter.RenderManagedBlock(document);
    Assert(rendered.Contains("materialIndex = nil", StringComparison.Ordinal), "Writer should preserve internal Nil semantics.");
}

static void XamlResourceAndLayoutContracts()
{
    var toolRoot = FindToolRoot();
    var sourceRoot = Path.Combine(toolRoot, "src", "Ember_Config_Tool");
    var mainWindow = File.ReadAllText(Path.Combine(sourceRoot, "MainWindow.xaml"));
    var app = File.ReadAllText(Path.Combine(sourceRoot, "App.xaml"));
    var focus = File.ReadAllText(Path.Combine(sourceRoot, "Themes", "FocusStates.xaml"));
    var scroll = File.ReadAllText(Path.Combine(sourceRoot, "Themes", "ScrollBars.xaml"));
    var numberEditorXaml = Path.Combine(sourceRoot, "Controls", "ConfigNumberEditor.xaml");
    var numberEditorCode = Path.Combine(sourceRoot, "Controls", "ConfigNumberEditor.xaml.cs");

    Assert(File.Exists(numberEditorXaml), "Approved bounded numeric editor XAML should exist.");
    Assert(File.Exists(numberEditorCode), "Approved bounded numeric editor code-behind should exist.");
    var numberEditor = File.ReadAllText(numberEditorXaml);
    var visibleXaml = string.Join(Environment.NewLine, mainWindow, numberEditor, focus);

    Assert(!mainWindow.Contains("<Slider", StringComparison.Ordinal), "MainWindow should not use raw Slider controls for config values.");
    Assert(mainWindow.Contains("controls:ConfigNumberEditor", StringComparison.Ordinal), "Ranged scalar numbers should use the bounded numeric editor.");
    Assert(numberEditor.Contains("<Slider", StringComparison.Ordinal), "The numeric editor should own the slider implementation.");
    Assert(numberEditor.Contains("Style=\"{StaticResource ConfigSliderStyle}\"", StringComparison.Ordinal), "The numeric editor slider should use the approved style.");
    Assert(mainWindow.Contains("UsesSliderEditor", StringComparison.Ordinal), "Scalar ranged numbers should be routed to slider rows.");
    Assert(mainWindow.Contains("ValueText=\"{Binding ValueText", StringComparison.Ordinal), "Numeric editor should keep ValueText as its source binding.");
    Assert(mainWindow.Contains("Minimum=\"{Binding NumberMinimum}\"", StringComparison.Ordinal), "Numeric editor should bind source-backed minimum.");
    Assert(mainWindow.Contains("Maximum=\"{Binding NumberMaximum}\"", StringComparison.Ordinal), "Numeric editor should bind source-backed maximum.");
    Assert(mainWindow.Contains("Increment=\"{Binding NumberIncrement}\"", StringComparison.Ordinal), "Numeric editor should bind source-backed increment without visible hint text.");
    Assert(!mainWindow.Contains("StateOptions", StringComparison.Ordinal), "MainWindow should not expose state dropdowns.");
    Assert(!visibleXaml.Contains("Use Default", StringComparison.Ordinal), "Visible XAML should not expose Use Default labels.");
    Assert(!visibleXaml.Contains("Set None", StringComparison.Ordinal), "Visible XAML should not expose Set None labels.");
    Assert(!visibleXaml.Contains("Custom", StringComparison.Ordinal), "Visible XAML should not expose Custom state labels.");
    Assert(!visibleXaml.Contains("StateOptions", StringComparison.Ordinal), "Visible XAML should not expose state option collections.");
    Assert(!visibleXaml.Contains("Step", StringComparison.Ordinal) && !visibleXaml.Contains("step", StringComparison.Ordinal), "Visible XAML should not print scalar step metadata.");
    Assert(!mainWindow.Contains("Content=\"Up\"", StringComparison.Ordinal), "Row Up button should not be present.");
    Assert(!mainWindow.Contains("Content=\"Down\"", StringComparison.Ordinal), "Row Down button should not be present.");
    Assert(!mainWindow.Contains("Content=\"Duplicate\"", StringComparison.Ordinal), "Row Duplicate button should not be present.");
    Assert(!mainWindow.Contains("Content=\"Remove\"", StringComparison.Ordinal), "Row Remove button should not be present.");
    Assert(!mainWindow.Contains("Content=\"Value\"", StringComparison.Ordinal), "Boolean table cells should not render a generic Value checkbox label.");
    Assert(!mainWindow.Contains("Use custom value", StringComparison.Ordinal), "Nullable value activation should not use legacy custom-value labels.");
    Assert(mainWindow.Contains("ConfigNumberTextBoxStyle", StringComparison.Ordinal), "Unbounded numeric config inputs should share the compact width style.");
    Assert(mainWindow.Contains("<Setter Property=\"Width\" Value=\"132\" />", StringComparison.Ordinal), "Numeric config inputs should not span the full content width.");
    Assert(mainWindow.Contains("Text=\"{Binding SelectedGroup.Label}\"", StringComparison.Ordinal), "Content title should bind to the selected group label.");
    Assert(mainWindow.Contains("ContentTitleBandStyle", StringComparison.Ordinal), "Content title band style should be present.");
    Assert(mainWindow.Contains("SectionSurfaceStyle", StringComparison.Ordinal), "Section surface style should be present.");
    Assert(mainWindow.Contains("SectionHeaderBandStyle", StringComparison.Ordinal), "Section header band style should be present.");
    Assert(mainWindow.Contains("Text=\"{Binding BadgeLabel}\"", StringComparison.Ordinal), "Tiered sections should render the tier badge.");
    Assert(mainWindow.Contains("Text=\"{Binding HeaderText}\"", StringComparison.Ordinal), "Section headers should bind through the section header presentation text.");
    var tierBadgeIndex = mainWindow.IndexOf("Text=\"{Binding BadgeLabel}\"", StringComparison.Ordinal);
    var tierHeaderIndex = mainWindow.IndexOf("Text=\"{Binding HeaderText}\"", StringComparison.Ordinal);
    var tierHeaderStackIndex = mainWindow.LastIndexOf("<StackPanel Orientation=\"Horizontal\"", tierBadgeIndex, StringComparison.Ordinal);
    Assert(tierHeaderStackIndex >= 0 && tierHeaderStackIndex < tierBadgeIndex && tierBadgeIndex < tierHeaderIndex, "Tier badge and section header should share a horizontal header row.");
    Assert(mainWindow.Contains("<Setter Property=\"FontSize\" Value=\"13\" />", StringComparison.Ordinal), "Section headings should use the redesign header font size.");
    Assert(mainWindow.Contains("ConfigBooleanSwitchStyle", StringComparison.Ordinal), "Boolean values should use switch visuals.");
    Assert(focus.Contains("ConfigBooleanSwitchStyle", StringComparison.Ordinal), "Switch style should be defined in shared resources.");
    Assert(focus.Contains("ConfigSliderStyle", StringComparison.Ordinal), "Slider style should be defined in shared resources.");
    Assert(focus.Contains("ConfigNumberEditorTextBoxStyle", StringComparison.Ordinal), "Centered numeric input style should be defined in shared resources.");
    Assert(!mainWindow.Contains("InlineLabelOrEnabledText", StringComparison.Ordinal), "Boolean switches should not reuse the old checkbox label binding.");
    Assert(mainWindow.Contains("Text=\"Enabled\"", StringComparison.Ordinal), "Switches should show the static Enabled label.");
    Assert(!mainWindow.Contains("<ColumnDefinition Width=\"*\" MinWidth=\"220\" />", StringComparison.Ordinal), "Scalar label columns must not absorb wide-window slack before editor clusters.");
    Assert(CountOccurrences(mainWindow, "<ColumnDefinition Width=\"280\" MinWidth=\"220\" MaxWidth=\"280\" />") >= 5, "Scalar rows should use bounded 220-280 px label columns before editor clusters.");
    Assert(!mainWindow.Contains("TextAlignment=\"Right\"", StringComparison.Ordinal), "Scalar range hints should align with their input instead of the far right edge.");
    Assert(mainWindow.Contains("<Setter Property=\"Width\" Value=\"188\" />", StringComparison.Ordinal), "Table cells should use a compact fixed cell width.");
    var tableValueLabelIndex = mainWindow.IndexOf("Text=\"{Binding Column.Label}\"", StringComparison.Ordinal);
    var tableValueActivationIndex = mainWindow.IndexOf("IsChecked=\"{Binding OverrideActive", Math.Max(tableValueLabelIndex, 0), StringComparison.Ordinal);
    Assert(tableValueLabelIndex >= 0 && tableValueActivationIndex > tableValueLabelIndex, "Table value labels should appear above activation controls.");
    Assert(mainWindow.Contains("CellLabelStyle", StringComparison.Ordinal), "Table value labels should use the compact cell label style.");
    Assert(mainWindow.Contains("TableNumberTextBoxStyle", StringComparison.Ordinal), "Table numeric cells should use centered compact inputs.");
    Assert(!mainWindow.Contains("<UniformGrid", StringComparison.Ordinal), "Table rows should not stretch cells across the full page.");
    Assert(!mainWindow.Contains("Content=\"{Binding Column.Label}\"", StringComparison.Ordinal), "Boolean table cells should not use square checkboxes with the column label as content.");
    Assert(mainWindow.Contains("IsChecked=\"{Binding BooleanEnabled", StringComparison.Ordinal), "Boolean switch bindings should preserve BooleanEnabled.");
    Assert(mainWindow.Contains("ShowsDefaultHint", StringComparison.Ordinal), "Table default hints should be suppressible for boolean cells.");
    Assert(mainWindow.Contains("IsChecked=\"{Binding OverrideActive", StringComparison.Ordinal), "Table value activation should be a checkbox.");
    Assert(!mainWindow.Contains("No blocking issues detected.", StringComparison.Ordinal), "The success validation section should not be visible.");
    Assert(!mainWindow.Contains("WarningDiagnostics", StringComparison.Ordinal), "The bottom warning diagnostics section should not be visible.");
    Assert(!mainWindow.Contains("BlockingDiagnostics", StringComparison.Ordinal), "The bottom blocking diagnostics section should not be visible.");
    Assert(!mainWindow.Contains("Text=\"{Binding StatusText}\"", StringComparison.Ordinal), "The bottom status footer should not be visible.");
    Assert(!mainWindow.Contains("Text=\"{Binding OverridePath}\"", StringComparison.Ordinal), "The bottom override path footer should not be visible.");
    Assert(app.Contains("Themes/EmberDark.xaml", StringComparison.Ordinal), "App resources should merge EmberDark.xaml.");
    Assert(app.Contains("Themes/ScrollBars.xaml", StringComparison.Ordinal), "App resources should merge ScrollBars.xaml.");
    Assert(app.Contains("Themes/FocusStates.xaml", StringComparison.Ordinal), "App resources should merge FocusStates.xaml.");
    Assert(scroll.Contains("TargetType=\"{x:Type ScrollBar}\"", StringComparison.Ordinal), "ScrollBar style missing.");
    Assert(focus.Contains("TargetType=\"TextBox\"", StringComparison.Ordinal), "TextBox focus style missing.");
    Assert(focus.Contains("TargetType=\"ComboBox\"", StringComparison.Ordinal), "ComboBox focus style missing.");
    Assert(focus.Contains("ContentTemplateSelector=\"{TemplateBinding ItemTemplateSelector}\"", StringComparison.Ordinal), "ComboBox selected value should honor DisplayMemberPath and item templates.");
    Assert(focus.Contains("TargetType=\"Button\"", StringComparison.Ordinal), "Button focus style missing.");
    Assert(focus.Contains("TargetType=\"CheckBox\"", StringComparison.Ordinal), "CheckBox focus style missing.");
    Assert(focus.Contains("TargetType=\"ListBoxItem\"", StringComparison.Ordinal), "ListBoxItem focus style missing.");
    Assert(mainWindow.Contains("x:Name=\"ModFolderCommandBand\"", StringComparison.Ordinal), "Mod folder command band should be named.");
    Assert(mainWindow.Contains("x:Name=\"PresetCommandBand\"", StringComparison.Ordinal), "Preset command band should be named.");
    Assert(mainWindow.Contains("x:Name=\"SectionsItemsControl\"", StringComparison.Ordinal), "Sections items control should be named for WPF smoke coverage.");
    Assert(!mainWindow.Contains("BrowseFolderCommand", StringComparison.Ordinal), "Browse control should not be exposed.");
    Assert(!mainWindow.Contains("DetectFolderCommand", StringComparison.Ordinal), "Detect control should not be exposed.");
    Assert(mainWindow.Contains("Content=\"Defaults\"", StringComparison.Ordinal), "Defaults button label should replace Reset.");
    Assert(!mainWindow.Contains("Content=\"Reset\"", StringComparison.Ordinal), "Reset button label should not remain visible.");
    Assert(!mainWindow.Contains("ImportPresetCommand", StringComparison.Ordinal), "Preset import control should not be exposed.");
    Assert(!mainWindow.Contains("ExportPresetCommand", StringComparison.Ordinal), "Preset export control should not be exposed.");
    Assert(mainWindow.Contains("GridSplitter", StringComparison.Ordinal), "Feature/content splitter should remain present.");
}

static void WpfControlAndLayoutSmoke()
{
    RunSta(() =>
    {
        var app = (System.Windows.Application.Current as EmberApp) ?? new EmberApp();
        if (app.TryFindResource("FocusRingBrush") is null)
        {
            app.InitializeComponent();
        }

        Assert(app.TryFindResource(typeof(ScrollBar)) is Style, "Runtime ScrollBar style should resolve.");
        Assert(app.TryFindResource(typeof(TextBox)) is Style, "Runtime TextBox style should resolve.");
        Assert(app.TryFindResource("FocusRingBrush") is not null, "Focus brush should resolve.");
        Assert(app.TryFindResource("ConfigBooleanSwitchStyle") is Style, "Runtime boolean switch style should resolve.");
        Assert(app.TryFindResource("ConfigSliderStyle") is Style, "Runtime config slider style should resolve.");
        Assert(app.TryFindResource("ConfigNumberEditorTextBoxStyle") is Style, "Runtime numeric editor text style should resolve.");

        foreach (var (width, height) in new[] { (1120d, 720d), (1180d, 760d), (1360d, 860d) })
        {
            var window = new MainWindow(new AppOptions())
            {
                Width = width,
                Height = height,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000,
                Top = -20000
            };
            var modBand = (FrameworkElement)window.FindName("ModFolderCommandBand");
            var presetBand = (FrameworkElement)window.FindName("PresetCommandBand");
            Assert(modBand is not null && presetBand is not null, "Named command bands should resolve.");
            if (modBand is null)
            {
                throw new InvalidOperationException("ModFolderCommandBand was not found.");
            }

            if (presetBand is null)
            {
                throw new InvalidOperationException("PresetCommandBand was not found.");
            }

            var headerGrid = (FrameworkElement)modBand.Parent;
            headerGrid.Measure(new Size(width - 28, double.PositiveInfinity));
            headerGrid.Arrange(new Rect(0, 0, width - 28, headerGrid.DesiredSize.Height));
            headerGrid.UpdateLayout();
            Assert(modBand.ActualWidth > 0 && presetBand.ActualWidth > 0, "Command bands should have positive width.");
            Assert(!TransformedRect(modBand, headerGrid).IntersectsWith(TransformedRect(presetBand, headerGrid)), $"Command bands overlap at {width}x{height}.");
            window.Close();
        }

        var scalarWindow = new MainWindow(new AppOptions())
        {
            Width = 1360,
            Height = 860,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000
        };
        var scalarVm = (MainViewModel)scalarWindow.DataContext;
        var explorationGroup = scalarVm.Groups.Single(group => group.Id == "exploration");
        var sliderSetting = explorationGroup.Items.OfType<SettingViewModel>().First(item => item.UsesSliderEditor);
        var originalSliderText = sliderSetting.ValueText;
        var originalSliderState = sliderSetting.State;
        scalarWindow.Show();
        scalarVm.SelectedGroup = explorationGroup;
        scalarWindow.ApplyTemplate();
        scalarWindow.UpdateLayout();
        scalarWindow.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        ApplyTemplates(scalarWindow);
        scalarWindow.UpdateLayout();
        var sectionsControl = (ItemsControl)scalarWindow.FindName("SectionsItemsControl");
        sectionsControl.ApplyTemplate();
        sectionsControl.Measure(new Size(900, double.PositiveInfinity));
        sectionsControl.Arrange(new Rect(0, 0, 900, 620));
        sectionsControl.UpdateLayout();
        ApplyTemplates(sectionsControl);
        sectionsControl.UpdateLayout();
        Assert(FindDescendant<ConfigNumberEditor>(scalarWindow) is not null, "Selected ranged scalar rows should instantiate the numeric editor.");
        Assert(sliderSetting.ValueText == originalSliderText, "Opening ranged scalar controls in WPF should not snap or clamp source text.");
        Assert(sliderSetting.State == originalSliderState, "Opening ranged scalar controls in WPF should not promote unset overrides.");
        scalarWindow.Close();

        var tableWindow = new MainWindow(new AppOptions())
        {
            Width = 1360,
            Height = 860,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000
        };
        var tableVm = (MainViewModel)tableWindow.DataContext;
        var manaMax = FindTable(tableVm, "ExpandedGameSettings_Config").Rows.Single(row => row.Identity == "playerMana").Cells.Single(cell => cell.Column.Key == "Max");
        var originalManaMax = manaMax.ValueText;
        tableVm.SelectedGroup = tableVm.Groups.Single(group => group.Id == "expanded");
        tableWindow.Show();
        tableWindow.ApplyTemplate();
        tableWindow.UpdateLayout();
        Assert(manaMax.ValueText == originalManaMax, "Opening unbounded table numeric cells in WPF should not clamp existing values.");
        tableWindow.Close();
    });
}

static void ContractAndSnapshotParity()
{
    var metadata = ConfigMetadata.LoadDefault();
    var contract = ConfigContractManifest.LoadDefault();
    Assert(contract.Scalars.Count == metadata.Settings.Count, "Contract scalar count does not match metadata.");
    Assert(contract.Tables.Count == metadata.Tables.Count, "Contract table count does not match metadata.");
    Assert(contract.TablesByKey["ExpandedGameSettings_Config"].RowShapes.Count == 3, "Contract row shapes missing.");

    var snapshot = Path.Combine(FindToolRoot(), "src", "Ember_Config_Tool", "Assets", "ConfigSnapshot");
    Assert(File.Exists(Path.Combine(snapshot, "mod.lua")), "Snapshot mod.lua missing.");
    Assert(File.Exists(Path.Combine(snapshot, "User_Config_Overrides_Template.lua")), "Snapshot override template missing.");
    Assert(File.Exists(Path.Combine(snapshot, "Ember_ReadMe.md")), "Snapshot readme missing.");

    var parentConfig = Path.GetFullPath(Path.Combine(FindToolRoot(), "..", "..", "mods", "Ember", "src", "Config"));
    if (Directory.Exists(parentConfig))
    {
        var snapshotConfig = Path.Combine(snapshot, "Config");
        var sourceNames = Directory.GetFiles(parentConfig, "*.lua").Select(Path.GetFileName).Order(StringComparer.OrdinalIgnoreCase).ToList();
        var snapshotNames = Directory.GetFiles(snapshotConfig, "*.lua").Select(Path.GetFileName).Order(StringComparer.OrdinalIgnoreCase).ToList();
        Assert(sourceNames.SequenceEqual(snapshotNames, StringComparer.OrdinalIgnoreCase), "Snapshot config file names differ from source.");
    }

    var buildScript = File.ReadAllText(Path.Combine(FindToolRoot(), "Build-EmberConfigTool.ps1"));
    Assert(buildScript.Contains("Remove-Item", StringComparison.OrdinalIgnoreCase), "Snapshot sync should clear stale config files.");
    Assert(buildScript.Contains("Get-FileHash", StringComparison.OrdinalIgnoreCase), "Snapshot sync should verify file hashes.");
}

static void SourceHygieneRules()
{
    var repoRoot = FindToolRoot();
    var ignore = File.ReadAllText(Path.Combine(repoRoot, ".gitignore"));
    Assert(ignore.Contains("bin/", StringComparison.Ordinal), "bin output must be ignored.");
    Assert(ignore.Contains("obj/", StringComparison.Ordinal), "obj output must be ignored.");
    Assert(ignore.Contains("artifacts/", StringComparison.Ordinal), "publish artifacts must be ignored.");

    var locator = File.ReadAllText(Path.Combine(repoRoot, "src", "Ember_Config_Tool", "Services", "ConfigLocator.cs"));
    Assert(locator.Contains("IsEmberWorkspaceRoot", StringComparison.Ordinal), "Config locator should detect Ember workspace roots by project shape.");
}

static TempModFixture DisposableModFixture()
{
    var source = Path.GetFullPath(Path.Combine(FindToolRoot(), "..", "..", "mods", "Ember"));
    if (!Directory.Exists(source))
    {
        source = Path.Combine(FindToolRoot(), "src", "Ember_Config_Tool", "Assets", "ConfigSnapshot");
    }

    var root = Path.Combine(Path.GetTempPath(), $"ember-config-tool-test-{Guid.NewGuid():N}");
    var modFolder = Path.Combine(root, "Ember");
    CopyDirectory(source, modFolder);
    var overridePath = ConfigLocator.OverridesPath(modFolder);
    if (File.Exists(overridePath))
    {
        File.Delete(overridePath);
    }

    return new TempModFixture(root, modFolder);
}

static void CopyDirectory(string source, string destination)
{
    Directory.CreateDirectory(destination);
    foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
    {
        Directory.CreateDirectory(directory.Replace(source, destination, StringComparison.Ordinal));
    }

    foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
    {
        var target = file.Replace(source, destination, StringComparison.Ordinal);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(file, target, overwrite: true);
    }
}

static SettingViewModel FindSetting(MainViewModel vm, string key)
{
    return vm.Groups.SelectMany(group => group.Items).OfType<SettingViewModel>().Single(item => item.Key == key);
}

static TableSettingViewModel FindTable(MainViewModel vm, string key)
{
    return vm.Groups.SelectMany(group => group.Items).OfType<TableSettingViewModel>().Single(item => item.Key == key);
}

static TableSettingViewModel CreateTableViewModel(string key)
{
    var metadata = ConfigMetadata.LoadDefault();
    var dataset = ConfigSourceLoader.LoadSnapshot();
    var definition = metadata.TablesByKey[key];
    var tableValue = dataset.ValueFor(key) as LuaTableValue
        ?? throw new InvalidOperationException($"Snapshot table '{key}' was not found.");
    return new TableSettingViewModel(definition, tableValue, dataset.DefaultHintFor);
}

static string RealRepoOverridePath()
{
    return Path.GetFullPath(Path.Combine(FindToolRoot(), "..", "..", "mods", "Ember", "src", "User_Config_Overrides.lua"));
}

static string ManagedOverrideSeed()
{
    return string.Join(Environment.NewLine, new[]
    {
        "-- manual before",
        "Manual_Before = true",
        "",
        LuaOverrideWriter.BeginMarker,
        "-- schema_version = 1",
        "LOG_LEVEL = 2",
        "GroundRelative_X = nil",
        "GliderConfig.T1.yawAngleSpeed = 88",
        "for _, row in ipairs(ExpandedGameSettings_Config or {}) do",
        "    if row.Name == \"playerMana\" then",
        "        row.Max = 1500",
        "    end",
        "end",
        "BlockMaterialReplacements = {",
        "    { enabled = true, targetGuid = \"bea90ffd-00fb-4737-9d57-f87d7da49ca8\", materialIndex = 183 },",
        "}",
        LuaOverrideWriter.EndMarker,
        "",
        "-- manual after",
        "Manual_After = true",
        ""
    });
}

static int CountOccurrences(string text, string value)
{
    var count = 0;
    var index = 0;
    while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
    {
        count++;
        index += value.Length;
    }

    return count;
}

static void RunSta(Action action)
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure is not null)
    {
        throw new InvalidOperationException("STA WPF smoke failed.", failure);
    }
}

static Rect TransformedRect(FrameworkElement element, Visual ancestor)
{
    return element.TransformToAncestor(ancestor).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
}

static T? FindVisualDescendant<T>(DependencyObject root)
    where T : DependencyObject
{
    if (root is not Visual && root is not System.Windows.Media.Media3D.Visual3D)
    {
        return null;
    }

    for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
    {
        var child = VisualTreeHelper.GetChild(root, i);
        if (child is T match)
        {
            return match;
        }

        var nested = FindVisualDescendant<T>(child);
        if (nested is not null)
        {
            return nested;
        }
    }

    return null;
}

static void ApplyTemplates(DependencyObject root)
{
    if (root is FrameworkElement element)
    {
        element.ApplyTemplate();
    }

    if (root is not Visual && root is not System.Windows.Media.Media3D.Visual3D)
    {
        return;
    }

    for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
    {
        ApplyTemplates(VisualTreeHelper.GetChild(root, i));
    }
}

static T? FindDescendant<T>(DependencyObject root)
    where T : DependencyObject
{
    var visual = FindVisualDescendant<T>(root);
    if (visual is not null)
    {
        return visual;
    }

    foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
    {
        if (child is T match)
        {
            return match;
        }

        var nested = FindDescendant<T>(child);
        if (nested is not null)
        {
            return nested;
        }
    }

    return null;
}

static string FindToolRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Ember_Config_Tool.sln")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Could not find tool root.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

sealed class TempModFixture : IDisposable
{
    public TempModFixture(string root, string modFolder)
    {
        Root = root;
        ModFolder = modFolder;
    }

    public string Root { get; }
    public string ModFolder { get; }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
