using System.Globalization;

namespace Ember_Config_Tool.Services;

public sealed record ConfigNumberCommitResult(
    bool Success,
    decimal Value,
    string DisplayText,
    string? ValidationMessage);

public static class ConfigNumberPolicy
{
    public const decimal DefaultStep = 1m;

    public static decimal SnapClamp(decimal value, decimal minimum, decimal maximum, decimal step)
    {
        var (min, max, normalizedStep) = Normalize(minimum, maximum, step);
        var clamped = Math.Min(max, Math.Max(min, value));
        if (normalizedStep <= 0m)
        {
            return clamped;
        }

        var snapped = min + (Math.Round((clamped - min) / normalizedStep, 0, MidpointRounding.AwayFromZero) * normalizedStep);
        return Math.Min(max, Math.Max(min, snapped));
    }

    public static decimal StepValue(decimal value, decimal delta, decimal minimum, decimal maximum, decimal step)
    {
        return SnapClamp(value + delta, minimum, maximum, step);
    }

    public static decimal LargeStep(decimal? configuredLargeStep, decimal step)
    {
        if (configuredLargeStep is > 0m)
        {
            return configuredLargeStep.Value;
        }

        var normalizedStep = step > 0m ? step : DefaultStep;
        return normalizedStep * 5m;
    }

    public static string DisplayFormatForStep(decimal step, bool integer)
    {
        if (integer)
        {
            return "0";
        }

        var normalizedStep = step > 0m ? step : DefaultStep;
        var decimals = DecimalPlaces(normalizedStep);
        return decimals <= 0 ? "0" : "0." + new string('#', decimals);
    }

    public static string FormatDisplay(decimal value, decimal step, bool integer)
    {
        var displayValue = integer ? decimal.Truncate(value) : value;
        return displayValue.ToString(DisplayFormatForStep(step, integer), CultureInfo.InvariantCulture);
    }

    public static ConfigNumberCommitResult ParseAndNormalize(
        string text,
        decimal currentValue,
        decimal minimum,
        decimal maximum,
        decimal step,
        bool integer)
    {
        if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return new ConfigNumberCommitResult(
                false,
                currentValue,
                FormatDisplay(currentValue, step, integer),
                "Enter a valid number.");
        }

        var normalized = SnapClamp(parsed, minimum, maximum, step);
        if (integer)
        {
            normalized = decimal.Truncate(normalized);
        }

        return new ConfigNumberCommitResult(
            true,
            normalized,
            FormatDisplay(normalized, step, integer),
            null);
    }

    public static decimal DecimalFromDouble(double value, decimal fallback)
    {
        if (!double.IsFinite(value))
        {
            return fallback;
        }

        try
        {
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }
        catch (OverflowException)
        {
            return fallback;
        }
    }

    public static double DoubleFromDecimal(decimal value)
    {
        return (double)value;
    }

    public static (decimal Minimum, decimal Maximum, decimal Step) Normalize(decimal minimum, decimal maximum, decimal step)
    {
        var min = minimum;
        var max = maximum < min ? min : maximum;
        var normalizedStep = step > 0m ? step : DefaultStep;
        return (min, max, normalizedStep);
    }

    private static int DecimalPlaces(decimal value)
    {
        value = Math.Abs(value);
        var bits = decimal.GetBits(value);
        var scale = (bits[3] >> 16) & 0x7F;
        while (scale > 0 && decimal.Round(value, scale - 1, MidpointRounding.ToZero) == value)
        {
            scale--;
        }

        return scale;
    }
}
