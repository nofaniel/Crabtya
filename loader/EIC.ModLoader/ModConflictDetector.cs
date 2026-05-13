using System.Text.Json;
using BepInEx.Logging;

namespace EIC.ModLoader;

/// <summary>
/// Describes a single conflict notice affecting a mod.
/// </summary>
public sealed class ModConflict
{
    /// <summary>Mod id that receives this notice.</summary>
    public string AffectedModId { get; init; } = string.Empty;

    /// <summary>Other mod id involved in the conflict.</summary>
    public string ConflictingModId { get; init; } = string.Empty;

    /// <summary>Machine label for the conflict source: "explicit_conflict" or "definition_overlap".</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>"info", "warning", or "blocked".</summary>
    public string Severity { get; init; } = "info";

    /// <summary>Human-readable description of the conflict.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Result of a conflict-detection pass.
/// </summary>
public sealed class ConflictDetectionResult
{
    public static ConflictDetectionResult Empty { get; } = new();

    /// <summary>All conflict notices grouped by the affected mod id.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ModConflict>> ByMod { get; init; } =
        new Dictionary<string, IReadOnlyList<ModConflict>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Mod ids that must be prevented from loading due to a blocking conflict.</summary>
    public IReadOnlySet<string> BlockedModIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Detects conflicts between mods:
///   1. Explicit conflicts declared in manifests via the "conflicts" field.
///   2. Silent definition-ID overwrites (two mods claim the same definition id for the same surface type).
/// </summary>
public static class ModConflictDetector
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static ConflictDetectionResult Detect(
        IEnumerable<DiscoveredModEntry> allEntries,
        IReadOnlyList<string> loadOrder,
        Dictionary<string, ModStateEntry> modState,
        ManualLogSource log)
    {
        // Work only with mods that are currently effectively-enabled.
        var enabledInOrder = loadOrder
            .Where(id => modState.TryGetValue(id, out var s) && s.EffectiveEnabled)
            .ToList();

        if (enabledInOrder.Count == 0)
        {
            return ConflictDetectionResult.Empty;
        }

        var entryById = allEntries
            .Where(e => e.Manifest?.Id is not null)
            .ToDictionary(e => e.Manifest!.Id!, e => e, StringComparer.OrdinalIgnoreCase);

        var byMod = new Dictionary<string, List<ModConflict>>(StringComparer.OrdinalIgnoreCase);
        var blockedModIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddConflict(ModConflict conflict)
        {
            if (!byMod.TryGetValue(conflict.AffectedModId, out var list))
            {
                list = new List<ModConflict>();
                byMod[conflict.AffectedModId] = list;
            }

            list.Add(conflict);
        }

        // ----------------------------------------------------------------
        // Pass 1 — Explicit conflict declarations
        // When mod A declares conflicts: ["mod.b"] and both are enabled,
        // block whichever of the pair comes later in load order.
        // ----------------------------------------------------------------
        var loadOrderIndex = enabledInOrder
            .Select((id, i) => (id, i))
            .ToDictionary(t => t.id, t => t.i, StringComparer.OrdinalIgnoreCase);

        // Track pairs already handled to avoid double-processing when both sides declare the same conflict.
        var handledPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var modId in enabledInOrder)
        {
            if (!entryById.TryGetValue(modId, out var entry))
            {
                continue;
            }

            var conflicts = entry.Manifest?.Conflicts;
            if (conflicts is null || conflicts.Count == 0)
            {
                continue;
            }

            foreach (var conflictTarget in conflicts)
            {
                if (string.IsNullOrWhiteSpace(conflictTarget))
                {
                    continue;
                }

                // Only act if the conflict target is also currently enabled.
                if (!loadOrderIndex.ContainsKey(conflictTarget))
                {
                    continue;
                }

                // Deduplicate symmetrical declarations (A conflicts B, B conflicts A).
                var pairKey = string.Compare(modId, conflictTarget, StringComparison.OrdinalIgnoreCase) < 0
                    ? $"{modId}|{conflictTarget}"
                    : $"{conflictTarget}|{modId}";
                if (!handledPairs.Add(pairKey))
                {
                    continue;
                }

                var indexA = loadOrderIndex[modId];
                var indexB = loadOrderIndex[conflictTarget];
                var blockedId = indexA > indexB ? modId : conflictTarget;
                var survivingId = blockedId == modId ? conflictTarget : modId;

                blockedModIds.Add(blockedId);

                AddConflict(new ModConflict
                {
                    AffectedModId = blockedId,
                    ConflictingModId = survivingId,
                    Source = "explicit_conflict",
                    Severity = "blocked",
                    Message = $"Blocked: incompatible with '{survivingId}'. Both mods cannot be enabled simultaneously."
                });

                AddConflict(new ModConflict
                {
                    AffectedModId = survivingId,
                    ConflictingModId = blockedId,
                    Source = "explicit_conflict",
                    Severity = "info",
                    Message = $"Incompatible mod '{blockedId}' was blocked."
                });

                log.LogWarning(
                    $"Conflict: '{modId}' declares incompatibility with '{conflictTarget}'. Blocking '{blockedId}' (later in load order).");
            }
        }

