using System.Globalization;
using System.Text.Json;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace EIC.ModLoader;

public static class FovSettingsSliderApplicator
{
    private const string SupportedTarget = "camera.orthographicSize";
    private const string SliderObjectName = "Crabtya.NativeFovSlider";
    private const string LabelObjectName = "Crabtya.NativeFovLabel";
    private const string ValueObjectName = "Crabtya.NativeFovValue";

    private static readonly string[] SettingsHints =
    {
        "settings",
        "setting",
        "options",
        "option"
    };

    private static readonly string[] PreferredAnchorLabels =
    {
        "Display Mode",
        "Language",
        "Music",
        "SFX",
        "Global"
    };

    private static ManualLogSource _log;
    private static Slider _slider;
    private static Text _labelText;
    private static Text _valueText;
    private static Transform _sliderParent;
    private static Transform _labelParent;
    private static string _currentPatchId = string.Empty;
    private static bool _suppressChange;
    private static float _nextProbeTime;
    private static float _lastLoggedValue = float.NaN;

    public static void Install(ManualLogSource log)
    {
        _log = log;
        _log.LogInfo("Crabtya native camera settings integration installed.");
    }

    public static void Update()
    {
        if (CrabtyaNativeSettingsApplicator.OwnsCameraPatchSettings)
        {
            DestroyInjectedControls();
            return;
        }

        if (Time.unscaledTime < _nextProbeTime)
        {
            return;
        }

        _nextProbeTime = Time.unscaledTime + 0.35f;

        var patch = GetAdjustableCameraPatch();
        if (patch is null)
        {
            DestroyInjectedControls();
            return;
        }

        var target = FindSettingsTarget();
        if (target is null)
        {
            DestroyInjectedControls();
            return;
        }

        if (_slider is null ||
            _sliderParent != target.SliderParent ||
            _labelParent != target.LabelParent ||
            !string.Equals(_currentPatchId, patch.Id, StringComparison.OrdinalIgnoreCase))
        {
            DestroyInjectedControls();
            CreateIntegratedRow(target, patch);
        }

        SyncSlider(patch);
    }

    private static CameraPatchDefinition GetAdjustableCameraPatch()
    {
        return RuntimeContentState.Current.CameraPatches.Values
            .Where(p => p.Settings.Slider)
            .Where(p => string.Equals(p.Target, SupportedTarget, StringComparison.OrdinalIgnoreCase))
            .LastOrDefault();
    }

    private static SettingsTarget FindSettingsTarget()
    {
        var sliders = FindActiveGameSliders();
        if (sliders.Count == 0)
        {
            return null;
        }

        var sliderTemplate = sliders
            .OrderByDescending(s => ScoreSlider(s))
            .FirstOrDefault();
        if (sliderTemplate is null)
        {
            return null;
        }

        var sliderTemplateRect = sliderTemplate.GetComponent<RectTransform>();
        if (sliderTemplateRect is null || sliderTemplateRect.parent is null)
        {
            return null;
        }

        var texts = FindActiveGameTexts();
        var labelTemplate = FindLabelTemplate(texts, sliderTemplateRect);
        if (labelTemplate is null)
        {
            return null;
        }

        var labelTemplateRect = labelTemplate.GetComponent<RectTransform>();
        if (labelTemplateRect is null || labelTemplateRect.parent is null)
        {
            return null;
        }

        var sliderSpacing = EstimateSpacing(
            sliders
                .Select(s => s.GetComponent<RectTransform>())
                .Where(r => r is not null && r.parent == sliderTemplateRect.parent)
                .Select(r => r.anchoredPosition.y)
                .ToList(),
            60f);
        var labelSpacing = EstimateSpacing(
            texts
                .Select(t => t.GetComponent<RectTransform>())
                .Where(r => r is not null && r.parent == labelTemplateRect.parent)
                .Select(r => r.anchoredPosition.y)
                .ToList(),
            sliderSpacing);

        var sliderY = ChooseInsertionY(
            sliders
                .Select(s => s.GetComponent<RectTransform>())
                .Where(r => r is not null && r.parent == sliderTemplateRect.parent)
                .Select(r => r.anchoredPosition.y)
                .ToList(),
            sliderSpacing);
        var labelY = ChooseInsertionY(
            texts
                .Where(IsSettingsRowLabel)
                .Select(t => t.GetComponent<RectTransform>())
                .Where(r => r is not null && r.parent == labelTemplateRect.parent)
                .Select(r => r.anchoredPosition.y)
                .ToList(),
            labelSpacing);
        if (labelTemplateRect.parent == sliderTemplateRect.parent)
        {
            sliderY = labelY;
        }

        return new SettingsTarget(
            sliderTemplate,
            labelTemplate,
            sliderTemplateRect.parent,
            labelTemplateRect.parent,
            sliderTemplateRect.anchoredPosition.x,
            sliderY,
            labelTemplateRect.anchoredPosition.x,
            labelY);
    }

