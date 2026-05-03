using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Ember_Config_Tool.Models;
using Ember_Config_Tool.Services;

namespace Ember_Config_Tool.ViewModels;

public sealed class ConfigGroupViewModel : ObservableObject
{
    private static readonly FishingTierDefinition[] FishingTiers =
    [
        new(1, "Common Fish"),
        new(2, "Uncommon Fish"),
        new(3, "Rare Fish"),
        new(4, "Epic Fish"),
        new(5, "Legendary Fish")
    ];

    private static readonly FishingTierFieldDefinition[] FishingTierFields =
    [
        new("AdvancedGame", "Advanced Game"),
        new("ReduceRoundsBy", "Reduce Rounds By"),
        new("QuickTimeEvent", "Quick Time Event"),
        new("RodStrength", "Rod Strength"),
        new("RodEndurance", "Rod Endurance")
    ];

    public ConfigGroupViewModel(GroupDefinition definition)
    {
        Id = definition.Id;
        Label = definition.Label;
        IconGlyph = IconFor(definition.Id);
    }

    public string Id { get; }
    public string Label { get; }
    public string IconGlyph { get; }
    public ObservableCollection<ConfigItemViewModel> Items { get; } = [];
    public ObservableCollection<ConfigSectionViewModel> Sections { get; } = [];

    public void RebuildSections()
    {
        Sections.Clear();
        var assigned = new HashSet<ConfigItemViewModel>();

        foreach (var item in Items)
        {
            item.ShowInlineLabel = true;
            item.SetDisplayLabelOverride(null);
        }

        if (Id.Equals("fishing", StringComparison.Ordinal))
        {
            RebuildFishingSections(assigned);
            return;
        }

        foreach (var item in Items)
        {
            if (assigned.Contains(item))
            {
                continue;
            }

            if (item is SettingViewModel setting && setting.IsBoolean)
            {
                var section = new ConfigSectionViewModel(setting.Label);
                setting.ShowInlineLabel = false;
                section.Items.Add(setting);
                assigned.Add(setting);

                foreach (var dependent in Items.Where(candidate => candidate.MasterToggle?.Equals(setting.Key, StringComparison.Ordinal) == true))
                {
                    dependent.ShowInlineLabel = dependent is not TableSettingViewModel;
                    section.Items.Add(dependent);
                    assigned.Add(dependent);
                }

                Sections.Add(section);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(item.MasterToggle) &&
                Items.OfType<SettingViewModel>().Any(candidate => candidate.Key.Equals(item.MasterToggle, StringComparison.Ordinal) && candidate.IsBoolean))
            {
                continue;
            }

            item.ShowInlineLabel = false;
            var single = new ConfigSectionViewModel(item.Label);
            single.Items.Add(item);
            assigned.Add(item);
            Sections.Add(single);
        }
    }

    private void RebuildFishingSections(HashSet<ConfigItemViewModel> assigned)
    {
        var master = Items.OfType<SettingViewModel>().FirstOrDefault(item => item.Key.Equals("Enable_Fishing_Tweaks", StringComparison.Ordinal));
        if (master is not null)
        {
            master.ShowInlineLabel = false;
            var masterSection = new ConfigSectionViewModel(master.Label);
            masterSection.Items.Add(master);
            assigned.Add(master);
            Sections.Add(masterSection);
        }

        foreach (var tier in FishingTiers)
        {
            var section = new ConfigSectionViewModel(tier.HeaderLabel, $"Tier {tier.Number}");

            foreach (var field in FishingTierFields)
            {
                var key = $"Tier{tier.Number}_{field.KeySuffix}";
                var item = Items.FirstOrDefault(candidate => candidate.Key.Equals(key, StringComparison.Ordinal));
                if (item is null)
                {
                    continue;
                }

                item.ShowInlineLabel = true;
                item.SetDisplayLabelOverride(field.Label);
                section.Items.Add(item);
                assigned.Add(item);
            }

            if (section.Items.Count > 0)
            {
                Sections.Add(section);
            }
        }

        foreach (var item in Items.Where(item => !assigned.Contains(item)))
        {
            item.ShowInlineLabel = false;
            var section = new ConfigSectionViewModel(item.Label);
            section.Items.Add(item);
            assigned.Add(item);
            Sections.Add(section);
        }
    }

    private static string IconFor(string groupId)
    {
        return groupId switch
        {
            "misc" => "\uE9D2",
            "storage" => "\uE7B8",
            "exploration" => "\uE819",
            "progression" => "\uE74A",
            "spells" => "\uE945",
            "buffs" => "\uE83D",
            "shroud" => "\uE9CE",
            "building" => "\uE80F",
            "map" => "\uE707",
            "glider" => "\uE734",
            "terraforming" => "\uE80F",
            "blueprint" => "\uE8A5",
            "expanded" => "\uE9D2",
            "skills" => "\uE8FD",
            "gems" => "\uE735",
            "crafting" => "\uE90F",
            "fishing" => "\uE81E",
            "loot" => "\uE7C3",
            _ => "\uE10F"
        };
    }
}

public sealed class ConfigSectionViewModel
{
    public ConfigSectionViewModel(string label, string? badgeLabel = null)
    {
        Label = label;
        BadgeLabel = badgeLabel ?? "";
    }

    public string Label { get; }
    public string DisplayLabel => Label.ToUpperInvariant();
    public string BadgeLabel { get; }
    public bool HasBadge => !string.IsNullOrWhiteSpace(BadgeLabel);
    public string HeaderText => HasBadge ? Label : DisplayLabel;
    public ObservableCollection<ConfigItemViewModel> Items { get; } = [];
}

public abstract class ConfigItemViewModel : ObservableObject
{
    private Func<string, bool>? _masterStateResolver;
    private Func<string, string>? _displayNameResolver;
    private string? _displayLabelOverride;
    private bool _isDependencyActive = true;
    private bool _showInlineLabel = true;

    protected ConfigItemViewModel(string key, string label, string group, string? masterToggle)
    {
        Key = key;
        Label = label;
        Group = group;
        MasterToggle = masterToggle;
    }

    public string Key { get; }
    public string Label { get; }
    public string DisplayLabel => string.IsNullOrWhiteSpace(_displayLabelOverride) ? Label : _displayLabelOverride;
    public string Group { get; }
    public string? MasterToggle { get; }
    public abstract bool IsScalar { get; }
    public abstract bool IsTable { get; }
    public abstract bool HasExplicitOverride { get; }

