using BepInEx.Logging;

namespace EIC.ModLoader;

/// <summary>
/// Caches the post-bootstrap mod discovery state so the Mods settings window can
/// perform instant (no-restart) content re-apply for declarative-only mods.
///
/// Hot-toggle is only attempted when a mod carries no assemblies and no Addressable
/// catalogs; those require a full restart because .NET/IL2CPP cannot unload assemblies
/// and catalogues are not safely unloadable at runtime.
///
/// Even for eligible mods the hot-toggle is a best-effort operation.  Balance patches
/// and localization entries are applied to newly-created objects / fresh look-ups;
/// already-patched live objects keep their current values until the next scene reload
/// or object respawn.  Camera and visual patches re-apply every frame via the existing
/// per-frame applicators and therefore take effect within one tick.
/// </summary>
public static class LiveModRegistry
{
    private static IReadOnlyDictionary<string, DiscoveredModEntry> _entryById =
        new Dictionary<string, DiscoveredModEntry>(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string> _loadOrder = Array.Empty<string>();

    // Mutable in-memory copy of mod-state; shared with the discovery run that initialised us.
    private static Dictionary<string, ModStateEntry> _modState =
        new(StringComparer.OrdinalIgnoreCase);

    private static string _statePath = string.Empty;
    private static bool _safeModeActive;
    private static bool _initialized;

    public static bool IsInitialized => _initialized;

    /// <summary>
    /// Called once at the end of <see cref="ModDiscovery.Run"/> to hand over state.
    /// </summary>
    public static void Initialize(
        IReadOnlyDictionary<string, DiscoveredModEntry> entryById,
        IReadOnlyList<string> loadOrder,
        Dictionary<string, ModStateEntry> modState,
        string statePath,
        bool safeModeActive)
    {
        _entryById = entryById;
        _loadOrder = loadOrder;
        _modState = modState;
        _statePath = statePath;
        _safeModeActive = safeModeActive;
        _initialized = true;
        
        var log = BepInEx.Logging.Logger.CreateLogSource("Crabtya");
        log.LogInfo($"LiveModRegistry: initialized. Entries={entryById.Count}, LoadOrder={loadOrder.Count}, SafeMode={safeModeActive}");
    }

    /// <summary>
    /// Returns <c>true</c> when toggling this mod requires a restart.
    /// Mods with assemblies or Addressable catalogs always require restart.
    /// </summary>
    public static bool RequiresRestart(string modId)
    {
        if (!_entryById.TryGetValue(modId, out var entry) || entry.Manifest is null)
        {
            return true;
        }

        return (entry.Manifest.Assemblies?.Count ?? 0) > 0
               || (entry.Manifest.Content?.Catalogs?.Count ?? 0) > 0
               || (entry.Manifest.Content?.AssetBundles?.Count ?? 0) > 0;
    }

    /// <summary>
    /// Attempts an instant content re-apply without requiring a restart.
    /// Only valid for declarative-only mods (no assemblies, no catalogs).
    /// Returns <c>true</c> on success.
    /// </summary>
    public static bool TryHotToggle(string modId, bool enabled, out string failReason)
    {
        failReason = string.Empty;

        if (!_initialized)
        {
            failReason = "LiveModRegistry not initialised.";
            return false;
        }

        if (RequiresRestart(modId))
        {
            failReason = "Mod requires restart (assemblies or catalogs declared).";
            return false;
        }

        if (!_modState.TryGetValue(modId, out var entry))
        {
            failReason = $"Mod '{modId}' not found in live state.";
            return false;
        }

        var log = Plugin.Instance?.Log;

        try
        {
            // 1. Update this mod's enabled state.
            entry.Enabled = enabled;
            entry.EffectiveEnabled = enabled && entry.Errors.Count == 0 && !_safeModeActive;

            // 2. Reset conflict fields for all mods so stale notices don't accumulate.
            //    Previously conflict-blocked content mods are restored to their natural enabled state.
            ResetConflictState();

            // 3. Re-run conflict detection across the updated enabled set.
            var conflictResult = ModConflictDetector.Detect(_entryById.Values, _loadOrder, _modState, log!);

            // 4. Apply explicit blocking.
            foreach (var blockedId in conflictResult.BlockedModIds.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
            {
                if (_modState.TryGetValue(blockedId, out var blockedState) && blockedState.EffectiveEnabled)
                {
                    blockedState.EffectiveEnabled = false;
                    if (!string.Equals(blockedState.Status, "errored", StringComparison.OrdinalIgnoreCase))
                    {
                        blockedState.Status = "conflictBlocked";
                    }
                }
            }

            // 5. Apply conflict notices to all affected mods.
            foreach (var (affectedId, modConflicts) in conflictResult.ByMod
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!_modState.TryGetValue(affectedId, out var affectedState))
                {
                    continue;
                }

                foreach (var conflict in modConflicts)
                {
                    affectedState.ConflictNotices.Add(conflict.Message);
                }

                var worstSeverity = modConflicts.Any(c => string.Equals(c.Severity, "blocked", StringComparison.OrdinalIgnoreCase))
                    ? "blocked"
                    : modConflicts.Any(c => string.Equals(c.Severity, "warning", StringComparison.OrdinalIgnoreCase))
                        ? "warning"
                        : "info";

                if (!string.Equals(affectedState.ConflictStatus, "blocked", StringComparison.OrdinalIgnoreCase))
                {
                    affectedState.ConflictStatus = worstSeverity;
                }
            }

            // 6. Re-build applied content from the updated (post-conflict) mod state.
            var applied = ContentPipeline.Apply(_entryById.Values, _loadOrder, _modState, log!);
            RuntimeContentState.Set(applied);

            // 7. Persist all mod state so the Mods window sees updated conflict notices on Refresh().
            PersistAllModState(log);

            log?.LogInfo($"LiveModRegistry: hot-toggled '{modId}' Enabled={enabled}.");
            return true;
        }
        catch (Exception ex)
        {
            failReason = ex.Message;
            log?.LogWarning($"LiveModRegistry: hot-toggle failed for '{modId}': {ex}");
            return false;
        }
    }

    /// <summary>
    /// Clears all conflict-related fields on every in-memory mod state entry so a
    /// fresh conflict detection pass starts from a clean slate.  Any mod that was
    /// previously marked <c>conflictBlocked</c> has its effective-enabled state
    /// restored to the natural <c>Enabled &amp;&amp; no-errors &amp;&amp; no-safe-mode</c> value.
    /// </summary>
    private static void ResetConflictState()
    {
        foreach (var (_, state) in _modState)
        {
            state.ConflictNotices.Clear();
            state.ConflictStatus = "none";

            // Restore mods that were blocked by a previous conflict run.
            if (string.Equals(state.Status, "conflictBlocked", StringComparison.OrdinalIgnoreCase))
            {
                state.Status = state.Errors.Count > 0 ? "errored" : "ready";
                state.EffectiveEnabled = state.Enabled && state.Errors.Count == 0 && !_safeModeActive;
            }
        }
    }

    /// <summary>
    /// Persists the current in-memory state for ALL mods to disk.  Used after a
    /// hot-toggle so conflict notices (which may affect several mods at once) are
    /// visible to the Mods settings window on its next Refresh().
    /// </summary>
    private static void PersistAllModState(ManualLogSource? log)
    {
        if (string.IsNullOrWhiteSpace(_statePath))
        {
            return;
        }

        try
        {
            var store = new ModStateStore(_statePath);
            var file = store.Load();

            foreach (var (modId, updated) in _modState)
            {
                if (file.Mods.TryGetValue(modId, out var existing))
                {
                    existing.Enabled = updated.Enabled;
                    existing.EffectiveEnabled = updated.EffectiveEnabled;
                    existing.Status = updated.Status;
                    existing.ConflictStatus = updated.ConflictStatus;
                    existing.ConflictNotices = updated.ConflictNotices;
                    existing.AppliedDefinitionCount = updated.AppliedDefinitionCount;
                    existing.AppliedLocalizationEntryCount = updated.AppliedLocalizationEntryCount;
                    existing.AppliedBalancePatchCount = updated.AppliedBalancePatchCount;
                    existing.AppliedCameraPatchCount = updated.AppliedCameraPatchCount;
                    existing.AppliedVisualPatchCount = updated.AppliedVisualPatchCount;
                    existing.AppliedDefinitionTypes = updated.AppliedDefinitionTypes;
                    existing.AppliedDefinitionIds = updated.AppliedDefinitionIds;
                    existing.AppliedLocales = updated.AppliedLocales;
                }
                else
                {
                    file.Mods[modId] = updated;
                }
            }

            store.Save(file);
        }
        catch (Exception ex)
        {
            log?.LogWarning($"LiveModRegistry: failed to persist full mod state: {ex.Message}");
        }
    }
}