    private static List<Slider> FindActiveGameSliders()
    {
        var result = new List<Slider>();
        Slider[] sliders;
        try
        {
            sliders = Resources.FindObjectsOfTypeAll<Slider>();
        }
        catch
        {
            return result;
        }

        for (var i = 0; i < sliders.Length; i++)
        {
            var slider = sliders[i];
            if (slider is null ||
                slider.gameObject is null ||
                !slider.gameObject.activeInHierarchy ||
                IsLoaderObject(slider.transform))
            {
                continue;
            }

            if (IsInSettingsContext(slider.transform))
            {
                result.Add(slider);
            }
        }

        return result;
    }

    private static List<Text> FindActiveGameTexts()
    {
        var result = new List<Text>();
        Text[] texts;
        try
        {
            texts = Resources.FindObjectsOfTypeAll<Text>();
        }
        catch
        {
            return result;
        }

        for (var i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            if (text is null ||
                text.gameObject is null ||
                !text.gameObject.activeInHierarchy ||
                IsLoaderObject(text.transform) ||
                string.IsNullOrWhiteSpace(text.text))
            {
                continue;
            }

            if (IsInSettingsContext(text.transform))
            {
                result.Add(text);
            }
        }

        return result;
    }

    private static Text FindLabelTemplate(IReadOnlyList<Text> texts, RectTransform sliderTemplateRect)
    {
        foreach (var preferred in PreferredAnchorLabels)
        {
            var match = texts.FirstOrDefault(t => string.Equals(t.text.Trim(), preferred, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        var sliderY = sliderTemplateRect.position.y;
        return texts
            .Where(IsSettingsRowLabel)
            .OrderBy(t => Math.Abs(t.GetComponent<RectTransform>().position.y - sliderY))
            .FirstOrDefault();
    }

    private static bool IsSettingsRowLabel(Text text)
    {
        var value = text.text.Trim();
        return PreferredAnchorLabels.Any(label => string.Equals(value, label, StringComparison.OrdinalIgnoreCase)) ||
            value.Contains("volume", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("mode", StringComparison.OrdinalIgnoreCase);
    }

    private static int ScoreSlider(Slider slider)
    {
        var score = 0;
        var chain = BuildNameChain(slider.transform);
        if (ContainsAnyHint(chain))
        {
            score += 100;
        }

        var texts = slider.GetComponentsInParent<RectTransform>(true)
            .SelectMany(r =>
            {
                try { return r.GetComponentsInChildren<Text>(true); }
                catch { return Array.Empty<Text>(); }
            })
            .Where(t => t is not null && t.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(t.text))
            .Take(40);
        foreach (var text in texts)
        {
            if (ContainsAnyHint(text.text) || IsSettingsRowLabel(text))
            {
                score += 20;
            }
        }

        return score;
    }

    private static bool IsInSettingsContext(Transform transform)
    {
        if (ContainsAnyHint(BuildNameChain(transform)))
        {
            return true;
        }

        var current = transform;
        for (var depth = 0; current is not null && depth < 8; depth++)
        {
            Text[] texts;
            try
            {
                texts = current.GetComponentsInChildren<Text>(true);
            }
            catch
            {
                current = current.parent;
                continue;
            }

            var usefulTextCount = 0;
            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text is null || !text.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(text.text))
                {
                    continue;
                }

                usefulTextCount++;
                if (ContainsAnyHint(text.text) || IsSettingsRowLabel(text))
                {
                    return true;
                }
            }

            if (usefulTextCount > 20)
            {
                break;
            }

            current = current.parent;
        }

        return false;
    }

    private static string BuildNameChain(Transform transform)
    {
        var names = new List<string>();
        var current = transform;
        while (current is not null && names.Count < 10)
        {
            names.Add(current.name ?? string.Empty);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private static bool ContainsAnyHint(string value)
    {
        for (var i = 0; i < SettingsHints.Length; i++)
        {
            if (value.Contains(SettingsHints[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLoaderObject(Transform transform)
    {
        var current = transform;
        while (current is not null)
        {
            var name = current.name ?? string.Empty;
            if (name.StartsWith("EIC.ModLoader", StringComparison.Ordinal) ||
                name.StartsWith("Crabtya.", StringComparison.Ordinal))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static float EstimateSpacing(IReadOnlyList<float> yPositions, float fallback)
    {
        var ordered = yPositions
            .Distinct()
            .OrderByDescending(v => v)
            .ToList();
        var deltas = new List<float>();
        for (var i = 1; i < ordered.Count; i++)
        {
            var delta = Math.Abs(ordered[i - 1] - ordered[i]);
            if (delta > 4f && delta < 180f)
            {
                deltas.Add(delta);
            }
        }

        if (deltas.Count == 0)
        {
            return fallback;
        }

        deltas.Sort();
        return Math.Max(36f, deltas[deltas.Count / 2]);
    }

    private static float ChooseInsertionY(IReadOnlyList<float> yPositions, float spacing)
    {
        if (yPositions.Count == 0)
        {
            return -spacing;
        }

        return yPositions.Min() - spacing;
    }

    private static void CreateIntegratedRow(SettingsTarget target, CameraPatchDefinition patch)
    {
        _sliderParent = target.SliderParent;
        _labelParent = target.LabelParent;
        _currentPatchId = patch.Id;

        var labelObject = UnityEngine.Object.Instantiate(target.LabelTemplate.gameObject, target.LabelParent);
        labelObject.name = LabelObjectName;
        _labelText = labelObject.GetComponent<Text>();
        _labelText.text = string.IsNullOrWhiteSpace(patch.Settings.Label) ? "FOV" : patch.Settings.Label;
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchoredPosition = new Vector2(target.LabelX, target.LabelY);

        var sliderObject = UnityEngine.Object.Instantiate(target.SliderTemplate.gameObject, target.SliderParent);
        sliderObject.name = SliderObjectName;
        _slider = sliderObject.GetComponent<Slider>();
        _slider.onValueChanged.RemoveAllListeners();
        _slider.direction = Slider.Direction.LeftToRight;
        _slider.wholeNumbers = false;
        _slider.minValue = GetMin(patch);
        _slider.maxValue = GetMax(patch);
        var sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchoredPosition = new Vector2(target.SliderX, target.SliderY);

        _valueText = CreateValueText(target, patch);
        _slider.onValueChanged.AddListener((Action<float>)OnSliderChanged);

        _log?.LogInfo(
            $"Crabtya native camera setting row injected for patch '{patch.Id}'. SliderParent={target.SliderParent.name}, LabelParent={target.LabelParent.name}, SliderY={target.SliderY}, LabelY={target.LabelY}");
    }

    private static Text CreateValueText(SettingsTarget target, CameraPatchDefinition patch)
    {
        var valueObject = UnityEngine.Object.Instantiate(target.LabelTemplate.gameObject, target.LabelParent);
        valueObject.name = ValueObjectName;
        var valueText = valueObject.GetComponent<Text>();
        valueText.alignment = TextAnchor.MiddleRight;
        valueText.text = string.Empty;
        var valueRect = valueObject.GetComponent<RectTransform>();
        valueRect.anchoredPosition = new Vector2(target.LabelX + 260f, target.LabelY);
        valueRect.sizeDelta = new Vector2(120f, valueRect.sizeDelta.y);
        return valueText;
    }

    private static void SyncSlider(CameraPatchDefinition patch)
    {
        if (_slider is null)
        {
            return;
        }

        var min = GetMin(patch);
        var max = GetMax(patch);
        var step = GetStep(patch);
        var value = Mathf.Clamp(Quantize(GetCurrentValue(patch), step), min, max);

        _suppressChange = true;
        _slider.minValue = min;
        _slider.maxValue = max;
        _slider.SetValueWithoutNotify(value);
        _suppressChange = false;

        if (_labelText is not null)
        {
            _labelText.text = string.IsNullOrWhiteSpace(patch.Settings.Label) ? "FOV" : patch.Settings.Label;
        }

        SetValueText(value);
    }

    private static void OnSliderChanged(float rawValue)
    {
        if (_suppressChange || string.IsNullOrWhiteSpace(_currentPatchId))
        {
            return;
        }

        var patch = GetAdjustableCameraPatch();
        if (patch is null)
        {
            return;
        }

        var value = Mathf.Clamp(Quantize(rawValue, GetStep(patch)), GetMin(patch), GetMax(patch));
        RuntimeModSettings.SetFloat(_currentPatchId, value);
        SetValueText(value);
        CameraPatchApplicator.ApplyToLiveCameras();

        if (float.IsNaN(_lastLoggedValue) || Math.Abs(_lastLoggedValue - value) >= GetStep(patch))
        {
            _lastLoggedValue = value;
            _log?.LogInfo($"Crabtya native camera setting updated: '{_currentPatchId}'={value.ToString("0.##", CultureInfo.InvariantCulture)}.");
        }
    }

    private static void SetValueText(float value)
    {
        if (_valueText is not null)
        {
            _valueText.text = $"{value.ToString("0.##", CultureInfo.InvariantCulture)}x";
        }
    }

    private static float GetCurrentValue(CameraPatchDefinition patch)
    {
        if (RuntimeModSettings.TryGetFloat(patch.Id, out var value))
        {
            return value;
        }

        return TryGetNumericValue(patch.Value, out value) ? value : 1f;
    }

    private static float GetMin(CameraPatchDefinition patch)
    {
        return patch.Settings.Min > 0f ? patch.Settings.Min : 0.5f;
    }

    private static float GetMax(CameraPatchDefinition patch)
    {
        var min = GetMin(patch);
        return patch.Settings.Max > min ? patch.Settings.Max : min + 1f;
    }

    private static float GetStep(CameraPatchDefinition patch)
    {
        return patch.Settings.Step > 0f ? patch.Settings.Step : 0.05f;
    }

    private static float Quantize(float value, float step)
    {
        return step <= 0f ? value : (float)Math.Round(value / step) * step;
    }

    private static bool TryGetNumericValue(JsonElement element, out float value)
    {
        value = 0f;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetSingle(out value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return float.TryParse(
                element.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        return false;
    }

    private static void DestroyInjectedControls()
    {
        DestroyIfPresent(_slider);
        DestroyIfPresent(_labelText);
        DestroyIfPresent(_valueText);

        _slider = null;
        _labelText = null;
        _valueText = null;
        _sliderParent = null;
        _labelParent = null;
        _currentPatchId = string.Empty;
        _lastLoggedValue = float.NaN;
    }

    private static void DestroyIfPresent(Component component)
    {
        if (component is not null && component.gameObject is not null)
        {
            UnityEngine.Object.Destroy(component.gameObject);
        }
    }

    private sealed class SettingsTarget
    {
        public SettingsTarget(
            Slider sliderTemplate,
            Text labelTemplate,
            Transform sliderParent,
            Transform labelParent,
            float sliderX,
            float sliderY,
            float labelX,
            float labelY)
        {
            SliderTemplate = sliderTemplate;
            LabelTemplate = labelTemplate;
            SliderParent = sliderParent;
            LabelParent = labelParent;
            SliderX = sliderX;
            SliderY = sliderY;
            LabelX = labelX;
            LabelY = labelY;
        }

        public Slider SliderTemplate { get; }

        public Text LabelTemplate { get; }

        public Transform SliderParent { get; }

        public Transform LabelParent { get; }

        public float SliderX { get; }

        public float SliderY { get; }

        public float LabelX { get; }

        public float LabelY { get; }
    }
}