    public bool ShowInlineLabel
    {
        get => _showInlineLabel;
        set
        {
            if (SetProperty(ref _showInlineLabel, value))
            {
                OnPropertyChanged(nameof(InlineLabelOrEnabledText));
            }
        }
    }

    public string InlineLabelOrEnabledText => ShowInlineLabel ? DisplayLabel : "Enabled";
    public bool HasDependency => !string.IsNullOrWhiteSpace(MasterToggle);

    public void SetDisplayLabelOverride(string? label)
    {
        var normalized = string.IsNullOrWhiteSpace(label) ? null : label;
        if (_displayLabelOverride == normalized)
        {
            return;
        }

        _displayLabelOverride = normalized;
        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(InlineLabelOrEnabledText));
    }

    public bool IsDependencyActive
    {
        get => _isDependencyActive;
        private set
        {
            if (SetProperty(ref _isDependencyActive, value))
            {
                OnPropertyChanged(nameof(ShowInactiveDependencyWarning));
                OnPropertyChanged(nameof(DependencyText));
                OnPropertyChanged(nameof(DependencyTechnicalText));
            }
        }
    }

    public bool ShowInactiveDependencyWarning => HasDependency && !IsDependencyActive && HasExplicitOverride;

    public string DependencyText => ShowInactiveDependencyWarning
        ? $"Inactive until {DisplayNameFor(MasterToggle)} is enabled."
        : "";

    public string DependencyTechnicalText => ShowInactiveDependencyWarning
        ? $"Inactive until {MasterToggle} is enabled."
        : "";

    public event EventHandler? DeepChanged;

    public void SetMasterStateResolver(Func<string, bool> resolver)
    {
        _masterStateResolver = resolver;
        RefreshDependency();
    }

    public void SetDisplayNameResolver(Func<string, string> resolver)
    {
        _displayNameResolver = resolver;
        OnPropertyChanged(nameof(DependencyText));
        OnPropertyChanged(nameof(DependencyTechnicalText));
    }

    public void RefreshDependency()
    {
        IsDependencyActive = string.IsNullOrWhiteSpace(MasterToggle) || (_masterStateResolver?.Invoke(MasterToggle) ?? false);
    }

    protected void RaiseDeepChanged()
    {
        RefreshDependency();
        OnPropertyChanged(nameof(HasExplicitOverride));
        OnPropertyChanged(nameof(ShowInactiveDependencyWarning));
        OnPropertyChanged(nameof(DependencyText));
        OnPropertyChanged(nameof(DependencyTechnicalText));
        DeepChanged?.Invoke(this, EventArgs.Empty);
    }

    public abstract IEnumerable<string> Validate();
    public abstract void ResetToUnset();
    public abstract void CapturePreset(Dictionary<string, PresetSettingValue> values);
    public abstract void ApplyPreset(string path, PresetSettingValue value, List<string> warnings);

    private string DisplayNameFor(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "";
        }

        return _displayNameResolver?.Invoke(key) ?? key;
    }
}

public sealed record FishingTierDefinition(int Number, string HeaderLabel);

public sealed record FishingTierFieldDefinition(string KeySuffix, string Label);

public sealed record ConfigValueStateOption(ConfigValueState State, string Label, string TechnicalName)
{
    public override string ToString()
    {
        return Label;
    }
}

public static class ConfigValueStateOptions
{
    public static ConfigValueStateOption UseDefault { get; } = new(ConfigValueState.Unset, "Use Default", nameof(ConfigValueState.Unset));
    public static ConfigValueStateOption SetNone { get; } = new(ConfigValueState.Nil, "Set None", nameof(ConfigValueState.Nil));
    public static ConfigValueStateOption Custom { get; } = new(ConfigValueState.Value, "Custom", nameof(ConfigValueState.Value));
    public static IReadOnlyList<ConfigValueStateOption> ReadOnly { get; } = [UseDefault];

    public static IReadOnlyList<ConfigValueStateOption> ForNullable(bool nullable)
    {
        return nullable
            ? [UseDefault, SetNone, Custom]
            : [UseDefault, Custom];
    }
}

public sealed record TableRowDisplayIdentity(string DisplayText, string TechnicalIdentity, bool IsEditable);

public sealed class SettingViewModel : ConfigItemViewModel
{
    private ConfigValueState _state;
    private string _valueText = "";
    private bool _boolValue;
    private string? _selectedOption;
    private ValueOptionDefinition? _selectedValueOption;
    private bool _suppressValueOptionState;
    private bool _suppressStatePromotion;

    public SettingViewModel(SettingDefinition definition, LuaValue defaultValue, string? explicitDefaultHint = null)
        : base(definition.Key, definition.Label, definition.Group, definition.MasterToggle)
    {
        Definition = definition;
        DefaultValue = defaultValue;
        ExplicitDefaultHint = explicitDefaultHint;
        StateOptions = ConfigValueStateOptions.ForNullable(definition.Nullable);
        ApplyDefaultValue(defaultValue);
    }

    public SettingDefinition Definition { get; }
    public LuaValue DefaultValue { get; }
    public string? ExplicitDefaultHint { get; }
    public IReadOnlyList<ConfigValueStateOption> StateOptions { get; }
    public override bool IsScalar => true;
    public override bool IsTable => false;
    public bool IsBoolean => Definition.IsBoolean;
    public bool IsNumber => Definition.IsNumber;
    public bool HasValueOptions => Definition.ValueOptions.Count > 0;
    public bool HasStringOptions => Definition.Options.Count > 0 && !HasValueOptions;
    public bool IsNumberInput => IsNumber && !HasValueOptions;
    public bool HasBoundedRange => Definition.Min.HasValue && Definition.Max.HasValue;
    public bool UsesSliderEditor => IsNumberInput && HasBoundedRange;
    public bool UsesNumberTextEditor => IsNumberInput && !UsesSliderEditor;
    public bool IsText => !IsBoolean && !IsNumber && !HasStringOptions && !HasValueOptions;
    public bool IsEnum => HasStringOptions || HasValueOptions;
    public bool UsesOverrideStateSelector => Definition.Nullable;
    public bool AllowsImplicitMasterOverride => HasDependency && !IsBoolean && !UsesOverrideStateSelector;
    public bool ShowsDefaultHint => IsNonBooleanScalar && !HasValueOptions && !string.IsNullOrWhiteSpace(DefaultHint);
    public bool ShowsScalarMetaRow => UsesOverrideStateSelector || ShowsDefaultHint;
    public bool IsNonBooleanScalar => !IsBoolean;
    public bool IsValueEditorEnabled => State == ConfigValueState.Value;
    public bool IsScalarEditorEnabled => !UsesOverrideStateSelector || State == ConfigValueState.Value;
    public bool OverrideActive
    {
        get => State == ConfigValueState.Value;
        set
        {
            State = value ? ConfigValueState.Value : ConfigValueState.Unset;
        }
    }

