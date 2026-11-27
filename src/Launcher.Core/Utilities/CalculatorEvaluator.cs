using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Launcher.Core.Utilities;

public static class CalculatorEvaluator
{
    private static readonly Regex UnitConversionPattern = new(
        @"^([\d\.\,\-]+)\s*([a-zA-Z°]+)\s+(?:to|in|as)\s+([a-zA-Z°]+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex BaseConversionPattern = new(
        @"^(0x[0-9a-fA-F]+|[0-9]+|0b[01]+)\s+(?:to|as|in)\s+(hex|dec|bin|oct)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex MathPattern = new(
        @"^[\d\s\+\-\*\/\^\%\(\)\.\,eEa-zA-Z]+$",
        RegexOptions.Compiled
    );

    [ThreadStatic]
    private static DataTable? _threadDataTable;

    public static bool TryEvaluate(string expression, out double result, out string formatted)
    {
        result = 0;
        formatted = string.Empty;

        if (string.IsNullOrWhiteSpace(expression))
            return false;

        var trimmed = expression.Trim();

        // 0. Clean common prefixes and suffixes for direct calculation
        var isExplicitCalc = false;
        if (trimmed.StartsWith("calc ", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[5..].Trim();
            isExplicitCalc = true;
        }
        else if (trimmed.StartsWith("calc:", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[5..].Trim();
            isExplicitCalc = true;
        }
        else if (trimmed.StartsWith("="))
        {
            trimmed = trimmed[1..].Trim();
            isExplicitCalc = true;
        }

        if (trimmed.EndsWith("="))
            trimmed = trimmed[..^1].Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
            return false;

        // 1. Try Base Conversion (e.g., "0xFF to dec", "255 to hex", "0b1010 to dec", "42 to bin")
        if (TryBaseConversion(trimmed, out var baseFormatted, out var baseVal))
        {
            result = baseVal;
            formatted = baseFormatted;
            return true;
        }

        // 2. Try Unit Conversion (e.g., "100 km to miles", "37 c to f", "1024 mb to gb", "5 kg to lbs")
        if (TryUnitConversion(trimmed, out var unitFormatted, out var unitVal))
        {
            result = unitVal;
            formatted = unitFormatted;
            return true;
        }

        // 3. Mathematical Expression Evaluation
        return TryMathEvaluation(trimmed, isExplicitCalc, out result, out formatted);
    }

    private static bool TryBaseConversion(string expr, out string formatted, out double numValue)
    {
        formatted = string.Empty;
        numValue = 0;

        var match = BaseConversionPattern.Match(expr);
        if (!match.Success)
            return false;

        var inputStr = match.Groups[1].Value.Trim();
        var targetBase = match.Groups[2].Value.ToLowerInvariant();

        long value;
        try
        {
            if (inputStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                value = Convert.ToInt64(inputStr[2..], 16);
            }
            else if (inputStr.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            {
                value = Convert.ToInt64(inputStr[2..], 2);
            }
            else
            {
                value = Convert.ToInt64(inputStr, 10);
            }
        }
        catch
        {
            return false;
        }

        numValue = value;
        formatted = targetBase switch
        {
            "hex" => $"0x{value:X}",
            "dec" => value.ToString(),
            "bin" => $"0b{Convert.ToString(value, 2)}",
            "oct" => $"0o{Convert.ToString(value, 8)}",
            _ => string.Empty
        };

        return !string.IsNullOrEmpty(formatted);
    }

    private static bool TryUnitConversion(string expr, out string formatted, out double resultVal)
    {
        formatted = string.Empty;
        resultVal = 0;

        var match = UnitConversionPattern.Match(expr);
        if (!match.Success)
            return false;

        var numStr = match.Groups[1].Value.Replace(',', '.');
        if (!double.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            return false;

        var fromUnit = match.Groups[2].Value.ToLowerInvariant().Trim('°');
        var toUnit = match.Groups[3].Value.ToLowerInvariant().Trim('°');

        // Length
        if (TryConvertLength(value, fromUnit, toUnit, out resultVal, out var unitLabel))
        {
            formatted = $"{resultVal:G10} {unitLabel}";
            return true;
        }

        // Digital Storage
        if (TryConvertStorage(value, fromUnit, toUnit, out resultVal, out unitLabel))
        {
            formatted = $"{resultVal:G10} {unitLabel}";
            return true;
        }

        // Temperature
        if (TryConvertTemperature(value, fromUnit, toUnit, out resultVal, out unitLabel))
        {
            formatted = $"{resultVal:G10} °{unitLabel}";
            return true;
        }

        // Mass / Weight
        if (TryConvertMass(value, fromUnit, toUnit, out resultVal, out unitLabel))
        {
            formatted = $"{resultVal:G10} {unitLabel}";
            return true;
        }

        // Time
        if (TryConvertTime(value, fromUnit, toUnit, out resultVal, out unitLabel))
        {
            formatted = $"{resultVal:G10} {unitLabel}";
            return true;
        }

        return false;
    }

    private static bool TryConvertLength(double val, string from, string to, out double res, out string unit)
    {
        res = 0;
        unit = to;
        var toMeters = from switch
        {
            "km" or "kilometer" or "kilometers" => val * 1000,
            "m" or "meter" or "meters" => val,
            "cm" or "centimeter" or "centimeters" => val * 0.01,
            "mm" or "millimeter" or "millimeters" => val * 0.001,
            "mi" or "mile" or "miles" => val * 1609.344,
            "yd" or "yard" or "yards" => val * 0.9144,
            "ft" or "foot" or "feet" => val * 0.3048,
            "in" or "inch" or "inches" => val * 0.0254,
            _ => (double?)null
        };

        if (toMeters == null) return false;

        var converted = to switch
        {
            "km" or "kilometer" or "kilometers" => (toMeters.Value / 1000, "km"),
            "m" or "meter" or "meters" => (toMeters.Value, "m"),
            "cm" or "centimeter" or "centimeters" => (toMeters.Value / 0.01, "cm"),
            "mm" or "millimeter" or "millimeters" => (toMeters.Value / 0.001, "mm"),
            "mi" or "mile" or "miles" => (toMeters.Value / 1609.344, "mi"),
            "yd" or "yard" or "yards" => (toMeters.Value / 0.9144, "yd"),
            "ft" or "foot" or "feet" => (toMeters.Value / 0.3048, "ft"),
            "in" or "inch" or "inches" => (toMeters.Value / 0.0254, "in"),
            _ => ((double, string)?)null
        };

        if (converted == null) return false;
        res = converted.Value.Item1;
        unit = converted.Value.Item2;
        return true;
    }

    private static bool TryConvertStorage(double val, string from, string to, out double res, out string unit)
    {
        res = 0;
        unit = to.ToUpperInvariant();
        var toBytes = from switch
        {
            "b" or "byte" or "bytes" => val,
            "kb" or "kilobyte" => val * 1024.0,
            "mb" or "megabyte" => val * 1024.0 * 1024.0,
            "gb" or "gigabyte" => val * 1024.0 * 1024.0 * 1024.0,
            "tb" or "terabyte" => val * 1024.0 * 1024.0 * 1024.0 * 1024.0,
            _ => (double?)null
        };

        if (toBytes == null) return false;

        var converted = to switch
        {
            "b" or "byte" or "bytes" => (toBytes.Value, "B"),
            "kb" or "kilobyte" => (toBytes.Value / 1024.0, "KB"),
            "mb" or "megabyte" => (toBytes.Value / (1024.0 * 1024.0), "MB"),
            "gb" or "gigabyte" => (toBytes.Value / (1024.0 * 1024.0 * 1024.0), "GB"),
            "tb" or "terabyte" => (toBytes.Value / (1024.0 * 1024.0 * 1024.0 * 1024.0), "TB"),
            _ => ((double, string)?)null
        };

        if (converted == null) return false;
        res = converted.Value.Item1;
        unit = converted.Value.Item2;
        return true;
    }

    private static bool TryConvertTemperature(double val, string from, string to, out double res, out string unit)
    {
        res = 0;
        unit = to.ToUpperInvariant();
        double toCelsius;

        switch (from)
        {
            case "c" or "celsius":
                toCelsius = val;
                break;
            case "f" or "fahrenheit":
                toCelsius = (val - 32.0) * 5.0 / 9.0;
                break;
            case "k" or "kelvin":
                toCelsius = val - 273.15;
                break;
            default:
                return false;
        }

        switch (to)
        {
            case "c" or "celsius":
                res = toCelsius;
                unit = "C";
                return true;
            case "f" or "fahrenheit":
                res = (toCelsius * 9.0 / 5.0) + 32.0;
                unit = "F";
                return true;
            case "k" or "kelvin":
                res = toCelsius + 273.15;
                unit = "K";
                return true;
            default:
                return false;
        }
    }

    private static bool TryConvertMass(double val, string from, string to, out double res, out string unit)
    {
        res = 0;
        unit = to;
        var toGrams = from switch
        {
            "kg" or "kilogram" or "kilograms" => val * 1000.0,
            "g" or "gram" or "grams" => val,
            "mg" or "milligram" or "milligrams" => val * 0.001,
            "lb" or "lbs" or "pound" or "pounds" => val * 453.59237,
            "oz" or "ounce" or "ounces" => val * 28.349523,
            _ => (double?)null
        };

        if (toGrams == null) return false;

        var converted = to switch
        {
            "kg" or "kilogram" or "kilograms" => (toGrams.Value / 1000.0, "kg"),
            "g" or "gram" or "grams" => (toGrams.Value, "g"),
            "mg" or "milligram" or "milligrams" => (toGrams.Value / 0.001, "mg"),
            "lb" or "lbs" or "pound" or "pounds" => (toGrams.Value / 453.59237, "lb"),
            "oz" or "ounce" or "ounces" => (toGrams.Value / 28.349523, "oz"),
            _ => ((double, string)?)null
        };

        if (converted == null) return false;
        res = converted.Value.Item1;
        unit = converted.Value.Item2;
        return true;
    }

    private static bool TryConvertTime(double val, string from, string to, out double res, out string unit)
    {
        res = 0;
        unit = to;
        var toSeconds = from switch
        {
            "s" or "sec" or "second" or "seconds" => val,
            "m" or "min" or "minute" or "minutes" => val * 60.0,
            "h" or "hr" or "hour" or "hours" => val * 3600.0,
            "d" or "day" or "days" => val * 86400.0,
            "w" or "week" or "weeks" => val * 604800.0,
            _ => (double?)null
        };

        if (toSeconds == null) return false;

        var converted = to switch
        {
            "s" or "sec" or "second" or "seconds" => (toSeconds.Value, "sec"),
            "m" or "min" or "minute" or "minutes" => (toSeconds.Value / 60.0, "min"),
            "h" or "hr" or "hour" or "hours" => (toSeconds.Value / 3600.0, "hours"),
            "d" or "day" or "days" => (toSeconds.Value / 86400.0, "days"),
            "w" or "week" or "weeks" => (toSeconds.Value / 604800.0, "weeks"),
            _ => ((double, string)?)null
        };

        if (converted == null) return false;
        res = converted.Value.Item1;
        unit = converted.Value.Item2;
        return true;
    }

    private static bool TryMathEvaluation(string expression, bool isExplicitCalc, out double result, out string formatted)
    {
        result = 0;
        formatted = string.Empty;

        var expr = expression.Trim()
            .Replace("x", "*", StringComparison.OrdinalIgnoreCase)
            .Replace("×", "*")
            .Replace("÷", "/")
            .Replace(",", ".");

        // Support 'of' as '*' (e.g. "20% of 500")
        expr = Regex.Replace(expr, @"\bof\b", "*", RegexOptions.IgnoreCase);

        // Support 'mod' as '%' (modulo)
        expr = Regex.Replace(expr, @"\bmod\b", "%", RegexOptions.IgnoreCase);

        // Check if string contains math symbols or functions or explicit calc
        var hasMathOp = expr.Any(c => c is '+' or '-' or '*' or '/' or '^' or '%');
        var hasMathFunc = expr.Contains("sqrt", StringComparison.OrdinalIgnoreCase)
            || expr.Contains("sin", StringComparison.OrdinalIgnoreCase)
            || expr.Contains("cos", StringComparison.OrdinalIgnoreCase)
            || expr.Contains("tan", StringComparison.OrdinalIgnoreCase)
            || expr.Contains("pi", StringComparison.OrdinalIgnoreCase);

        if (!hasMathOp && !hasMathFunc && !isExplicitCalc)
            return false;

        if (!MathPattern.IsMatch(expr))
            return false;

        try
        {
            // Percentage support: understand % as /100.0 (e.g., 20% -> (20 / 100.0), 50 * 20% -> 10, (10+20)% -> 0.3)
            // If % is followed by a number (like "10 % 3"), it's modulo, so only convert % if not followed by a digit.
            expr = Regex.Replace(expr, @"(?<val>(\d+(?:\.\d+)?|\)))\s*%(?!\s*\d)", m =>
            {
                var val = m.Groups["val"].Value;
                if (val == ")")
                    return ") / 100.0";
                return $"({val} / 100.0)";
            });

            // Replace constants
            expr = Regex.Replace(expr, @"\bpi\b", Math.PI.ToString(CultureInfo.InvariantCulture), RegexOptions.IgnoreCase);
            expr = Regex.Replace(expr, @"\be\b", Math.E.ToString(CultureInfo.InvariantCulture), RegexOptions.IgnoreCase);

            // Replace sqrt(x)
            expr = Regex.Replace(expr, @"sqrt\(([^)]+)\)", m =>
            {
                var inner = m.Groups[1].Value.Trim();
                if (double.TryParse(inner, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) || TryEvaluate(inner, out val, out _))
                    return Math.Sqrt(val).ToString(CultureInfo.InvariantCulture);
                return m.Value;
            }, RegexOptions.IgnoreCase);

            // Replace sin(x)
            expr = Regex.Replace(expr, @"sin\(([^)]+)\)", m =>
            {
                var inner = m.Groups[1].Value.Trim();
                if (double.TryParse(inner, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) || TryEvaluate(inner, out val, out _))
                    return Math.Sin(val).ToString(CultureInfo.InvariantCulture);
                return m.Value;
            }, RegexOptions.IgnoreCase);

            // Replace cos(x)
            expr = Regex.Replace(expr, @"cos\(([^)]+)\)", m =>
            {
                var inner = m.Groups[1].Value.Trim();
                if (double.TryParse(inner, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) || TryEvaluate(inner, out val, out _))
                    return Math.Cos(val).ToString(CultureInfo.InvariantCulture);
                return m.Value;
            }, RegexOptions.IgnoreCase);

            // Handle Power ^
            if (expr.Contains('^'))
            {
                expr = Regex.Replace(expr, @"([\d\.]+)\s*\^\s*([\d\.]+)", m =>
                {
                    if (double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var b) &&
                        double.TryParse(m.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
                    {
                        return Math.Pow(b, p).ToString(CultureInfo.InvariantCulture);
                    }
                    return m.Value;
                });
            }

            // Ensure floating point division between integers: e.g. "5 / 2" -> "5.0 / 2.0", "10 / 2 / 5" -> "10.0 / 2.0 / 5.0"
            expr = Regex.Replace(expr, @"(?<![\.\d])(\d+)(?![\.\d])(?=\s*/)", "$1.0");
            expr = Regex.Replace(expr, @"(?<=/\s*)(?<![\.\d])(\d+)(?![\.\d])", "$1.0");

            _threadDataTable ??= new DataTable();
            var res = _threadDataTable.Compute(expr, null);
            if (res != null && double.TryParse(res.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            {
                if (double.IsInfinity(val) || double.IsNaN(val))
                    return false;

                result = val;
                formatted = val.ToString("G15", CultureInfo.InvariantCulture);
                return true;
            }
        }
        catch
        {
            // Invalid expression
        }

        return false;
    }
}
