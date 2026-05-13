using System.Text.Json;
using System.Text.Json.Serialization;

namespace EIC.ModLoader;

public sealed class AppliedModContent
{
    public string ModId { get; init; } = string.Empty;
    public int AppliedDefinitionCount { get; init; }
    public int AppliedLocalizationEntryCount { get; init; }
    public int AppliedBalancePatchCount { get; init; }
    public int AppliedCameraPatchCount { get; init; }
    public int AppliedVisualPatchCount { get; init; }
    public List<string> AppliedDefinitionTypes { get; init; } = new();
    public List<string> AppliedDefinitionIds { get; init; } = new();
    public List<string> AppliedLocales { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

public sealed class AppliedContentState
{
    public static AppliedContentState Empty { get; } = new();

    public IReadOnlyDictionary<string, AppliedModContent> Mods { get; init; } =
        new Dictionary<string, AppliedModContent>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Localization { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, BalancePatchDefinition> BalancePatches { get; init; } =
        new Dictionary<string, BalancePatchDefinition>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, CameraPatchDefinition> CameraPatches { get; init; } =
        new Dictionary<string, CameraPatchDefinition>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, VisualPatchDefinition> VisualPatches { get; init; } =
        new Dictionary<string, VisualPatchDefinition>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, IntroSkipDefinition> IntroSkips { get; init; } =
        new Dictionary<string, IntroSkipDefinition>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, UiScaleDefinition> UiScales { get; init; } =
        new Dictionary<string, UiScaleDefinition>(StringComparer.OrdinalIgnoreCase);

    public int AppliedModCount { get; init; }
    public int AppliedDefinitionCount { get; init; }
    public int AppliedLocalizationDefinitionCount { get; init; }
    public int AppliedLocalizationEntryCount { get; init; }
    public int AppliedBalancePatchCount { get; init; }
    public int AppliedCameraPatchCount { get; init; }
    public int AppliedVisualPatchCount { get; init; }
    public int AppliedIntroSkipCount { get; init; }
}

public sealed class LocalizationDefinition
{
    [JsonPropertyName("locale")]
    public string Locale { get; init; } = string.Empty;

    [JsonPropertyName("entries")]
    public Dictionary<string, string> Entries { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BalancePatchDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; init; } = string.Empty;

    [JsonPropertyName("op")]
    public string Operation { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public JsonElement Value { get; init; }

    public string GetValuePreview()
    {
        return Value.ValueKind switch
        {
            JsonValueKind.String => Value.GetString() ?? string.Empty,
            JsonValueKind.Number => Value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => Value.GetRawText()
        };
    }
}

public sealed class CameraPatchDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; init; } = string.Empty;

    [JsonPropertyName("op")]
    public string Operation { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public JsonElement Value { get; init; }

    [JsonPropertyName("settings")]
    public CameraPatchSettings Settings { get; init; } = new();

    public string ModId { get; set; } = string.Empty;

    public string GetValuePreview()
    {
        return Value.ValueKind switch
        {
            JsonValueKind.String => Value.GetString() ?? string.Empty,
            JsonValueKind.Number => Value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => Value.GetRawText()
        };
    }
}

public sealed class CameraPatchSettings
{
    [JsonPropertyName("slider")]
    public bool Slider { get; init; }

    [JsonPropertyName("mouseWheel")]
    public bool MouseWheel { get; init; }

    [JsonPropertyName("invert")]
    public bool Invert { get; init; }

    [JsonPropertyName("label")]
    public string Label { get; init; } = "FOV";

    [JsonPropertyName("min")]
    public float Min { get; init; } = 0.75f;

    [JsonPropertyName("max")]
    public float Max { get; init; } = 1.75f;

    [JsonPropertyName("step")]
    public float Step { get; init; } = 0.05f;
}

public sealed class VisualPatchDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("image")]
    public string Image { get; set; } = string.Empty;

    [JsonPropertyName("pixelsPerUnit")]
    public float PixelsPerUnit { get; set; } = 100f;

    public string ModId { get; set; } = string.Empty;
    public string ResolvedImagePath { get; set; } = string.Empty;
}

public sealed class IntroSkipDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("mode")]
    public string Mode { get; init; } = "containsAny";

    [JsonPropertyName("matchContains")]
    public List<string> MatchContains { get; init; } = new();

    public string ModId { get; set; } = string.Empty;
}

public sealed class UiScaleDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; init; } = "ui.scale";

    [JsonPropertyName("op")]
    public string Operation { get; init; } = "set";

    [JsonPropertyName("value")]
    public JsonElement Value { get; init; }

    [JsonPropertyName("settings")]
    public UiScaleSettings Settings { get; init; } = new();

    public string ModId { get; set; } = string.Empty;

    public string GetValuePreview()
    {
        return Value.ValueKind switch
        {
            JsonValueKind.String => Value.GetString() ?? string.Empty,
            JsonValueKind.Number => Value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => Value.GetRawText()
        };
    }
}

public sealed class UiScaleSettings
{
    [JsonPropertyName("slider")]
    public bool Slider { get; init; } = true;

    [JsonPropertyName("label")]
    public string Label { get; init; } = "UI Scale";

    [JsonPropertyName("min")]
    public float Min { get; init; } = 0.5f;

    [JsonPropertyName("max")]
    public float Max { get; init; } = 1.15f;

    [JsonPropertyName("step")]
    public float Step { get; init; } = 0.05f;
}

public static class RuntimeContentState
{
    public static AppliedContentState Current { get; private set; } = AppliedContentState.Empty;

    public static void Set(AppliedContentState state)
    {
        Current = state ?? AppliedContentState.Empty;
    }

    public static bool TryGetLocalization(string locale, string key, out string value)
    {
        value = string.Empty;
        if (!Current.Localization.TryGetValue(locale, out var table))
        {
            return false;
        }

        return table.TryGetValue(key, out value!);
    }

    public static bool TryGetBalancePatch(string id, out BalancePatchDefinition definition)
    {
        return Current.BalancePatches.TryGetValue(id, out definition!);
    }

    public static bool TryGetCameraPatch(string id, out CameraPatchDefinition definition)
    {
        return Current.CameraPatches.TryGetValue(id, out definition!);
    }

    public static bool TryGetVisualPatch(string id, out VisualPatchDefinition definition)
    {
        return Current.VisualPatches.TryGetValue(id, out definition!);
    }

    public static bool TryGetIntroSkip(string id, out IntroSkipDefinition definition)
    {
        return Current.IntroSkips.TryGetValue(id, out definition!);
    }

    public static bool TryGetUiScale(string id, out UiScaleDefinition definition)
    {
        return Current.UiScales.TryGetValue(id, out definition!);
    }
}