    public bool IsInteger => Definition.IsInteger;
    public string? DefaultHint => ExplicitDefaultHint ?? Definition.VanillaDefault;
    public string DefaultText => $"Default = {DefaultHint}";
    public decimal NumberMinimum => Definition.Min ?? 0m;
    public decimal NumberMaximum => Definition.Max ?? NumberMinimum;
    public decimal NumberIncrement => Definition.Step ?? ConfigNumberPolicy.DefaultStep;
    public bool HasRange => Definition.Min.HasValue || Definition.Max.HasValue;
    public string RangeText => Definition.Min.HasValue && Definition.Max.HasValue
        ? $"Range {Definition.Min.Value:0.####} - {Definition.Max.Value:0.####}"
        : Definition.Min.HasValue
            ? $"Range >= {Definition.Min.Value:0.####}"
            : Definition.Max.HasValue
                ? $"Range <= {Definition.Max.Value:0.####}"
                : "";

    public override bool HasExplicitOverride => State != ConfigValueState.Unset;

    public ConfigValueState State
    {
        get => _state;
        set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsValueEditorEnabled));
                OnPropertyChanged(nameof(IsScalarEditorEnabled));
                OnPropertyChanged(nameof(OverrideActive));
                OnPropertyChanged(nameof(BooleanEnabled));
                RaiseDeepChanged();
            }
        }
    }

    public string ValueText
    {
        get => _valueText;
        set
        {
            if (SetProperty(ref _valueText, value))
            {
                PromoteStateFromDirectEdit();
                RaiseDeepChanged();
            }
        }
    }

    public bool BoolValue
    {
        get => _boolValue;
        set
        {
            if (SetProperty(ref _boolValue, value))
            {
                OnPropertyChanged(nameof(BooleanEnabled));
                RaiseDeepChanged();
            }
        }
    }

    public bool BooleanEnabled
    {
        get => State == ConfigValueState.Value && BoolValue;
        set
        {
            if (value)
            {
                if (!BoolValue)
                {
                    BoolValue = true;
                }

                if (State != ConfigValueState.Value)
                {
                    State = ConfigValueState.Value;
                }

                return;
            }

            if (BoolValue)
            {
                BoolValue = false;
            }

            State = ConfigValueState.Unset;
        }
    }

    public string? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (SetProperty(ref _selectedOption, value))
            {
                ValueText = value ?? "";
                PromoteStateFromDirectEdit();
                RaiseDeepChanged();
            }
        }
    }

    public ValueOptionDefinition? SelectedValueOption
    {
        get => _selectedValueOption;
        set
        {
            if (SetProperty(ref _selectedValueOption, value))
            {
                ValueText = value?.Value ?? "";
                if (!_suppressValueOptionState && value is not null && State != ConfigValueState.Value)
                {
                    State = ConfigValueState.Value;
                }

                RaiseDeepChanged();
            }
        }
    }

    public bool EffectiveBool
    {
        get
        {
            if (Definition.IsBoolean)
            {
                return State == ConfigValueState.Value && BoolValue;
            }

            return false;
        }
    }

    public ScalarOverride ToOverride()
    {
        return new ScalarOverride(Definition, State, State == ConfigValueState.Value ? CurrentLuaValue() : null);
    }

    public ScalarOverride ToOverride(bool forceValueWhenUnset)
    {
        var state = forceValueWhenUnset && State == ConfigValueState.Unset
            ? ConfigValueState.Value
            : State;
        return new ScalarOverride(Definition, state, state == ConfigValueState.Value ? CurrentLuaValue() : null);
    }

    public void ApplyOverride(ConfigValueState state, LuaValue? value)
    {
        if (state == ConfigValueState.Nil && !Definition.Nullable)
        {
            return;
        }

        if (state == ConfigValueState.Value && value is not null)
        {
            ApplyDefaultValue(value);
        }

        State = state;
    }

    public override IEnumerable<string> Validate()
    {
        if (State != ConfigValueState.Value)
        {
            yield break;
        }

        LuaValue? value = null;
        string? parseError = null;
        try
        {
            value = CurrentLuaValue();
        }
        catch (FormatException ex)
        {
            parseError = $"{Key}: {ex.Message}";
        }

        if (parseError is not null)
        {
            yield return parseError;
            yield break;
        }

        if (value is LuaNumberValue number)
        {
            if (Definition.Min.HasValue && number.Value < Definition.Min.Value)
            {
                yield return $"{Key}: value is below {Definition.Min.Value}.";
            }

            if (Definition.Max.HasValue && number.Value > Definition.Max.Value)
            {
                yield return $"{Key}: value is above {Definition.Max.Value}.";
            }
        }
    }

    public override void ResetToUnset()
    {
        State = ConfigValueState.Unset;
        ApplyDefaultValue(DefaultValue);
    }

    public override void CapturePreset(Dictionary<string, PresetSettingValue> values)
    {
        CapturePreset(values, false);
    }

    public void CapturePreset(Dictionary<string, PresetSettingValue> values, bool forceValueWhenUnset)
    {
        var state = forceValueWhenUnset && State == ConfigValueState.Unset
            ? ConfigValueState.Value
            : State;
        values[Key] = new PresetSettingValue
        {
            State = state,
            Value = state == ConfigValueState.Value ? PresetStore.ToJsonValue(CurrentLuaValue()) : null
        };
    }

    public override void ApplyPreset(string path, PresetSettingValue value, List<string> warnings)
    {
        if (!path.Equals(Key, StringComparison.Ordinal))
        {
            return;
        }

        if (!ValidatePresetState(value, Definition.Nullable, Key, warnings))
        {
            return;
        }

        if (value.State == ConfigValueState.Nil && !Definition.Nullable)
        {
            warnings.Add($"{Key}: preset requested nil for a non-nullable setting.");
            return;
        }

        State = value.State;
        if (value.State == ConfigValueState.Value)
        {
            var lua = PresetStore.FromJsonValue(value.Value, Definition.ValueType);
            if (lua is not null)
            {
                ApplyDefaultValue(lua);
            }
        }
    }

    private static bool ValidatePresetState(PresetSettingValue value, bool nullable, string path, List<string> warnings)
    {
        if (!Enum.IsDefined(value.State))
        {
            warnings.Add($"{path}: preset state is not valid.");
            return false;
        }

        if (value.State == ConfigValueState.Unset && value.Value is not null)
        {
            warnings.Add($"{path}: preset unset state must not include a value.");
            return false;
        }

        if (value.State == ConfigValueState.Nil && value.Value is not null)
        {
            warnings.Add($"{path}: preset nil state must not include a value.");
            return false;
        }

        if (value.State == ConfigValueState.Nil && !nullable)
        {
            warnings.Add($"{path}: preset requested nil for a non-nullable setting.");
            return false;
        }

        if (value.State == ConfigValueState.Value && value.Value is null)
        {
            warnings.Add($"{path}: preset value state is missing a value.");
            return false;
        }

        return true;
    }

    private void ApplyDefaultValue(LuaValue value)
    {
        _suppressStatePromotion = true;
        try
        {
        if (value is LuaBooleanValue boolean)
        {
            BoolValue = boolean.Value;
            ValueText = boolean.Value ? "true" : "false";
        }
        else if (value is LuaNumberValue number)
        {
            ValueText = LuaOverrideWriter.RenderValue(number, Definition.IsInteger);
            _suppressValueOptionState = true;
            try
            {
                SelectedValueOption = Definition.ValueOptions.FirstOrDefault(option => option.Value.Equals(ValueText, StringComparison.Ordinal))
                    ?? Definition.ValueOptions.FirstOrDefault();
            }
            finally
            {
                _suppressValueOptionState = false;
            }
        }
        else if (value is LuaStringValue text)
        {
            ValueText = text.Value;
            SelectedOption = Definition.Options.Contains(text.Value, StringComparer.Ordinal) ? text.Value : Definition.Options.FirstOrDefault();
        }
        else
        {
            ValueText = Definition.IsBoolean ? "false" : "0";
            BoolValue = false;
            SelectedOption = Definition.Options.FirstOrDefault();
        }
        }
        finally
        {
            _suppressStatePromotion = false;
        }
    }

    private void PromoteStateFromDirectEdit()
    {
        if (_suppressStatePromotion || UsesOverrideStateSelector || IsBoolean || HasValueOptions)
        {
            return;
        }

        if (State != ConfigValueState.Value)
        {
            State = ConfigValueState.Value;
        }
    }

    private LuaValue CurrentLuaValue()
    {
        if (Definition.IsBoolean)
        {
            return new LuaBooleanValue(BoolValue);
        }

        if (Definition.IsNumber)
        {
            if (!decimal.TryParse(ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                throw new FormatException("Enter a valid number.");
            }

            if (Definition.IsInteger)
            {
                number = decimal.Truncate(number);
            }

            return new LuaNumberValue(number, number.ToString(CultureInfo.InvariantCulture));
        }

        return new LuaStringValue(HasStringOptions ? (SelectedOption ?? ValueText) : ValueText);
    }
}

