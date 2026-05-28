namespace Stockflow.Simulation.Component;

public enum PropertyType
{
    Float,
    Int,
    String,
    Bool,
    Enum,
}

/// <summary>
/// Describes a single configurable property on a component.
/// The simulation engine uses this for validation; clients use it to generate
/// inspector UIs dynamically without hardcoding per-component-type logic.
/// </summary>
public sealed record PropertySchema(
    string        Key,
    string        DisplayName,
    PropertyType  Type,
    string?       DefaultValue  = null,
    string?       Min           = null,
    string?       Max           = null,
    string[]?     EnumValues    = null,
    bool          IsReadOnly    = false
)
{
    /// <summary>
    /// Validates a string value against this schema. Returns null if valid,
    /// or an error message string if invalid.
    /// </summary>
    public string? Validate(string? value)
    {
        if (value is null)
            return DefaultValue is not null ? null : $"Property '{Key}' is required";

        return Type switch
        {
            PropertyType.Float  => ValidateFloat(value),
            PropertyType.Int    => ValidateInt(value),
            PropertyType.Bool   => bool.TryParse(value, out _) ? null : $"'{Key}' must be true/false",
            PropertyType.Enum   => EnumValues?.Contains(value, StringComparer.OrdinalIgnoreCase) == true
                                       ? null
                                       : $"'{Key}' must be one of: {string.Join(", ", EnumValues ?? [])}",
            PropertyType.String => null,
            _                   => null,
        };
    }

    private string? ValidateFloat(string value)
    {
        if (!float.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var f))
            return $"'{Key}' must be a number";
        if (Min is not null && float.TryParse(Min, System.Globalization.CultureInfo.InvariantCulture, out var min) && f < min)
            return $"'{Key}' must be >= {Min}";
        if (Max is not null && float.TryParse(Max, System.Globalization.CultureInfo.InvariantCulture, out var max) && f > max)
            return $"'{Key}' must be <= {Max}";
        return null;
    }

    private string? ValidateInt(string value)
    {
        if (!int.TryParse(value, out var i))
            return $"'{Key}' must be an integer";
        if (Min is not null && int.TryParse(Min, out var min) && i < min)
            return $"'{Key}' must be >= {Min}";
        if (Max is not null && int.TryParse(Max, out var max) && i > max)
            return $"'{Key}' must be <= {Max}";
        return null;
    }
}
