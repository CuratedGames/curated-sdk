using System;

namespace Curated.Sdk;

/// <summary>One entry for a batch stats store call (absolute value or delta).</summary>
public sealed class StatUpdate
{
    private StatUpdate(string apiName, double? value, double? delta)
    {
        if (string.IsNullOrWhiteSpace(apiName))
        {
            throw new ArgumentException("stat name is required", nameof(apiName));
        }

        if (value is null == delta is null)
        {
            throw new ArgumentException("provide exactly one of value or delta");
        }

        ApiName = apiName.Trim();
        Value = value;
        Delta = delta;
    }

    /// <summary>Stat API name.</summary>
    public string ApiName { get; }

    /// <summary>Absolute value to store, when set.</summary>
    public double? Value { get; }

    /// <summary>Delta to add, when set.</summary>
    public double? Delta { get; }

    /// <summary>Absolute set for <paramref name="apiName"/>.</summary>
    public static StatUpdate Set(string apiName, double value) => new(apiName, value, null);

    /// <summary>Absolute set for <paramref name="apiName"/>.</summary>
    public static StatUpdate Set(string apiName, int value) => Set(apiName, (double)value);

    /// <summary>Increment <paramref name="apiName"/> by <paramref name="delta"/>.</summary>
    public static StatUpdate Increment(string apiName, double delta) => new(apiName, null, delta);

    /// <summary>Increment <paramref name="apiName"/> by <paramref name="delta"/>.</summary>
    public static StatUpdate Increment(string apiName, int delta) => Increment(apiName, (double)delta);
}