public sealed class TableSettingViewModel : ConfigItemViewModel
{
    private readonly List<string> _sourceIdentityOrder;
    private readonly Func<SourceLocation, string?> _defaultHintResolver;
    private bool _wholeTableEnabled;
    private bool _wholeTableConfirmed;
    private bool _listStructureChanged;

    public TableSettingViewModel(TableDefinition definition, LuaTableValue defaultValue, Func<SourceLocation, string?>? defaultHintResolver = null)
        : base(definition.Key, definition.Label, definition.Group, definition.MasterToggle)
    {
        Definition = definition;
        DefaultValue = defaultValue;
        _defaultHintResolver = defaultHintResolver ?? (_ => null);
        _sourceIdentityOrder = BuildRows(definition, defaultValue, this, _defaultHintResolver).Select(row => row.Identity).ToList();
        Rows = new ObservableCollection<TableRowViewModel>(BuildRows(definition, defaultValue, this, _defaultHintResolver));
        AttachRows();
        AddRowCommand = new RelayCommand(_ => AddRow(), _ => CanManageRows);
    }

    public TableDefinition Definition { get; }
    public LuaTableValue DefaultValue { get; }
    public ObservableCollection<TableRowViewModel> Rows { get; }
    public ICommand AddRowCommand { get; }
    public override bool IsScalar => false;
    public override bool IsTable => true;
    public bool IsWholeTable => Definition.IsWholeTable;
    public bool UsesCellState => !Definition.IsWholeTable;
    public string WholeTableWarning => Definition.WholeTableWarning ?? "";
    public bool CanManageRows => IsWholeTable && WholeTableEnabled;
    public bool RequiresWholeTableConfirmation => IsWholeTable && WholeTableEnabled && _listStructureChanged;
    public override bool HasExplicitOverride => WholeTableEnabled || Rows.Any(row => row.Cells.Any(cell => cell.State != ConfigValueState.Unset));

    public string? DefaultHintFor(SourceLocation location) => _defaultHintResolver(location);

    public bool WholeTableEnabled
    {
        get => _wholeTableEnabled;
        set
        {
            if (SetProperty(ref _wholeTableEnabled, value))
            {
                OnPropertyChanged(nameof(CanManageRows));
                OnPropertyChanged(nameof(RequiresWholeTableConfirmation));
                RefreshRowManagement();
                RaiseDeepChanged();
            }
        }
    }

    public bool WholeTableConfirmed
    {
        get => _wholeTableConfirmed;
        set
        {
            if (SetProperty(ref _wholeTableConfirmed, value))
            {
                RaiseDeepChanged();
            }
        }
    }

    public TableOverride ToOverride()
    {
        return new TableOverride(Definition, WholeTableEnabled, Rows.Select(row => row.ToOverride(Definition.IsWholeTable, Definition)).ToList());
    }

    public void ApplyOverride(TableOverride loaded)
    {
        if (Definition.IsWholeTable && loaded.WholeTableEnabled)
        {
            Rows.Clear();
            foreach (var row in loaded.Rows)
            {
                var rowTable = RowTableFromOverride(row);
                var kind = _sourceIdentityOrder.Contains(row.Identity, StringComparer.Ordinal)
                    ? TableRowKind.Source
                    : TableRowKind.Added;
                AddRowViewModel(new TableRowViewModel(this, row.Identity, Definition, rowTable, kind, _defaultHintResolver));
            }

            WholeTableEnabled = true;
            WholeTableConfirmed = false;
            _listStructureChanged = false;
            OnPropertyChanged(nameof(RequiresWholeTableConfirmation));
            return;
        }

        foreach (var rowOverride in loaded.Rows)
        {
            var row = Rows.FirstOrDefault(item => item.Identity.Equals(rowOverride.Identity, StringComparison.Ordinal));
            if (row is null)
            {
                continue;
            }

            row.ApplyOverride(rowOverride);
        }
    }

