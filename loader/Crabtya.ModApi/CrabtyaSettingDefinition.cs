namespace Crabtya.ModApi;

public enum CrabtyaSettingKind
{
    Bool,
    Int,
    Float,
    Option
}

public sealed class CrabtyaSettingDefinition
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public CrabtyaSettingKind Kind { get; init; }

    public object DefaultValue { get; init; }

    public int? IntMin { get; init; }

    public int? IntMax { get; init; }

    public int? IntStep { get; init; }

    public float? FloatMin { get; init; }

    public float? FloatMax { get; init; }

    public float? FloatStep { get; init; }

    public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();
}