        // ----------------------------------------------------------------
        // Pass 2 — Definition-ID overlap detection
        // For each enabled mod (including those that will be blocked by
        // explicit conflicts, so mod makers see the full picture), scan
        // definition files and track the first owner of each (type, id)
        // pair. When a second mod claims the same pair, warn both.
        // Localization is intentionally excluded: key merging is by design.
        // ----------------------------------------------------------------
        var definitionOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // key = "{type}:{id}" (lower-invariant), value = first owner mod id.

        foreach (var modId in enabledInOrder)
        {
            if (!entryById.TryGetValue(modId, out var entry))
            {
                continue;
            }

            foreach (var relativePath in entry.Manifest?.Content?.Definitions ?? new List<string>())
            {
                var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
                var resolved = Path.Combine(entry.DirectoryPath, normalized);
                if (!File.Exists(resolved))
                {
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(resolved));
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("type", out var typeProp) ||
                        typeProp.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var defType = typeProp.GetString();
                    if (string.IsNullOrWhiteSpace(defType))
                    {
                        continue;
                    }

                    // Localization uses locale+key merging by design — skip overlap detection.
                    if (string.Equals(defType, "localization", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!root.TryGetProperty("id", out var idProp) ||
                        idProp.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var defId = idProp.GetString();
                    if (string.IsNullOrWhiteSpace(defId))
                    {
                        continue;
                    }

                    var mapKey = $"{defType}:{defId}".ToLowerInvariant();
                    if (definitionOwners.TryGetValue(mapKey, out var firstOwner))
                    {
                        // Two different enabled mods both claim the same (type, id).
                        // Last writer wins at apply time; both get a warning.
                        AddConflict(new ModConflict
                        {
                            AffectedModId = firstOwner,
                            ConflictingModId = modId,
                            Source = "definition_overlap",
                            Severity = "warning",
                            Message = $"Definition overlap: {defType} '{defId}' is also declared by '{modId}'. That mod's value will apply (last-writer wins in load order)."
                        });

                        AddConflict(new ModConflict
                        {
                            AffectedModId = modId,
                            ConflictingModId = firstOwner,
                            Source = "definition_overlap",
                            Severity = "warning",
                            Message = $"Definition overlap: {defType} '{defId}' was already declared by '{firstOwner}'. This mod's value will override it (last-writer wins in load order)."
                        });

                        log.LogWarning(
                            $"Conflict: {defType} definition '{defId}' is claimed by both '{firstOwner}' and '{modId}'; '{modId}' will win (load order).");
                    }
                    else
                    {
                        definitionOwners[mapKey] = modId;
                    }
                }
                catch (Exception ex)
                {
                    log.LogWarning(
                        $"ModConflictDetector: failed to scan definition '{relativePath}' in mod '{modId}': {ex.Message}");
                }
            }
        }

        return new ConflictDetectionResult
        {
            ByMod = byMod.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<ModConflict>)kv.Value.AsReadOnly(),
                StringComparer.OrdinalIgnoreCase),
            BlockedModIds = blockedModIds
        };
    }
}