    public override IEnumerable<string> Validate()
    {
        foreach (var row in Rows)
        {
            foreach (var issue in row.Validate())
            {
                yield return $"{Key}[{row.Identity}].{issue}";
            }
        }

        if (!Definition.IsWholeTable || !WholeTableEnabled)
        {
            yield break;
        }

        if (RequiresWholeTableConfirmation && !WholeTableConfirmed)
        {
            yield return $"{Key}: confirm whole-table replacement after list row changes.";
        }

        foreach (var group in Rows.Where(row => !string.IsNullOrWhiteSpace(row.Identity)).GroupBy(row => row.Identity, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            yield return $"{Key}: duplicate row identity '{group.Key}'.";
        }

        foreach (var row in Rows.Where(row => !row.IsDisabledBlankPlaceholder))
        {
            if (!ValidateIdentity(row.Identity, out var issue))
            {
                yield return $"{Key}[{row.Identity}]: {issue}";
            }
        }
    }

    public override void ResetToUnset()
    {
        WholeTableEnabled = false;
        WholeTableConfirmed = false;
        _listStructureChanged = false;
        Rows.Clear();
        foreach (var row in BuildRows(Definition, DefaultValue, this, _defaultHintResolver))
        {
            AddRowViewModel(row);
        }

        OnPropertyChanged(nameof(RequiresWholeTableConfirmation));
        RaiseDeepChanged();
    }

    public override void CapturePreset(Dictionary<string, PresetSettingValue> values)
    {
        values[Key] = new PresetSettingValue
        {
            State = WholeTableEnabled ? ConfigValueState.Value : ConfigValueState.Unset
        };

        foreach (var row in Rows)
        {
            foreach (var cell in row.Cells.Where(cell => !cell.IsReadOnly))
            {
                var state = cell.EffectiveState;
                values[$"{Key}[{row.Identity}].{cell.Column.Key}"] = new PresetSettingValue
                {
                    State = state,
                    Value = state == ConfigValueState.Value ? PresetStore.ToJsonValue(cell.CurrentLuaValue()) : null
                };
            }
        }
    }

    public override void ApplyPreset(string path, PresetSettingValue value, List<string> warnings)
    {
        if (path.Equals(Key, StringComparison.Ordinal))
        {
            if (value.State == ConfigValueState.Value)
            {
                WholeTableEnabled = true;
            }
            else if (value.State == ConfigValueState.Unset)
            {
                WholeTableEnabled = false;
            }
            else
            {
                warnings.Add($"{path}: table preset state must be Unset or Value.");
            }

            return;
        }

        var prefix = Key + "[";
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
        {
            return;
        }

        var end = path.IndexOf("].", prefix.Length, StringComparison.Ordinal);
        if (end < 0)
        {
            warnings.Add($"Ignored malformed table preset key: {path}");
            return;
        }

        var identity = path[prefix.Length..end];
        var columnKey = path[(end + 2)..];
        var row = Rows.FirstOrDefault(item => item.Identity.Equals(identity, StringComparison.Ordinal));
        if (row is null && Definition.IsWholeTable && ValidateIdentity(identity, out _))
        {
            row = CreateEmptyRow(identity, TableRowKind.Added);
            AddRowViewModel(row);
            _listStructureChanged = true;
            OnPropertyChanged(nameof(RequiresWholeTableConfirmation));
        }

        var cell = row?.Cells.FirstOrDefault(item => item.Column.Key.Equals(columnKey, StringComparison.Ordinal));
        if (cell is null)
        {
            warnings.Add($"Ignored unknown table preset key: {path}");
            return;
        }

        cell.ApplyPreset(value, warnings, path);
        if (columnKey.Equals(Definition.RowIdentity, StringComparison.Ordinal) && value.State == ConfigValueState.Value)
        {
            row!.Identity = cell.ValueText;
        }
    }

    internal void RemoveRow(TableRowViewModel row)
    {
        if (!CanManageRows)
        {
            return;
        }

        Rows.Remove(row);
        MarkListStructureChanged();
    }

    internal void DuplicateRow(TableRowViewModel row)
    {
        if (!CanManageRows)
        {
            return;
        }

        var clone = row.CloneAsDuplicate();
        var index = Rows.IndexOf(row);
        Rows.Insert(index < 0 ? Rows.Count : index + 1, clone);
        AttachRow(clone);
        MarkListStructureChanged();
    }

    internal void MoveRow(TableRowViewModel row, int offset)
    {
        if (!CanManageRows)
        {
            return;
        }

        var index = Rows.IndexOf(row);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= Rows.Count)
        {
            return;
        }

        Rows.Move(index, target);
        MarkListStructureChanged();
    }

    internal void MarkIdentityChanged()
    {
        MarkListStructureChanged();
    }

    private void AddRow()
    {
        AddRowViewModel(CreateEmptyRow("", TableRowKind.Added));
        MarkListStructureChanged();
    }

    private void MarkListStructureChanged()
    {
        _listStructureChanged = true;
        WholeTableConfirmed = false;
        OnPropertyChanged(nameof(RequiresWholeTableConfirmation));
        RaiseDeepChanged();
    }

    private void RefreshRowManagement()
    {
        foreach (var row in Rows)
        {
            row.RefreshCanManageRows();
        }
    }

    private void AttachRows()
    {
        foreach (var row in Rows)
        {
            AttachRow(row);
        }
    }

    private void AddRowViewModel(TableRowViewModel row)
    {
        Rows.Add(row);
        AttachRow(row);
    }

    private void AttachRow(TableRowViewModel row)
    {
        row.DeepChanged += (_, _) => RaiseDeepChanged();
        row.RefreshCanManageRows();
    }

    private TableRowViewModel CreateEmptyRow(string identity, TableRowKind kind)
    {
        var entries = Definition.Columns.Select(column =>
        {
            LuaValue value = column.ValueType.Equals("boolean", StringComparison.OrdinalIgnoreCase)
                ? new LuaBooleanValue(false)
                : column.ValueType.Equals("integer", StringComparison.OrdinalIgnoreCase) || column.ValueType.Equals("number", StringComparison.OrdinalIgnoreCase)
                    ? new LuaNumberValue(0, "0")
                    : new LuaStringValue("");
            return new LuaTableEntry(column.Key, null, value, new SourceLocation(Key, 0, 0));
        }).ToList();
        return new TableRowViewModel(this, identity, Definition, new LuaTableValue(entries), kind, _defaultHintResolver);
    }

