using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using Ember_Config_Tool.Models;
using Ember_Config_Tool.Services;

namespace Ember_Config_Tool.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const string MissingFolderStatus = "Ember config files were not found; using embedded defaults. Save is disabled.";

    private readonly AppOptions _options;
    private ConfigMetadata _metadata = new();
    private ConfigDisplayNames _displayNames = new(new ConfigMetadata());
    private ConfigDataset _dataset = ConfigSourceLoader.LoadSnapshot();
    private OverrideLoadResult _overrideLoadResult = OverrideLoadResult.Empty;
    private ToolDiagnostic? _targetDiagnostic;
    private string _modFolder = "";
    private string _statusText = "";
    private string _developerWarning = "";
    private string _presetName = "My Ember Preset";
    private ConfigGroupViewModel? _selectedGroup;
    private PresetSummary? _selectedPreset;
    private bool _isValidTarget;
    private string _fingerprint = "";

    public MainViewModel(AppOptions options)
    {
        _options = options;
        ReloadCommand = new RelayCommand(_ => Reload());
        SaveCommand = new RelayCommand(_ => Save(), _ => CanSave);
        ResetCommand = new RelayCommand(_ => ResetEditor());
        SavePresetCommand = new RelayCommand(_ => SavePreset());
        LoadPresetCommand = new RelayCommand(_ => LoadPreset(), _ => SelectedPreset is not null);

        LoadInitial();
    }

    public ObservableCollection<ConfigGroupViewModel> Groups { get; } = [];
    public ObservableCollection<string> ValidationIssues { get; } = [];
    public ObservableCollection<string> BlockingIssues { get; } = [];
    public ObservableCollection<string> WarningIssues { get; } = [];
    public ObservableCollection<ToolDiagnostic> BlockingDiagnostics { get; } = [];
    public ObservableCollection<ToolDiagnostic> WarningDiagnostics { get; } = [];
    public ObservableCollection<PresetSummary> Presets { get; } = [];

    public ICommand ReloadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand SavePresetCommand { get; }
    public ICommand LoadPresetCommand { get; }

    public string ModFolder
    {
        get => _modFolder;
        set
        {
            if (SetProperty(ref _modFolder, value))
            {
                OnPropertyChanged(nameof(CurrentPath));
                OnPropertyChanged(nameof(OverridePath));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string CurrentPath => string.IsNullOrWhiteSpace(ModFolder) ? _dataset.SourceName : ModFolder;

    public string OverridePath => string.IsNullOrWhiteSpace(ModFolder) ? "" : ConfigLocator.OverridesPath(ModFolder);

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string DeveloperWarning
    {
        get => _developerWarning;
        set
        {
            if (SetProperty(ref _developerWarning, value))
            {
                OnPropertyChanged(nameof(HasDeveloperWarning));
            }
        }
    }

    public bool HasDeveloperWarning => !string.IsNullOrWhiteSpace(DeveloperWarning);

    public string PresetName
    {
        get => _presetName;
        set => SetProperty(ref _presetName, value);
    }

    public ConfigGroupViewModel? SelectedGroup
    {
        get => _selectedGroup;
        set => SetProperty(ref _selectedGroup, value);
    }

    public PresetSummary? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool CanSave => _isValidTarget && BlockingIssues.Count == 0;

    private void LoadInitial()
    {
        _metadata = ConfigMetadata.LoadDefault();
        _displayNames = new ConfigDisplayNames(_metadata);
        RefreshPresetList();

        if (!string.IsNullOrWhiteSpace(_options.EmberRepoRoot))
        {
            LoadResolved(ConfigLocator.ResolveSelectedFolder(_options.EmberRepoRoot));
        }
        else if (!string.IsNullOrWhiteSpace(_options.ModFolder))
        {
            LoadResolved(ConfigLocator.ResolveSelectedFolder(_options.ModFolder));
        }
        else if (ConfigLocator.TryAutoDetect(out var detected))
        {
            LoadResolved(detected);
        }
        else
        {
            LoadMissingFolderSnapshot();
        }
    }

    private void LoadResolved(ResolvedEmberFolder resolved)
    {
        if (!resolved.IsValid)
        {
            LoadSnapshot(resolved.Warning, MissingFolderDiagnostic(resolved.Warning, resolved.ModFolder));
            return;
        }

        _targetDiagnostic = null;
        ModFolder = resolved.ModFolder;
        DeveloperWarning = resolved.Warning;
        _isValidTarget = true;
        _dataset = ConfigSourceLoader.LoadFromModFolder(ModFolder);
        _overrideLoadResult = OverrideStateLoader.LoadFromFile(ConfigLocator.OverridesPath(ModFolder), _metadata);
        RebuildEditor();
        StatusText = $"Loaded Ember config from {ModFolder}";
        OnPropertyChanged(nameof(CanSave));
        CommandManager.InvalidateRequerySuggested();
    }

    private void LoadSnapshot(string status, ToolDiagnostic? targetDiagnostic = null)
    {
        ModFolder = "";
        DeveloperWarning = targetDiagnostic?.DisplayMessage ?? "";
        _isValidTarget = false;
        _targetDiagnostic = targetDiagnostic;
        _dataset = ConfigSourceLoader.LoadSnapshot();
        _overrideLoadResult = OverrideLoadResult.Empty;
        RebuildEditor();
        StatusText = status;
        OnPropertyChanged(nameof(CanSave));
        CommandManager.InvalidateRequerySuggested();
    }

    private void LoadMissingFolderSnapshot()
    {
        const string message = "Expected to find an Ember mod folder containing src\\mod.lua and src\\Config. Launch the tool from the Ember folder, or pass --mod-folder/--ember-repo-root when running from the CLI.";
        LoadSnapshot(MissingFolderStatus, MissingFolderDiagnostic(message, "Ember folder"));
    }

    private static ToolDiagnostic MissingFolderDiagnostic(string message, string path)
    {
        return new ToolDiagnostic(DiagnosticSeverity.Blocking, path, message);
    }

    private void RebuildEditor()
    {
        foreach (var group in Groups)
        {
            foreach (var item in group.Items)
            {
                item.DeepChanged -= OnItemChanged;
            }
        }

        Groups.Clear();
        ValidationIssues.Clear();
        BlockingIssues.Clear();
        WarningIssues.Clear();
        BlockingDiagnostics.Clear();
        WarningDiagnostics.Clear();

        var metadataIssues = MetadataValidator.Validate(_dataset, _metadata);
        foreach (var issue in metadataIssues)
        {
            ValidationIssues.Add(issue);
        }

        _fingerprint = ConfigFingerprint.Compute(_dataset, _metadata);
        var scalarByKey = new Dictionary<string, SettingViewModel>(StringComparer.Ordinal);
        var tableByKey = new Dictionary<string, TableSettingViewModel>(StringComparer.Ordinal);

        foreach (var groupDefinition in _metadata.Groups)
        {
            var group = new ConfigGroupViewModel(groupDefinition);

            foreach (var setting in _metadata.PublicSettings.Where(setting => setting.Group.Equals(group.Id, StringComparison.Ordinal)))
            {
                _dataset.AssignmentsByKey.TryGetValue(setting.Key, out var assignment);
                var value = assignment?.Value ?? LuaNilValue.Instance;
                var item = new SettingViewModel(setting, value, assignment is null ? null : _dataset.DefaultHintFor(assignment.Location));
                item.DeepChanged += OnItemChanged;
                item.SetMasterStateResolver(key => ResolveMasterToggle(key, scalarByKey));
                item.SetDisplayNameResolver(key => _displayNames.LabelForKey(key));
                scalarByKey[setting.Key] = item;
                group.Items.Add(item);
            }

            foreach (var table in _metadata.PublicTables.Where(table => table.Group.Equals(group.Id, StringComparison.Ordinal)))
            {
                if (_dataset.ValueFor(table.Key) is not LuaTableValue tableValue)
                {
                    continue;
                }

                var item = new TableSettingViewModel(table, tableValue, _dataset.DefaultHintFor);
                item.DeepChanged += OnItemChanged;
                item.SetMasterStateResolver(key => ResolveMasterToggle(key, scalarByKey));
                item.SetDisplayNameResolver(key => _displayNames.LabelForKey(key));
                tableByKey[table.Key] = item;
                group.Items.Add(item);
            }

            if (group.Items.Count > 0)
            {
                group.RebuildSections();
                Groups.Add(group);
            }
        }

        ApplyLoadedOverrides(scalarByKey, tableByKey);

        SelectedGroup = Groups.FirstOrDefault();
        RefreshDependencies();
        RefreshValidation();
        OnPropertyChanged(nameof(CurrentPath));
        OnPropertyChanged(nameof(OverridePath));
    }

    private static bool ResolveMasterToggle(string key, Dictionary<string, SettingViewModel> scalarByKey)
    {
        return scalarByKey.TryGetValue(key, out var setting) && setting.EffectiveBool;
    }

    private void ApplyLoadedOverrides(
        IReadOnlyDictionary<string, SettingViewModel> scalarByKey,
        IReadOnlyDictionary<string, TableSettingViewModel> tableByKey)
    {
        foreach (var scalar in _overrideLoadResult.Document.Scalars)
        {
            if (scalarByKey.TryGetValue(scalar.Definition.Key, out var setting))
            {
                setting.ApplyOverride(scalar.State, scalar.Value);
            }
        }

        foreach (var table in _overrideLoadResult.Document.Tables)
        {
            if (tableByKey.TryGetValue(table.Definition.Key, out var tableViewModel))
            {
                tableViewModel.ApplyOverride(table);
            }
        }
    }

    private void AddDiagnostic(ToolDiagnostic diagnostic)
    {
        var displayDiagnostic = _displayNames.ToDisplayDiagnostic(diagnostic);
        var text = displayDiagnostic.ToString();
        ValidationIssues.Add(text);
        if (displayDiagnostic.IsBlocking)
        {
            BlockingIssues.Add(text);
            BlockingDiagnostics.Add(displayDiagnostic);
        }
        else
        {
            WarningIssues.Add(text);
            WarningDiagnostics.Add(displayDiagnostic);
        }
    }

    private void OnItemChanged(object? sender, EventArgs e)
    {
        RefreshDependencies();
        RefreshValidation();
        StatusText = "Unsaved changes.";
        CommandManager.InvalidateRequerySuggested();
    }

    private void RefreshDependencies()
    {
        foreach (var item in AllItems())
        {
            item.RefreshDependency();
        }
    }

    private void RefreshValidation()
    {
        var existingMetadataIssues = MetadataValidator.Validate(_dataset, _metadata);
        ValidationIssues.Clear();
        BlockingIssues.Clear();
        WarningIssues.Clear();
        BlockingDiagnostics.Clear();
        WarningDiagnostics.Clear();
        foreach (var issue in existingMetadataIssues)
        {
            AddDiagnostic(new ToolDiagnostic(DiagnosticSeverity.Blocking, "", issue));
        }

        if (_targetDiagnostic is not null)
        {
            AddDiagnostic(_targetDiagnostic);
        }

        foreach (var diagnostic in _overrideLoadResult.Diagnostics)
        {
            AddDiagnostic(diagnostic);
        }

        foreach (var item in AllItems())
        {
            foreach (var issue in item.Validate())
            {
                AddDiagnostic(new ToolDiagnostic(DiagnosticSeverity.Blocking, item.Key, issue));
            }

            if (item.ShowInactiveDependencyWarning)
            {
                AddDiagnostic(new ToolDiagnostic(DiagnosticSeverity.Warning, item.Key, item.Key, item.DependencyText, item.DependencyTechnicalText));
            }
        }

        OnPropertyChanged(nameof(CanSave));
    }

    private IEnumerable<ConfigItemViewModel> AllItems()
    {
        return Groups.SelectMany(group => group.Items);
    }

    private void Reload()
    {
        if (!string.IsNullOrWhiteSpace(ModFolder))
        {
            LoadResolved(ConfigLocator.ResolveSelectedFolder(ModFolder));
        }
        else
        {
            LoadMissingFolderSnapshot();
        }
    }

    private void ResetEditor()
    {
        foreach (var item in AllItems())
        {
            item.ResetToUnset();
        }

        RefreshDependencies();
        RefreshValidation();
        StatusText = "Editor reset to defaults. Save to write explicit overrides.";
    }

    private void Save()
    {
        RefreshValidation();
        if (BlockingIssues.Count > 0)
        {
            StatusText = "Save blocked by validation.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ModFolder) || !ConfigLocator.IsValidEmberModFolder(ModFolder))
        {
            StatusText = "Save blocked; Ember config files were not found.";
            return;
        }

        try
        {
            var document = BuildOverrideDocument();
            var result = LuaOverrideWriter.SaveAtomic(ConfigLocator.OverridesPath(ModFolder), document, _overrideLoadResult.ImportedManualPaths);
            _overrideLoadResult = PreserveHiddenLoadedOverrides(document);
            ValidationIssues.Clear();
            BlockingIssues.Clear();
            WarningIssues.Clear();
            BlockingDiagnostics.Clear();
            WarningDiagnostics.Clear();
            foreach (var conflict in result.Conflicts)
            {
                AddDiagnostic(new ToolDiagnostic(DiagnosticSeverity.Warning, conflict.Path, conflict.Message));
            }

            StatusText = result.Conflicts.Count == 0
                ? $"Saved {ConfigLocator.OverridesPath(ModFolder)}"
                : $"Saved with {result.Conflicts.Count} manual conflict warning(s).";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ValidationIssues.Clear();
            BlockingIssues.Clear();
            WarningIssues.Clear();
            BlockingDiagnostics.Clear();
            WarningDiagnostics.Clear();
            AddDiagnostic(new ToolDiagnostic(DiagnosticSeverity.Blocking, OverridePath, ex.Message));
            StatusText = "Save failed.";
        }
    }

    private OverrideDocument BuildOverrideDocument()
    {
        var document = new OverrideDocument();
        var activeMasterKeys = ActiveMasterKeys();
        foreach (var setting in AllItems().OfType<SettingViewModel>())
        {
            document.Scalars.Add(setting.ToOverride(ShouldWriteImplicitMasterValue(setting, activeMasterKeys)));
        }

        foreach (var table in AllItems().OfType<TableSettingViewModel>())
        {
            document.Tables.Add(table.ToOverride());
        }

        AppendHiddenLoadedOverrides(document);
        return document;
    }

    private HashSet<string> ActiveMasterKeys()
    {
        return AllItems()
            .OfType<SettingViewModel>()
            .Where(setting => setting.IsBoolean && setting.EffectiveBool)
            .Select(setting => setting.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool ShouldWriteImplicitMasterValue(SettingViewModel setting, IReadOnlySet<string> activeMasterKeys)
    {
        return setting.State == ConfigValueState.Unset &&
               setting.AllowsImplicitMasterOverride &&
               setting.MasterToggle is not null &&
               activeMasterKeys.Contains(setting.MasterToggle);
    }

    private void AppendHiddenLoadedOverrides(OverrideDocument document)
    {
        var scalarKeys = document.Scalars.Select(scalar => scalar.Definition.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var scalar in _overrideLoadResult.Document.Scalars.Where(scalar => scalar.Definition.UiHidden))
        {
            if (scalarKeys.Add(scalar.Definition.Key))
            {
                document.Scalars.Add(scalar);
            }
        }

        var tableKeys = document.Tables.Select(table => table.Definition.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var table in _overrideLoadResult.Document.Tables.Where(table => table.Definition.UiHidden))
        {
            if (tableKeys.Add(table.Definition.Key))
            {
                document.Tables.Add(table);
            }
        }
    }

    private static OverrideLoadResult PreserveHiddenLoadedOverrides(OverrideDocument source)
    {
        var hidden = new OverrideDocument();
        hidden.Scalars.AddRange(source.Scalars.Where(scalar => scalar.Definition.UiHidden));
        hidden.Tables.AddRange(source.Tables.Where(table => table.Definition.UiHidden));
        return new OverrideLoadResult { Document = hidden };
    }

    private void SavePreset()
    {
        var preset = CapturePreset(string.IsNullOrWhiteSpace(PresetName) ? "Ember Preset" : PresetName);
        var path = PresetStore.Save(preset);
        RefreshPresetList();
        SelectedPreset = Presets.FirstOrDefault(item => item.Path.Equals(path, StringComparison.Ordinal));
        StatusText = $"Saved preset {path}";
    }

    private void LoadPreset()
    {
        if (SelectedPreset is null)
        {
            return;
        }

        try
        {
            ApplyPreset(PresetStore.Load(SelectedPreset.Path));
            StatusText = $"Loaded preset {SelectedPreset.DisplayName}. Save to write overrides.";
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            AddDiagnostic(new ToolDiagnostic(DiagnosticSeverity.Blocking, "Preset", ex.Message));
            StatusText = "Preset load failed.";
        }
    }

    private ConfigPreset CapturePreset(string displayName)
    {
        var values = new Dictionary<string, PresetSettingValue>(StringComparer.Ordinal);
        var activeMasterKeys = ActiveMasterKeys();
        foreach (var item in AllItems())
        {
            if (item is SettingViewModel setting)
            {
                setting.CapturePreset(values, ShouldWriteImplicitMasterValue(setting, activeMasterKeys));
            }
            else
            {
                item.CapturePreset(values);
            }
        }

        return new ConfigPreset
        {
            DisplayName = displayName,
            ConfigFingerprint = _fingerprint,
            Settings = values
        };
    }

    public void ApplyPreset(ConfigPreset preset)
    {
        var warnings = new List<string>();
        if (!preset.ConfigFingerprint.Equals(_fingerprint, StringComparison.Ordinal))
        {
            warnings.Add("Preset fingerprint differs from the loaded config schema.");
        }

        var known = BuildKnownPresetKeys();
        foreach (var pair in preset.Settings)
        {
            if (!known.Contains(pair.Key) && !CanApplyWholeTablePresetAddition(pair.Key))
            {
                if (_metadata.Settings.Any(setting => setting.Key.Equals(pair.Key, StringComparison.Ordinal) && setting.UiHidden) ||
                    _metadata.Tables.Any(table => table.Key.Equals(pair.Key, StringComparison.Ordinal) && table.UiHidden) ||
                    TryGetPresetTableColumn(pair.Key, out var hiddenTable, out _) && hiddenTable.UiHidden)
                {
                    warnings.Add($"Ignored hidden preset key: {_displayNames.DisplayPresetPath(pair.Key)}");
                }
                else if (_metadata.Settings.Any(setting => setting.Key.Equals(pair.Key, StringComparison.Ordinal) && !setting.IsPublic))
                {
                    warnings.Add($"Refused non-public preset key: {_displayNames.DisplayPresetPath(pair.Key)}");
                }
                else if (TryGetPresetTableColumn(pair.Key, out _, out var column) && column.ReadOnly)
                {
                    warnings.Add($"Ignored read-only preset key: {_displayNames.DisplayPresetPath(pair.Key)}");
                }
                else if (TryGetPresetTableColumn(pair.Key, out var table, out _) && !table.IsPublic)
                {
                    warnings.Add($"Refused non-public preset table key: {_displayNames.DisplayPresetPath(pair.Key)}");
                }
                else
                {
                    warnings.Add($"Ignored unknown preset key: {_displayNames.DisplayPresetPath(pair.Key)}");
                }

                continue;
            }

            foreach (var item in AllItems())
            {
                item.ApplyPreset(pair.Key, pair.Value, warnings);
            }
        }

        RefreshDependencies();
        RefreshValidation();
        foreach (var warning in warnings)
        {
            AddDiagnostic(new ToolDiagnostic(DiagnosticSeverity.Warning, "", warning));
        }
    }

    private HashSet<string> BuildKnownPresetKeys()
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var setting in AllItems().OfType<SettingViewModel>())
        {
            known.Add(setting.Key);
        }

        foreach (var table in AllItems().OfType<TableSettingViewModel>())
        {
            known.Add(table.Key);
            foreach (var row in table.Rows)
            {
                foreach (var cell in row.Cells.Where(cell => !cell.IsReadOnly))
                {
                    known.Add($"{table.Key}[{row.Identity}].{cell.Column.Key}");
                }
            }
        }

        return known;
    }

    private bool CanApplyWholeTablePresetAddition(string path)
    {
        if (!TryGetPresetTableColumn(path, out var table, out _))
        {
            return false;
        }

        return table.IsWholeTable && table.IsPublic && !table.UiHidden;
    }

    private bool TryGetPresetTableColumn(string path, out TableDefinition table, out TableColumnDefinition column)
    {
        table = new TableDefinition();
        column = new TableColumnDefinition();
        var bracket = path.IndexOf('[', StringComparison.Ordinal);
        var separator = path.IndexOf("].", StringComparison.Ordinal);
        if (bracket <= 0 || separator < bracket)
        {
            return false;
        }

        var tableKey = path[..bracket];
        var columnKey = path[(separator + 2)..];
        if (!_metadata.TablesByKey.TryGetValue(tableKey, out var foundTable))
        {
            return false;
        }

        var foundColumn = foundTable.Columns.FirstOrDefault(item => item.Key.Equals(columnKey, StringComparison.Ordinal));
        if (foundColumn is null)
        {
            return false;
        }

        table = foundTable;
        column = foundColumn;
        return true;
    }

    private void RefreshPresetList()
    {
        Presets.Clear();
        foreach (var preset in PresetStore.ListPresets())
        {
            Presets.Add(preset);
        }

        SelectedPreset = Presets.FirstOrDefault();
    }
}
