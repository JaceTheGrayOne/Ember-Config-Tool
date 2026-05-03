using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ember_Config_Tool.Services;

namespace Ember_Config_Tool.Controls;

public partial class ConfigNumberEditor : UserControl
{
    public static readonly DependencyProperty ValueTextProperty = DependencyProperty.Register(
        nameof(ValueText),
        typeof(string),
        typeof(ConfigNumberEditor),
        new FrameworkPropertyMetadata(
            "",
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnValueTextChanged));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum),
        typeof(decimal),
        typeof(ConfigNumberEditor),
        new PropertyMetadata(0m, OnNumberPropertyChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(decimal),
        typeof(ConfigNumberEditor),
        new PropertyMetadata(0m, OnNumberPropertyChanged));

    public static readonly DependencyProperty IncrementProperty = DependencyProperty.Register(
        nameof(Increment),
        typeof(decimal),
        typeof(ConfigNumberEditor),
        new PropertyMetadata(1m, OnNumberPropertyChanged));

    public static readonly DependencyProperty IsIntegerProperty = DependencyProperty.Register(
        nameof(IsInteger),
        typeof(bool),
        typeof(ConfigNumberEditor),
        new PropertyMetadata(false, OnNumberPropertyChanged));

    private bool _syncingControls;
    private decimal _currentValue;

    public ConfigNumberEditor()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshFromModelText(ValueText);
    }

    public string ValueText
    {
        get => (string)GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    public decimal Minimum
    {
        get => (decimal)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public decimal Maximum
    {
        get => (decimal)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public decimal Increment
    {
        get => (decimal)GetValue(IncrementProperty);
        set => SetValue(IncrementProperty, value);
    }

    public bool IsInteger
    {
        get => (bool)GetValue(IsIntegerProperty);
        set => SetValue(IsIntegerProperty, value);
    }

    public void CommitPendingText()
    {
        if (!IsEnabled)
        {
            RecoverTextBoxText();
            return;
        }

        var (minimum, maximum, increment) = Bounds();
        var fallback = ConfigNumberPolicy.SnapClamp(_currentValue, minimum, maximum, increment);
        var result = ConfigNumberPolicy.ParseAndNormalize(
            PART_TextBox.Text,
            fallback,
            minimum,
            maximum,
            increment,
            IsInteger);

        if (!result.Success)
        {
            RecoverTextBoxText();
            return;
        }

        CommitValue(result.Value, result.DisplayText);
    }

    private static void OnValueTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is ConfigNumberEditor editor)
        {
            editor.RefreshFromModelText(args.NewValue as string ?? "");
        }
    }

    private static void OnNumberPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is ConfigNumberEditor editor)
        {
            editor.RefreshFromModelText(editor.ValueText);
        }
    }

    private void RefreshFromModelText(string? modelText)
    {
        if (PART_Slider is null || PART_TextBox is null)
        {
            return;
        }

        var text = modelText ?? "";
        var (minimum, maximum, increment) = Bounds();
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            _currentValue = parsed;
        }

        var sliderValue = ConfigNumberPolicy.SnapClamp(_currentValue, minimum, maximum, increment);
        _syncingControls = true;
        try
        {
            PART_Slider.Minimum = ConfigNumberPolicy.DoubleFromDecimal(minimum);
            PART_Slider.Maximum = ConfigNumberPolicy.DoubleFromDecimal(maximum);
            PART_Slider.SmallChange = ConfigNumberPolicy.DoubleFromDecimal(increment);
            PART_Slider.LargeChange = ConfigNumberPolicy.DoubleFromDecimal(LargerIncrement(increment));
            PART_Slider.TickFrequency = ConfigNumberPolicy.DoubleFromDecimal(increment);
            PART_Slider.Value = ConfigNumberPolicy.DoubleFromDecimal(sliderValue);
            PART_TextBox.Text = text;
        }
        finally
        {
            _syncingControls = false;
        }
    }

    private void CommitValue(decimal value, string? displayText = null)
    {
        var (minimum, maximum, increment) = Bounds();
        var normalized = ConfigNumberPolicy.SnapClamp(value, minimum, maximum, increment);
        if (IsInteger)
        {
            normalized = decimal.Truncate(normalized);
        }

        var rendered = displayText ?? ConfigNumberPolicy.FormatDisplay(normalized, increment, IsInteger);
        _currentValue = normalized;

        _syncingControls = true;
        try
        {
            PART_Slider.Value = ConfigNumberPolicy.DoubleFromDecimal(normalized);
            PART_TextBox.Text = rendered;
        }
        finally
        {
            _syncingControls = false;
        }

        SetCurrentValue(ValueTextProperty, rendered);
        GetBindingExpression(ValueTextProperty)?.UpdateSource();
    }

    private void RecoverTextBoxText()
    {
        _syncingControls = true;
        try
        {
            PART_TextBox.Text = ValueText ?? "";
        }
        finally
        {
            _syncingControls = false;
        }
    }

    private (decimal Minimum, decimal Maximum, decimal Increment) Bounds()
    {
        return ConfigNumberPolicy.Normalize(Minimum, Maximum, Increment);
    }

    private static decimal LargerIncrement(decimal increment)
    {
        return (increment > 0m ? increment : 1m) * 5m;
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncingControls || !IsLoaded || !IsEnabled)
        {
            return;
        }

        var value = ConfigNumberPolicy.DecimalFromDouble(e.NewValue, _currentValue);
        CommitValue(value);
    }

    private void Slider_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsEnabled)
        {
            return;
        }

        var (minimum, maximum, increment) = Bounds();
        var larger = LargerIncrement(increment);
        var target = e.Key switch
        {
            Key.Left or Key.Down => ConfigNumberPolicy.SnapClamp(_currentValue - increment, minimum, maximum, increment),
            Key.Right or Key.Up => ConfigNumberPolicy.SnapClamp(_currentValue + increment, minimum, maximum, increment),
            Key.PageDown => ConfigNumberPolicy.SnapClamp(_currentValue - larger, minimum, maximum, increment),
            Key.PageUp => ConfigNumberPolicy.SnapClamp(_currentValue + larger, minimum, maximum, increment),
            Key.Home => minimum,
            Key.End => maximum,
            _ => (decimal?)null
        };

        if (!target.HasValue)
        {
            return;
        }

        CommitValue(target.Value);
        e.Handled = true;
    }

    private void Slider_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        if (Parent is not UIElement parent)
        {
            return;
        }

        var forwarded = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = MouseWheelEvent,
            Source = this
        };
        parent.RaiseEvent(forwarded);
    }

    private void TextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        CommitPendingText();
    }

    private void TextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitPendingText();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            RecoverTextBoxText();
            e.Handled = true;
        }
    }
}
