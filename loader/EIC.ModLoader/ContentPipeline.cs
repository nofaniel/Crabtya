using System.Text.Json;
using BepInEx.Logging;

namespace EIC.ModLoader;

public static class ContentPipeline
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static AppliedContentState Apply(
        IEnumerable<DiscoveredModEntry> entries,
        IReadOnlyList<string> loadOrder,
        Dictionary<string, ModStateEntry> modState,
        ManualLogSource log)
    {
        var entryById = entries
            .Where(e => e.Manifest?.Id is not null)
            .ToDictionary(e => e.Manifest!.Id!, e => e, StringComparer.OrdinalIgnoreCase);

        var appliedMods = new Dictionary<string, AppliedModContent>(StringComparer.OrdinalIgnoreCase);
        var localization = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var balancePatches = new Dictionary<string, BalancePatchDefinition>(StringComparer.OrdinalIgnoreCase);
        var cameraPatches = new Dictionary<string, CameraPatchDefinition>(StringComparer.OrdinalIgnoreCase);
        var visualPatches = new Dictionary<string, VisualPatchDefinition>(StringComparer.OrdinalIgnoreCase);
        var introSkips = new Dictionary<string, IntroSkipDefinition>(StringComparer.OrdinalIgnoreCase);
        var uiScales = new Dictionary<string, UiScaleDefinition>(StringComparer.OrdinalIgnoreCase);

        var appliedDefinitionCount = 0;
        var appliedLocalizationDefinitionCount = 0;
        var appliedLocalizationEntryCount = 0;
        var appliedBalancePatchCount = 0;
        var appliedCameraPatchCount = 0;
        var appliedVisualPatchCount = 0;
        var appliedIntroSkipCount = 0;

        foreach (var modId in loadOrder)
        {
            if (!entryById.TryGetValue(modId, out var entry) ||
                !modState.TryGetValue(modId, out var state) ||
                !state.EffectiveEnabled ||
                entry.Manifest?.Content?.Definitions is null)
            {
                continue;
            }

            var modWarnings = new List<string>();
            var appliedDefinitionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var appliedDefinitionIds = new List<string>();
            var appliedLocales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var modAppliedDefinitionCount = 0;
            var modAppliedLocalizationEntryCount = 0;
            var modAppliedBalancePatchCount = 0;
            var modAppliedCameraPatchCount = 0;
            var modAppliedVisualPatchCount = 0;

            foreach (var relativePath in entry.Manifest.Content.Definitions)
            {
                var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
                var resolved = Path.Combine(entry.DirectoryPath, normalized);
                if (!File.Exists(resolved))
                {
                    continue;
                }

                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(resolved));
                    var root = document.RootElement;
                    if (!root.TryGetProperty("type", out var typeProperty) ||
                        typeProperty.ValueKind != JsonValueKind.String)
                    {
                        modWarnings.Add($"Definition '{relativePath}' skipped because 'type' is missing.");
                        continue;
                    }

                    var definitionType = typeProperty.GetString();
                    if (string.IsNullOrWhiteSpace(definitionType))
                    {
                        modWarnings.Add($"Definition '{relativePath}' skipped because 'type' is empty.");
                        continue;
                    }

                    switch (definitionType)
                    {
                        case "localization":
                        {
                            var definition = JsonSerializer.Deserialize<LocalizationDefinition>(root.GetRawText(), JsonOptions);
                            if (definition is null || string.IsNullOrWhiteSpace(definition.Locale))
                            {
                                modWarnings.Add($"Localization definition '{relativePath}' is missing locale.");
                                continue;
                            }

                            if (definition.Entries.Count == 0)
                            {
                                modWarnings.Add($"Localization definition '{relativePath}' has no entries.");
                                continue;
                            }

                            if (!localization.TryGetValue(definition.Locale, out var localeEntries))
                            {
                                localeEntries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                localization[definition.Locale] = localeEntries;
                            }

                            foreach (var pair in definition.Entries)
                            {
                                localeEntries[pair.Key] = pair.Value;
                            }

                            appliedLocalizationDefinitionCount++;
                            modAppliedLocalizationEntryCount += definition.Entries.Count;
                            appliedLocalizationEntryCount += definition.Entries.Count;
                            modAppliedDefinitionCount++;
                            appliedDefinitionCount++;
                            appliedDefinitionTypes.Add("localization");
                            appliedLocales.Add(definition.Locale);
                            appliedDefinitionIds.Add($"{definition.Locale}:{relativePath}");

                            log.LogInfo(
                                $"Applied localization definition for mod '{modId}': Locale={definition.Locale}, Entries={definition.Entries.Count}");
                            break;
                        }
                        case "balancePatch":
                        {
                            var definition = JsonSerializer.Deserialize<BalancePatchDefinition>(root.GetRawText(), JsonOptions);
                            if (definition is null ||
                                string.IsNullOrWhiteSpace(definition.Id) ||
                                string.IsNullOrWhiteSpace(definition.Target) ||
                                string.IsNullOrWhiteSpace(definition.Operation))
                            {
                                modWarnings.Add($"Balance patch definition '{relativePath}' is missing required fields.");
                                continue;
                            }

                            balancePatches[definition.Id] = definition;
                            appliedBalancePatchCount++;
                            modAppliedBalancePatchCount++;
                            modAppliedDefinitionCount++;
                            appliedDefinitionCount++;
                            appliedDefinitionTypes.Add("balancePatch");
                            appliedDefinitionIds.Add(definition.Id);

                            log.LogInfo(
                                $"Applied balance patch for mod '{modId}': Id={definition.Id}, Target={definition.Target}, Op={definition.Operation}, Value={definition.GetValuePreview()}");
                            break;
                        }
                        case "cameraPatch":
                        {
                            var definition = JsonSerializer.Deserialize<CameraPatchDefinition>(root.GetRawText(), JsonOptions);
                            if (definition is null ||
                                string.IsNullOrWhiteSpace(definition.Id) ||
                                string.IsNullOrWhiteSpace(definition.Target) ||
                                string.IsNullOrWhiteSpace(definition.Operation))
                            {
                                modWarnings.Add($"Camera patch definition '{relativePath}' is missing required fields.");
                                continue;
                            }

                            if (!string.Equals(definition.Target, "camera.orthographicSize", StringComparison.OrdinalIgnoreCase))
                            {
                                modWarnings.Add($"Camera patch definition '{relativePath}' targets unsupported surface '{definition.Target}'.");
                                continue;
                            }

                            definition.ModId = modId;

                            cameraPatches[definition.Id] = definition;
                            appliedCameraPatchCount++;
                            modAppliedCameraPatchCount++;
                            modAppliedDefinitionCount++;
                            appliedDefinitionCount++;
                            appliedDefinitionTypes.Add("cameraPatch");
                            appliedDefinitionIds.Add(definition.Id);

                            log.LogInfo(
                                $"Applied camera patch for mod '{modId}': Id={definition.Id}, Target={definition.Target}, Op={definition.Operation}, Value={definition.GetValuePreview()}");
                            break;
                        }
                        case "visual":
                        {
                            var definition = JsonSerializer.Deserialize<VisualPatchDefinition>(root.GetRawText(), JsonOptions);
                            if (definition is null ||
                                string.IsNullOrWhiteSpace(definition.Id) ||
                                string.IsNullOrWhiteSpace(definition.Target) ||
                                string.IsNullOrWhiteSpace(definition.Image))
                            {
                                modWarnings.Add($"Visual definition '{relativePath}' is missing required fields.");
                                continue;
                            }

                            if (!IsVisualTargetSupported(definition.Target))
                            {
                                modWarnings.Add($"Visual definition '{relativePath}' targets unsupported surface '{definition.Target}'.");
                                continue;
                            }

                            if (Path.IsPathRooted(definition.Image) ||
                                definition.Image.Contains("..", StringComparison.Ordinal))
                            {
                                modWarnings.Add($"Visual definition '{relativePath}' has unsafe image path '{definition.Image}'.");
                                continue;
                            }

                            var imagePath = Path.Combine(
                                entry.DirectoryPath,
                                definition.Image.Replace('/', Path.DirectorySeparatorChar));
                            if (!File.Exists(imagePath))
                            {
                                modWarnings.Add($"Visual definition '{relativePath}' image is missing: {definition.Image}");
                                continue;
                            }

                            definition.ModId = modId;
                            definition.ResolvedImagePath = imagePath;
                            if (definition.PixelsPerUnit <= 0)
                            {
                                definition.PixelsPerUnit = 100f;
                            }

                            visualPatches[definition.Id] = definition;
                            appliedVisualPatchCount++;
                            modAppliedVisualPatchCount++;
                            modAppliedDefinitionCount++;
                            appliedDefinitionCount++;
                            appliedDefinitionTypes.Add("visual");
                            appliedDefinitionIds.Add(definition.Id);

                            log.LogInfo(
                                $"Applied visual definition for mod '{modId}': Id={definition.Id}, Target={definition.Target}, Image={definition.Image}, PixelsPerUnit={definition.PixelsPerUnit}");
                            break;
                        }
                        case "introSkip":
                        {
                            var definition = JsonSerializer.Deserialize<IntroSkipDefinition>(root.GetRawText(), JsonOptions);
                            if (definition is null || string.IsNullOrWhiteSpace(definition.Id))
                            {
                                modWarnings.Add($"Intro-skip definition '{relativePath}' is missing required fields.");
                                continue;
                            }

                            definition.ModId = modId;
                            introSkips[definition.Id] = definition;
                            appliedIntroSkipCount++;
                            modAppliedDefinitionCount++;
                            appliedDefinitionCount++;
                            appliedDefinitionTypes.Add("introSkip");
                            appliedDefinitionIds.Add(definition.Id);

                            log.LogInfo(
                                $"Applied intro-skip definition for mod '{modId}': Id={definition.Id}, Mode={definition.Mode}, MatchContains={string.Join(", ", definition.MatchContains)}");
                            break;
                        }
                        case "uiScale":
                        {
                            var definition = JsonSerializer.Deserialize<UiScaleDefinition>(root.GetRawText(), JsonOptions);
                            if (definition is null ||
                                string.IsNullOrWhiteSpace(definition.Id) ||
                                string.IsNullOrWhiteSpace(definition.Target) ||
                                string.IsNullOrWhiteSpace(definition.Operation))
                            {
                                modWarnings.Add($"UI-scale definition '{relativePath}' is missing required fields.");
                                continue;
                            }

                            if (!string.Equals(definition.Target, "ui.scale", StringComparison.OrdinalIgnoreCase))
                            {
                                modWarnings.Add($"UI-scale definition '{relativePath}' targets unsupported surface '{definition.Target}'.");
                                continue;
                            }

                            definition.ModId = modId;
                            uiScales[definition.Id] = definition;
                            modAppliedDefinitionCount++;
                            appliedDefinitionCount++;
                            appliedDefinitionTypes.Add("uiScale");
                            appliedDefinitionIds.Add(definition.Id);

                            log.LogInfo(
                                $"Applied UI-scale definition for mod '{modId}': Id={definition.Id}, Target={definition.Target}, Op={definition.Operation}, Value={definition.GetValuePreview()}");
                            break;
                        }
                        default:
                            modWarnings.Add(
                                $"Definition '{relativePath}' uses unsupported apply type '{definitionType}' and was skipped.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    modWarnings.Add($"Definition '{relativePath}' apply error: {ex.Message}");
                }
            }

            state.AppliedDefinitionCount = modAppliedDefinitionCount;
            state.AppliedLocalizationEntryCount = modAppliedLocalizationEntryCount;
            state.AppliedBalancePatchCount = modAppliedBalancePatchCount;
            state.AppliedCameraPatchCount = modAppliedCameraPatchCount;
            state.AppliedVisualPatchCount = modAppliedVisualPatchCount;
            state.AppliedDefinitionTypes = appliedDefinitionTypes.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList();
            state.AppliedDefinitionIds = appliedDefinitionIds;
            state.AppliedLocales = appliedLocales.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var warning in modWarnings)
            {
                if (!state.Warnings.Any(existing => string.Equals(existing, warning, StringComparison.OrdinalIgnoreCase)))
                {
                    state.Warnings.Add(warning);
                }

                log.LogWarning($"Mod '{modId}' content warning: {warning}");
            }

            if (modAppliedDefinitionCount > 0 || modWarnings.Count > 0)
            {
                appliedMods[modId] = new AppliedModContent
                {
                    ModId = modId,
                    AppliedDefinitionCount = modAppliedDefinitionCount,
                    AppliedLocalizationEntryCount = modAppliedLocalizationEntryCount,
                    AppliedBalancePatchCount = modAppliedBalancePatchCount,
                    AppliedCameraPatchCount = modAppliedCameraPatchCount,
                    AppliedVisualPatchCount = modAppliedVisualPatchCount,
                    AppliedDefinitionTypes = state.AppliedDefinitionTypes,
                    AppliedDefinitionIds = state.AppliedDefinitionIds,
                    AppliedLocales = state.AppliedLocales,
                    Warnings = modWarnings
                };
            }
        }

        return new AppliedContentState
        {
            Mods = appliedMods,
            Localization = localization.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyDictionary<string, string>)kv.Value,
                StringComparer.OrdinalIgnoreCase),
            BalancePatches = balancePatches,
            CameraPatches = cameraPatches,
            VisualPatches = visualPatches,
            IntroSkips = introSkips,
            UiScales = uiScales,
            AppliedModCount = appliedMods.Values.Count(v => v.AppliedDefinitionCount > 0),
            AppliedDefinitionCount = appliedDefinitionCount,
            AppliedLocalizationDefinitionCount = appliedLocalizationDefinitionCount,
            AppliedLocalizationEntryCount = appliedLocalizationEntryCount,
            AppliedBalancePatchCount = appliedBalancePatchCount,
            AppliedCameraPatchCount = appliedCameraPatchCount,
            AppliedVisualPatchCount = appliedVisualPatchCount,
            AppliedIntroSkipCount = appliedIntroSkipCount
        };
    }

    /// <summary>
    /// Returns <c>true</c> for visual definition target values the loader knows how to apply.
    /// </summary>
    private static bool IsVisualTargetSupported(string target)
    {
        return string.Equals(target, "player.baseSprite", StringComparison.OrdinalIgnoreCase)
               || string.Equals(target, "enemy.baseSprite", StringComparison.OrdinalIgnoreCase)
               || target.StartsWith("gameobject.named:", StringComparison.OrdinalIgnoreCase)
               || target.StartsWith("sprite.named:", StringComparison.OrdinalIgnoreCase);
    }
}