    private static IEnumerable<TableRowViewModel> BuildRows(
        TableDefinition definition,
        LuaTableValue table,
        TableSettingViewModel owner,
        Func<SourceLocation, string?> defaultHintResolver)
    {
        var index = 0;
        foreach (var entry in table.Entries)
        {
            index++;
            if (entry.Value is not LuaTableValue rowTable)
            {
                continue;
            }

            var identity = entry.IdentifierKey ?? RowIdentityFromField(definition, rowTable) ?? index.ToString(CultureInfo.InvariantCulture);
            var kind = string.IsNullOrWhiteSpace(identity) && definition.IsWholeTable
                ? TableRowKind.Placeholder
                : TableRowKind.Source;
            yield return new TableRowViewModel(owner, identity, definition, rowTable, kind, defaultHintResolver);
        }
    }

    private static LuaTableValue RowTableFromOverride(TableRowOverride row)
    {
        var entries = row.Cells
            .Select(cell => new LuaTableEntry(cell.Column.Key, null, cell.State == ConfigValueState.Nil ? LuaNilValue.Instance : cell.Value ?? LuaNilValue.Instance, new SourceLocation("managed", 0, 0)))
            .ToList();
        return new LuaTableValue(entries);
    }

    private bool ValidateIdentity(string identity, out string issue)
    {
        issue = "";
        if (Definition.RowIdentity.Equals("targetGuid", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(identity))
            {
                issue = "targetGuid is required.";
                return false;
            }

            if (!Guid.TryParse(identity, out _))
            {
                issue = "targetGuid must be a GUID.";
                return false;
            }
        }
        else if (Definition.RowIdentity.Equals("materialId", StringComparison.Ordinal))
        {
            if (!int.TryParse(identity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var materialId) || materialId < 0)
            {
                issue = "materialId must be a non-negative integer.";
                return false;
            }
        }

        return true;
    }

    private static string? RowIdentityFromField(TableDefinition definition, LuaTableValue rowTable)
    {
        if (string.IsNullOrWhiteSpace(definition.RowIdentity))
        {
            return null;
        }

        if (!rowTable.TryGetField(definition.RowIdentity, out var value))
        {
            return null;
        }

        return value switch
        {
            LuaStringValue text => text.Value,
            LuaNumberValue number => number.ToCanonicalString(),
            LuaBooleanValue boolean => boolean.Value ? "true" : "false",
            _ => null
        };
    }
}

public enum TableRowKind
{
    Source,
    Added,
    Duplicated,
    Placeholder
}

public sealed class TableRowViewModel : ObservableObject
{
    private readonly TableSettingViewModel _owner;
    private string _identity;
    private bool _canManageRows;

    public TableRowViewModel(
        TableSettingViewModel owner,
        string identity,
        TableDefinition table,
        LuaTableValue rowTable,
        TableRowKind kind,
        Func<SourceLocation, string?> defaultHintResolver)
    {
        _owner = owner;
        _identity = identity;
        Kind = kind;
        OriginalIdentity = identity;
        foreach (var column in table.ColumnsForRow(rowTable))
        {
            var defaultHint = rowTable.TryGetFieldEntry(column.Key, out var entry)
                ? defaultHintResolver(entry.Location)
                : null;
            var value = entry.Value;
            var cell = new TableCellViewModel(
                column,
                value,
                !table.IsWholeTable,
                defaultHint,
                IsExternallyActivatedByRow(table, column) ? () => RowActivationEnabled : null);
            cell.DeepChanged += (_, _) =>
            {
                if (IsRowActivationColumn(table, column))
                {
                    NotifyRowActivationChanged();
                }

                RaiseDeepChanged();
            };
            Cells.Add(cell);
        }

        RemoveCommand = new RelayCommand(_ => _owner.RemoveRow(this), _ => CanManageRows);
        DuplicateCommand = new RelayCommand(_ => _owner.DuplicateRow(this), _ => CanManageRows);
        MoveUpCommand = new RelayCommand(_ => _owner.MoveRow(this, -1), _ => CanManageRows);
        MoveDownCommand = new RelayCommand(_ => _owner.MoveRow(this, 1), _ => CanManageRows);
    }

    public string OriginalIdentity { get; }
    public TableRowKind Kind { get; }
    public ObservableCollection<TableCellViewModel> Cells { get; } = [];
    public ICommand RemoveCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public event EventHandler? DeepChanged;
    public bool IsIdentityEditable => Kind is TableRowKind.Added or TableRowKind.Duplicated or TableRowKind.Placeholder;
    public bool IsIdentityLocked => !IsIdentityEditable;
    public string IdentityDisplayText => ConfigDisplayNames.FriendlyRowIdentity(Identity);
    public string TechnicalIdentity => Identity;
    public TableRowDisplayIdentity DisplayIdentity => new(IdentityDisplayText, TechnicalIdentity, IsIdentityEditable);
    public bool UsesRowActivation => !string.IsNullOrWhiteSpace(_owner.Definition.RowActivationColumn);
    public bool RowActivationEnabled => RowActivationCell?.BooleanEnabled == true;
    private TableCellViewModel? RowActivationCell => Cells.FirstOrDefault(cell => IsRowActivationColumn(_owner.Definition, cell.Column));
    public bool CanManageRows
    {
        get => _canManageRows;
        private set => SetProperty(ref _canManageRows, value);
    }

    public string Identity
    {
        get => _identity;
        set
        {
            if (!IsIdentityEditable && !value.Equals(_identity, StringComparison.Ordinal))
            {
                return;
            }

            if (SetProperty(ref _identity, value))
            {
                OnPropertyChanged(nameof(IdentityDisplayText));
                OnPropertyChanged(nameof(TechnicalIdentity));
                OnPropertyChanged(nameof(DisplayIdentity));
                _owner.MarkIdentityChanged();
                RaiseDeepChanged();
            }
        }
    }

