using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace StabilityMatrix.Avalonia.MarkupExtensions;

/// <summary>
/// Provides a ternary conditional logic within XAML.
/// Usage: {ext:Ternary Condition={Binding SomeBool}, True='ValueIfTrue', False={Binding ValueIfFalse}}
/// </summary>
public class TernaryExtension : MarkupExtension
{
    /// <summary>
    /// Gets or sets the condition to evaluate. Can be a constant or a binding.
    /// This is the default constructor argument.
    /// </summary>
    public object? Condition { get; set; }

    /// <summary>
    /// Gets or sets the value to return if the condition evaluates to true.
    /// Can be a constant or a binding.
    /// </summary>
    public object? True { get; set; }

    /// <summary>
    /// Gets or sets the value to return if the condition evaluates to false.
    /// Can be a constant or a binding.
    /// </summary>
    public object? False { get; set; }

    public TernaryExtension() { }

    public TernaryExtension(object condition)
    {
        Condition = condition;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        // --- Input Validation ---
        // Basic check if Condition is provided (either via constructor or property)
        if (Condition == null)
        {
            // In Avalonia 11+, MarkupExtension exceptions during ProvideValue might not
            // always surface easily. Returning UnsetValue or null might be alternatives,
            // but throwing helps identify the XAML issue during development.
            throw new InvalidOperationException("TernaryExtension requires a Condition.");
            // return AvaloniaProperty.UnsetValue; // Or return null;
        }
        // Optional: Add checks for True/False if they are mandatory in your use case

        // --- Identify Bindings vs Constants ---
        var conditionBinding = Condition as IBinding;
        var trueBinding = True as IBinding;
        var falseBinding = False as IBinding;

        // --- Case 1: All inputs are constants ---
        if (conditionBinding == null && trueBinding == null && falseBinding == null)
        {
            // Evaluate the condition directly
            var conditionResult = EvaluateCondition(Condition);
            return conditionResult ? True : False;
        }

        // --- Case 2: At least one input is a binding ---
        var multiBinding = new MultiBinding
        {
            Converter = TernaryConverter.Instance // Use a shared instance
            // Mode = BindingMode.OneWay // Typically OneWay is sufficient
        };

        var converterParameters = new TernaryConverterParameters();
        var bindingIndex = 0;

        // Process Condition
        if (conditionBinding != null)
        {
            multiBinding.Bindings.Add(conditionBinding);
            converterParameters.ConditionIndex = bindingIndex++;
        }
        else
        {
            converterParameters.ConstantCondition = Condition;
            converterParameters.ConditionIndex = -1; // Indicate constant
        }

        // Process True value
        if (trueBinding != null)
        {
            multiBinding.Bindings.Add(trueBinding);
            converterParameters.TrueValueIndex = bindingIndex++;
        }
        else
        {
            converterParameters.ConstantTrueValue = True;
            converterParameters.TrueValueIndex = -1; // Indicate constant
        }

        // Process False value
        if (falseBinding != null)
        {
            multiBinding.Bindings.Add(falseBinding);
            converterParameters.FalseValueIndex = bindingIndex++;
        }
        else
        {
            converterParameters.ConstantFalseValue = False;
            converterParameters.FalseValueIndex = -1; // Indicate constant
        }

        multiBinding.ConverterParameter = converterParameters;

        // Return the MultiBinding instance. Avalonia will handle its evaluation.
        // Note: In Avalonia 11+, you might need to explicitly provide the target provider
        // if the default resolution isn't sufficient, but usually, this works.
        // For MultiBinding, ProvideValue typically returns the binding instance itself.
        return multiBinding;
        // If targeting Avalonia 11+ and facing issues, you might explore if
        // MultiBinding needs explicit target info from serviceProvider, but start with this.
    }

    /// <summary>
    /// Helper to evaluate the condition object.
    /// Treats null, false, "false" (case-insensitive), and 0 as false. Everything else is true.
    /// Adjust this logic if you need different truthiness rules.
    /// </summary>
    private static bool EvaluateCondition(object? conditionValue)
    {
        if (conditionValue == null)
            return false;
        if (conditionValue is bool b)
            return b;
        if (conditionValue is string s)
            return !string.Equals(s, "false", StringComparison.OrdinalIgnoreCase) && !string.Equals(s, "0");
        if (conditionValue is int i)
            return i != 0;
        if (conditionValue is double d)
            return d != 0.0;
        if (conditionValue is float f)
            return f != 0.0f;
        // Add other numeric types if needed (decimal, long, etc.)

        // Default: Treat non-null, non-specific types as true
        return true;
    }

    /// <summary>
    /// Internal class to hold parameters for the converter.
    /// </summary>
    private class TernaryConverterParameters
    {
        public int ConditionIndex { get; set; }
        public int TrueValueIndex { get; set; }
        public int FalseValueIndex { get; set; }

        public object? ConstantCondition { get; set; }
        public object? ConstantTrueValue { get; set; }
        public object? ConstantFalseValue { get; set; }
    }

    /// <summary>
    /// Converter used by the MultiBinding within TernaryExtension.
    /// </summary>
    private class TernaryConverter : IMultiValueConverter
    {
        /// <summary>
        /// Shared instance to avoid unnecessary allocations.
        /// </summary>
        public static TernaryConverter Instance { get; } = new TernaryConverter();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (parameter is not TernaryConverterParameters parameters)
            {
                // Should not happen if ProvideValue is correct
                return AvaloniaProperty.UnsetValue; // Or BindingOperations.DoNothing or null
            }

            try
            {
                // --- Get Values ---
                // Retrieve values from the binding results or the stored constants
                var conditionValue =
                    parameters.ConditionIndex >= 0
                        ? values[parameters.ConditionIndex]
                        : parameters.ConstantCondition;

                var trueValue =
                    parameters.TrueValueIndex >= 0
                        ? values[parameters.TrueValueIndex]
                        : parameters.ConstantTrueValue;

                var falseValue =
                    parameters.FalseValueIndex >= 0
                        ? values[parameters.FalseValueIndex]
                        : parameters.ConstantFalseValue;

                // --- Evaluate Condition ---
                var conditionResult = EvaluateCondition(conditionValue);

                // --- Return Result ---
                return conditionResult ? trueValue : falseValue;
            }
            catch
            {
                // Error during conversion (e.g., index out of bounds if ProvideValue logic failed)
                return AvaloniaProperty.UnsetValue; // Or BindingOperations.DoNothing or null
            }
        }

        // ConvertBack is not typically needed or meaningful for this type of extension
        public object[]? ConvertBack(
            object? value,
            Type[] targetTypes,
            object? parameter,
            CultureInfo culture
        )
        {
            throw new NotSupportedException("TernaryExtension does not support ConvertBack.");
        }
    }
}
