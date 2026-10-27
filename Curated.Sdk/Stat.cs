using System;

namespace Curated.Sdk;

/// <summary>One backend-defined player stat for the current app.</summary>
public sealed class Stat
{
    private string _displayName;
    private string _valueType;
    private double _value;
    private double _defaultValue;
    private double? _minValue;
    private double? _maxValue;
    private bool _incrementOnly;
    private string _setBy;
    private bool _stored;

    /// <summary>Create a handle for the stat with this API name (matches admin Stats Configuration).</summary>
    public Stat(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("stat name is required", nameof(name));
        }

        var trimmed = name.Trim();
        if (trimmed.Length > 64)
        {
            throw new ArgumentException("stat name max length is 64", nameof(name));
        }

        Name = trimmed;
        _displayName = trimmed;
        _valueType = "int";
        _setBy = "client";
    }

    internal Stat(
        string name,
        string displayName,
        string valueType,
        double value,
        double defaultValue,
        double? minValue,
        double? maxValue,
        bool incrementOnly,
        string setBy,
        bool stored)
    {
        Name = name;
        _displayName = displayName;
        _valueType = string.IsNullOrEmpty(valueType) ? "int" : valueType;
        _value = value;
        _defaultValue = defaultValue;
        _minValue = minValue;
        _maxValue = maxValue;
        _incrementOnly = incrementOnly;
        _setBy = string.IsNullOrEmpty(setBy) ? "client" : setBy;
        _stored = stored;
    }

    /// <summary>Stable API name used when setting or incrementing (same string as in admin).</summary>
    public string Name { get; }

    /// <summary>Player-facing title when progress has been loaded; otherwise equals <see cref="Name"/>.</summary>
    public string DisplayName => _displayName;

    /// <summary>Wire type from the backend: <c>int</c> or <c>float</c>.</summary>
    public string ValueType => _valueType;

    /// <summary>Current value for the local player (after <see cref="Stats.RefreshAsync"/> or a mutate call).</summary>
    public double Value => _value;

    /// <summary>Default value from the stat definition.</summary>
    public double DefaultValue => _defaultValue;

    /// <summary>Optional minimum allowed value.</summary>
    public double? MinValue => _minValue;

    /// <summary>Optional maximum allowed value.</summary>
    public double? MaxValue => _maxValue;

    /// <summary>When true, the backend rejects absolute sets that would decrease the value.</summary>
    public bool IncrementOnly => _incrementOnly;

    /// <summary>Who may write the value: <c>client</c> or <c>server</c>.</summary>
    public string SetBy => _setBy;

    /// <summary>
    /// False when <see cref="Value"/> is still the definition default (no <c>player_stats</c> row yet).
    /// </summary>
    public bool Stored => _stored;

    internal void ApplyProgress(Internal.PlayerStatProgressWire item)
    {
        if (!string.IsNullOrEmpty(item.DisplayName))
        {
            _displayName = item.DisplayName;
        }

        if (!string.IsNullOrEmpty(item.ValueType))
        {
            _valueType = item.ValueType;
        }

        _value = item.Value;
        _defaultValue = item.DefaultValue;
        _minValue = item.MinValue;
        _maxValue = item.MaxValue;
        _incrementOnly = item.IncrementOnly;
        if (!string.IsNullOrEmpty(item.SetBy))
        {
            _setBy = item.SetBy;
        }

        _stored = item.Stored;
    }

    internal void ApplyValue(double value, bool stored = true)
    {
        _value = value;
        _stored = stored;
    }
}