    public bool IsDisabledBlankPlaceholder
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Identity))
            {
                return false;
            }

            var enabled = Cells.FirstOrDefault(cell => cell.Column.Key.Equals("enabled", StringComparison.Ordinal));
            return enabled?.BoolValue != true;
        }
    }

    public TableRowOverride ToOverride(bool wholeTable, TableDefinition table)
    {
        SyncIdentityCell(table);
        var cells = wholeTable
            ? Cells.Select(cell => cell.ToOverrideForWholeTable()).Where(cell => cell.State != ConfigValueState.Unset).ToList()
            : Cells.Select(cell => cell.ToOverride()).Where(cell => cell.State != ConfigValueState.Unset).ToList();
        return new TableRowOverride(Identity, cells);
    }

    public IEnumerable<string> Validate()
    {
        foreach (var cell in Cells)
        {
            foreach (var issue in cell.Validate())
            {
                yield return issue;
            }
        }
    }

    public void ApplyOverride(TableRowOverride rowOverride)
    {
        var sawActivationOverride = false;
        foreach (var cellOverride in rowOverride.Cells)
        {
            var cell = Cells.FirstOrDefault(item => item.Column.Key.Equals(cellOverride.Column.Key, StringComparison.Ordinal));
            if (cell is not null && IsRowActivationColumn(_owner.Definition, cell.Column))
            {
                sawActivationOverride = true;
            }

            cell?.ApplyOverride(cellOverride.State, cellOverride.Value);
        }

        if (!sawActivationOverride &&
            RowActivationCell is { } activation &&
            Cells.Any(cell => cell.UsesExternalActivation && cell.State != ConfigValueState.Unset))
        {
            activation.BooleanEnabled = true;
        }

        NotifyRowActivationChanged();
    }

    public TableRowViewModel CloneAsDuplicate()
    {
        var entries = Cells
            .Select(cell => new LuaTableEntry(cell.Column.Key, null, cell.CurrentLuaValue(), new SourceLocation("duplicate", 0, 0)))
            .ToList();
        return new TableRowViewModel(_owner, "", _owner.Definition, new LuaTableValue(entries), TableRowKind.Duplicated, _owner.DefaultHintFor);
    }

    public void RefreshCanManageRows()
    {
        CanManageRows = _owner.CanManageRows;
    }

    private void SyncIdentityCell(TableDefinition table)
    {
        var cell = Cells.FirstOrDefault(item => item.Column.Key.Equals(table.RowIdentity, StringComparison.Ordinal));
        if (cell is null)
        {
            return;
        }

        cell.ApplyOverride(ConfigValueState.Value, cell.Column.IsNumber
            ? new LuaNumberValue(decimal.TryParse(Identity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0, Identity)
            : new LuaStringValue(Identity));
    }

    private void NotifyRowActivationChanged()
    {
        foreach (var cell in Cells.Where(cell => cell.UsesExternalActivation))
        {
            cell.NotifyExternalActivationChanged();
        }
    }

    private static bool IsRowActivationColumn(TableDefinition table, TableColumnDefinition column)
    {
        return !string.IsNullOrWhiteSpace(table.RowActivationColumn) &&
               column.Key.Equals(table.RowActivationColumn, StringComparison.Ordinal);
    }

    private static bool IsExternallyActivatedByRow(TableDefinition table, TableColumnDefinition column)
    {
        return !table.IsWholeTable &&
               !column.ReadOnly &&
               !IsRowActivationColumn(table, column) &&
               !string.IsNullOrWhiteSpace(table.RowActivationColumn);
    }

    private void RaiseDeepChanged()
    {
        DeepChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class TableCellViewModel : ObservableObject
{
    private readonly Func<bool>? _externalActivation;
    private ConfigValueState _state;
    private string _valueText = "";
    private bool _boolValue;
    private string? _selectedOption;

    public TableCellViewModel(
        TableColumnDefinition column,
        LuaValue defaultValue,
        bool usesOverrideActivation = true,
        string? explicitDefaultHint = null,
        Func<bool>? externalActivation = null)
    {
        Column = column;
        DefaultValue = defaultValue;
        UsesOverrideActivation = usesOverrideActivation;
        ExplicitDefaultHint = explicitDefaultHint;
        _externalActivation = externalActivation;
        StateOptions = column.ReadOnly
            ? ConfigValueStateOptions.ReadOnly
            : ConfigValueStateOptions.ForNullable(column.Nullable);
        ApplyLuaValue(defaultValue);
    }

    public TableColumnDefinition Column { get; }
    public LuaValue DefaultValue { get; }
    public bool UsesOverrideActivation { get; }
    public string? ExplicitDefaultHint { get; }
    public IReadOnlyList<ConfigValueStateOption> StateOptions { get; }
    public bool IsReadOnly => Column.ReadOnly;
    public bool UsesExternalActivation => _externalActivation is not null;
    public bool IsExternallyActive => _externalActivation?.Invoke() == true;
    public ConfigValueState EffectiveState => UsesExternalActivation
        ? IsExternallyActive ? ConfigValueState.Value : ConfigValueState.Unset
        : State;
    public bool CanEditState => !IsReadOnly && UsesOverrideActivation && !UsesExternalActivation;
    public bool CanEditValue => !IsReadOnly && (UsesExternalActivation
        ? IsExternallyActive
        : !UsesOverrideActivation || State == ConfigValueState.Value);
    public bool IsBoolean => Column.IsBoolean;
    public bool IsNonBoolean => !IsBoolean;
    public bool IsNumber => Column.IsNumber;
    public bool IsText => !IsBoolean && !IsNumber && Column.Options.Count == 0;
    public bool IsEnum => Column.Options.Count > 0;
    public bool IsValueEditorEnabled => CanEditValue;
    public bool ShowsOverrideActivation => CanEditState && !IsBoolean;
    public bool ShowsDefaultHint => !IsBoolean && !string.IsNullOrWhiteSpace(ExplicitDefaultHint);
    public bool BooleanEditorEnabled => !IsReadOnly && (!UsesExternalActivation || IsExternallyActive);
    public bool OverrideActive
    {
        get => State == ConfigValueState.Value;
        set
        {
            State = value ? ConfigValueState.Value : ConfigValueState.Unset;
        }
    }

    public bool IsInteger => Column.IsInteger;
    public string DefaultText => $"Default = {ExplicitDefaultHint}";
    public bool HasRange => Column.Min.HasValue || Column.Max.HasValue;
    public string RangeText => Column.Min.HasValue && Column.Max.HasValue
        ? $"Range {Column.Min.Value:0.####} - {Column.Max.Value:0.####}"
        : Column.Min.HasValue
            ? $"Range >= {Column.Min.Value:0.####}"
            : Column.Max.HasValue
                ? $"Range <= {Column.Max.Value:0.####}"
                : "";
    public event EventHandler? DeepChanged;

    public void NotifyExternalActivationChanged()
    {
        OnPropertyChanged(nameof(IsExternallyActive));
        OnPropertyChanged(nameof(EffectiveState));
        OnPropertyChanged(nameof(IsValueEditorEnabled));
        OnPropertyChanged(nameof(CanEditValue));
        OnPropertyChanged(nameof(BooleanEditorEnabled));
        OnPropertyChanged(nameof(BooleanEnabled));
    }

    public ConfigValueState State
    {
        get => _state;
        set
        {
            if (IsReadOnly && value != ConfigValueState.Unset)
            {
                return;
            }

            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsValueEditorEnabled));
                OnPropertyChanged(nameof(CanEditValue));
                OnPropertyChanged(nameof(OverrideActive));
                OnPropertyChanged(nameof(BooleanEnabled));
                RaiseDeepChanged();
            }
        }
    }

    public string ValueText
    {
        get => _valueText;
        set
        {
            if (SetProperty(ref _valueText, value))
            {
                RaiseDeepChanged();
            }
        }
    }

    public bool BoolValue
    {
        get => _boolValue;
        set
        {
            if (SetProperty(ref _boolValue, value))
            {
                OnPropertyChanged(nameof(BooleanEnabled));
                RaiseDeepChanged();
            }
        }
    }

    public bool BooleanEnabled
    {
        get => UsesExternalActivation
            ? IsExternallyActive && BoolValue
            : State == ConfigValueState.Value && BoolValue;
        set
        {
            if (IsReadOnly)
            {
                return;
            }

            if (value)
            {
                if (!BoolValue)
                {
                    BoolValue = true;
                }

                if (State != ConfigValueState.Value)
                {
                    State = ConfigValueState.Value;
                }

                return;
            }

            if (BoolValue)
            {
                BoolValue = false;
            }

            State = ConfigValueState.Unset;
        }
    }

    public string? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (SetProperty(ref _selectedOption, value))
            {
                ValueText = value ?? "";
                RaiseDeepChanged();
            }
        }
    }

    public LuaValue CurrentLuaValue()
    {
        if (Column.IsBoolean)
        {
            return new LuaBooleanValue(BoolValue);
        }

        if (Column.IsNumber)
        {
            if (!decimal.TryParse(ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                throw new FormatException($"{Column.Key}: enter a valid number.");
            }

            if (Column.IsInteger)
            {
                number = decimal.Truncate(number);
            }

            return new LuaNumberValue(number, number.ToString(CultureInfo.InvariantCulture));
        }

        return new LuaStringValue(IsEnum ? (SelectedOption ?? ValueText) : ValueText);
    }

    public TableCellOverride ToOverride()
    {
        if (IsReadOnly)
        {
            return new TableCellOverride(Column, ConfigValueState.Unset, null);
        }

        var state = EffectiveState;
        return new TableCellOverride(Column, state, state == ConfigValueState.Value ? CurrentLuaValue() : null);
    }

    public TableCellOverride ToOverrideForWholeTable()
    {
        if (IsReadOnly)
        {
            return new TableCellOverride(Column, ConfigValueState.Unset, null);
        }

        var state = State == ConfigValueState.Unset ? ConfigValueState.Value : State;
        return new TableCellOverride(Column, state, state == ConfigValueState.Value ? CurrentLuaValue() : null);
    }

    public void ApplyOverride(ConfigValueState state, LuaValue? value)
    {
        if (IsReadOnly)
        {
            return;
        }

        if (state == ConfigValueState.Nil && !Column.Nullable)
        {
            return;
        }

        if (state == ConfigValueState.Value && value is not null)
        {
            ApplyLuaValue(value);
        }

        State = state;
    }

    public IEnumerable<string> Validate()
    {
        if (EffectiveState != ConfigValueState.Value)
        {
            yield break;
        }

        LuaValue? value = null;
        string? parseError = null;
        try
        {
            value = CurrentLuaValue();
        }
        catch (FormatException ex)
        {
            parseError = ex.Message;
        }

        if (parseError is not null)
        {
            yield return parseError;
            yield break;
        }

        if (value is LuaNumberValue number)
        {
            if (Column.Min.HasValue && number.Value < Column.Min.Value)
            {
                yield return $"{Column.Key}: value is below {Column.Min.Value}.";
            }

            if (Column.Max.HasValue && number.Value > Column.Max.Value)
            {
                yield return $"{Column.Key}: value is above {Column.Max.Value}.";
            }
        }
    }

    public void ApplyPreset(PresetSettingValue value, List<string> warnings, string path)
    {
        if (IsReadOnly)
        {
            warnings.Add($"{path}: read-only table cell was ignored.");
            return;
        }

        if (!ValidatePresetState(value, Column.Nullable, path, warnings))
        {
            return;
        }

        if (value.State == ConfigValueState.Value)
        {
            var lua = PresetStore.FromJsonValue(value.Value, Column.ValueType);
            if (lua is not null)
            {
                ApplyLuaValue(lua);
            }
        }

        State = value.State;
    }

    private static bool ValidatePresetState(PresetSettingValue value, bool nullable, string path, List<string> warnings)
    {
        if (!Enum.IsDefined(value.State))
        {
            warnings.Add($"{path}: preset state is not valid.");
            return false;
        }

        if ((value.State == ConfigValueState.Unset || value.State == ConfigValueState.Nil) && value.Value is not null)
        {
            warnings.Add($"{path}: preset unset/nil state must not include a value.");
            return false;
        }

        if (value.State == ConfigValueState.Nil && !nullable)
        {
            warnings.Add($"{path}: preset requested nil for a non-nullable table cell.");
            return false;
        }

        if (value.State == ConfigValueState.Value && value.Value is null)
        {
            warnings.Add($"{path}: preset value state is missing a value.");
            return false;
        }

        return true;
    }

    private void ApplyLuaValue(LuaValue value)
    {
        if (value is LuaBooleanValue boolean)
        {
            BoolValue = boolean.Value;
            ValueText = boolean.Value ? "true" : "false";
        }
        else if (value is LuaNumberValue number)
        {
            ValueText = LuaOverrideWriter.RenderValue(number, Column.IsInteger);
        }
        else if (value is LuaStringValue text)
        {
            ValueText = text.Value;
            SelectedOption = Column.Options.Contains(text.Value, StringComparer.Ordinal) ? text.Value : Column.Options.FirstOrDefault();
        }
        else
        {
            ValueText = Column.IsBoolean ? "false" : "0";
            BoolValue = false;
            SelectedOption = Column.Options.FirstOrDefault();
        }
    }

    private void RaiseDeepChanged()
    {
        DeepChanged?.Invoke(this, EventArgs.Empty);
    }
}
