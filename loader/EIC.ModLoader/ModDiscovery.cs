using System.Text.Json;
using System.Text.RegularExpressions;
using BepInEx.Logging;

namespace EIC.ModLoader;

public static class ModDiscovery
{
    private const string TargetGameName = "Everything is Crab";
    private const string TargetUnityVersion = "6000.2.15f1";
    private const string TargetLoaderMajor = "1";
    private static readonly HashSet<string> AppliedDefinitionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "localization",
        "balancePatch",
        "cameraPatch",
        "visual",
        "introSkip",
        "uiScale"
    };

    private static readonly HashSet<string> DiscoveredOnlyDefinitionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "evolution",
        "enemy"
    };
    private static readonly Regex ModIdPattern = new("^[A-Za-z0-9]+(?:[._-][A-Za-z0-9]+)+$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ModDiscoveryResult Run(
        string gameRoot,
        SafeModeState safeMode,
        IReadOnlyList<ModToggleCommand> startupToggles,
        int cliToggleCount,
        int queuedToggleCount,
        int queueDedupedCount,
        bool queueFileConsumed,
        ManualLogSource log)
    {
        var modsRoot = Path.Combine(gameRoot, "Mods");
        var disabledRoot = Path.Combine(modsRoot, "_disabled");
        var statePath = Path.Combine(modsRoot, "mod-state.json");

        Directory.CreateDirectory(modsRoot);
        Directory.CreateDirectory(disabledRoot);

        var stateStore = new ModStateStore(statePath);
        var state = stateStore.Load();

        var entries = DiscoverManifestEntries(modsRoot, disabledRoot, log);
        var discoveredIds = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Manifest?.Id))
            .Select(e => e.Manifest!.Id!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownToggleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var toggle in startupToggles)
        {
            if (!string.IsNullOrWhiteSpace(toggle.ModId) && !discoveredIds.Contains(toggle.ModId))
            {
                unknownToggleIds.Add(toggle.ModId);
                var source = string.IsNullOrWhiteSpace(toggle.Source) ? "unknown" : toggle.Source;
                log.LogWarning(
                    $"Startup toggle ignored for unknown mod id '{toggle.ModId}'. Source={source}, RequestedState={(toggle.Enabled ? "enabled" : "disabled")}.");
            }
        }

        var duplicateIds = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Manifest?.Id))
            .GroupBy(e => e.Manifest!.Id!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            ValidateEntry(entry, duplicateIds);
        }

        var dependencyValidEntries = entries.Where(e => e.Errors.Count == 0).ToList();
        var dependencyGraph = dependencyValidEntries.ToDictionary(
            e => e.Manifest!.Id!,
            e => e.Manifest!.Dependencies ?? new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in dependencyValidEntries)
        {
            foreach (var dep in entry.Manifest!.Dependencies ?? new List<string>())
            {
                if (!dependencyGraph.ContainsKey(dep))
                {
                    entry.Errors.Add($"Missing dependency '{dep}'.");
                }
            }
        }

        var loadOrder = ResolveLoadOrder(dependencyValidEntries.Where(e => e.Errors.Count == 0).ToList());

        var existingState = new Dictionary<string, ModStateEntry>(state.Mods, StringComparer.OrdinalIgnoreCase);
        state.Mods.Clear();
        state.SafeModeActive = safeMode.IsActive;
        state.SafeModeReason = safeMode.Reason;
        state.LastRunUtc = DateTimeOffset.UtcNow.ToString("o");

        var effectiveEnabledCount = 0;
        var restartRequiredCount = 0;
        var restartRequiredMods = new List<string>();
        var definitionFileCount = 0;
        var localizationDefinitionCount = 0;
        var balancePatchDefinitionCount = 0;
        var cameraPatchDefinitionCount = 0;
        var visualDefinitionCount = 0;
        foreach (var entry in entries)
        {
            var manifest = entry.Manifest;
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id))
            {
                continue;
            }

            var id = manifest.Id;

            var isErrored = entry.Errors.Count > 0;
            var hasPreviousState = existingState.TryGetValue(id, out var previousState);
            var desiredEnabled = hasPreviousState
                ? previousState!.Enabled
                : manifest.DefaultEnabled != false;

            var toggle = startupToggles.LastOrDefault(t => string.Equals(t.ModId, id, StringComparison.OrdinalIgnoreCase));
            var hasToggle = !string.IsNullOrWhiteSpace(toggle.ModId);
            if (hasToggle)
            {
                desiredEnabled = toggle.Enabled;
                log.LogInfo($"Startup toggle applied for '{id}': Enabled={desiredEnabled}");
            }

            var enabled = desiredEnabled;
            var effectiveEnabled = enabled && !isErrored && !safeMode.IsActive;
            if (effectiveEnabled)
            {
                effectiveEnabledCount++;
            }

            var definitionTypes = DiscoverDefinitionTypes(entry);
            var perModDefinitionFileCount = manifest.Content?.Definitions?.Count ?? 0;
            definitionFileCount += perModDefinitionFileCount;
            localizationDefinitionCount += definitionTypes.Count(t => string.Equals(t, "localization", StringComparison.OrdinalIgnoreCase));
            balancePatchDefinitionCount += definitionTypes.Count(t => string.Equals(t, "balancePatch", StringComparison.OrdinalIgnoreCase));
            cameraPatchDefinitionCount += definitionTypes.Count(t => string.Equals(t, "cameraPatch", StringComparison.OrdinalIgnoreCase));
            visualDefinitionCount += definitionTypes.Count(t => string.Equals(t, "visual", StringComparison.OrdinalIgnoreCase));

            var restartReasons = BuildRestartReasons(manifest, hasToggle);
            var restartRequired = restartReasons.Count > 0;
            if (restartRequired)
            {
                restartRequiredCount++;
                restartRequiredMods.Add(id);
                log.LogWarning(
                    $"Startup toggle for '{id}' is restart-sensitive: {string.Join(", ", restartReasons)}.");
            }

            var warnings = new List<string>(entry.Warnings);
            if (safeMode.IsActive && enabled)
            {
                warnings.Add($"Safe mode active ({safeMode.Reason}); mod will not be applied this launch.");
            }

            state.Mods[id] = new ModStateEntry
            {
                Name = manifest.Name,
                Author = manifest.Author,
                Description = manifest.Description,
                Enabled = enabled,
                EffectiveEnabled = effectiveEnabled,
                RestartRequired = restartRequired,
                RestartReasons = restartReasons,
                DefinitionFileCount = perModDefinitionFileCount,
                DefinitionTypes = definitionTypes,
                Status = isErrored ? "errored" : "ready",
                Errors = entry.Errors,
                Warnings = warnings,
                StartupToggleApplied = hasToggle,
                StartupRequestedEnabled = hasToggle ? desiredEnabled : null,
                StartupToggleSource = hasToggle ? (string.IsNullOrWhiteSpace(toggle.Source) ? "unknown" : toggle.Source!) : string.Empty,
                PendingStartupQueuedEnabled = null,
                PendingStartupQueuedSource = string.Empty,
                Version = manifest.Version,
                FolderName = entry.FolderName
            };

            if (isErrored)
            {
                log.LogWarning($"Mod '{id}' skipped due to validation errors: {string.Join(" | ", entry.Errors)}");
            }
            else
            {
                log.LogInfo($"Mod '{id}' validated and queued. Enabled={enabled}");
            }

            if (warnings.Count > 0)
            {
                foreach (var warning in warnings)
                {
                    log.LogWarning($"Mod '{id}' warning: {warning}");
                }
            }

            if (definitionTypes.Count > 0)
            {
                log.LogInfo($"Mod '{id}' definition types detected: {string.Join(", ", definitionTypes)}");
            }
        }

        // Conflict detection: runs after state is built, before content/DLL loading.
        // Explicit blocking may flip some EffectiveEnabled values to false.
        var conflictResult = ModConflictDetector.Detect(entries, loadOrder, state.Mods, log);
        foreach (var blockedId in conflictResult.BlockedModIds.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
        {
            if (state.Mods.TryGetValue(blockedId, out var blockedState) && blockedState.EffectiveEnabled)
            {
                blockedState.EffectiveEnabled = false;
                effectiveEnabledCount--;
                if (!string.Equals(blockedState.Status, "errored", StringComparison.OrdinalIgnoreCase))
                {
                    blockedState.Status = "conflictBlocked";
                }

                log.LogWarning($"Mod '{blockedId}' is conflict-blocked and will not be applied this launch.");
            }
        }

        foreach (var (affectedId, modConflicts) in conflictResult.ByMod
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!state.Mods.TryGetValue(affectedId, out var affectedState))
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

        var appliedContent = ContentPipeline.Apply(entries, loadOrder, state.Mods, log);
        AssetBundleApplicator.LoadForMods(entries, loadOrder, state.Mods, log);
        var entryById = entries
            .Where(e => e.Manifest?.Id is not null)
            .ToDictionary(e => e.Manifest!.Id!, e => e, StringComparer.OrdinalIgnoreCase);
        var dllLoad = DllModLoader.LoadAndInvoke(loadOrder, entryById, state.Mods, log);
        LiveModRegistry.Initialize(entryById, loadOrder, state.Mods, statePath, safeMode.IsActive);
        RuntimeContentState.Set(appliedContent);
        var resolvedFinalIntents = startupToggles
            .Where(t => !string.IsNullOrWhiteSpace(t.ModId) && discoveredIds.Contains(t.ModId))
            .GroupBy(t => t.ModId, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var winner = g.Last();
                var winnerSource = string.IsNullOrWhiteSpace(winner.Source) ? "unknown" : winner.Source;
                return $"{g.Key}={(winner.Enabled ? "enable" : "disable")}({winnerSource})";
            })
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

        state.StartupToggles = new StartupToggleSummary
        {
            ProcessedAtUtc = DateTimeOffset.UtcNow.ToString("o"),
            QueueFileConsumed = queueFileConsumed,
            CliToggleCount = Math.Max(0, cliToggleCount),
            QueuedToggleCount = Math.Max(0, queuedToggleCount),
            QueueDedupedCount = Math.Max(0, queueDedupedCount),
            TotalToggleCount = startupToggles.Count,
            UnknownToggleCount = unknownToggleIds.Count,
            UnknownModIds = unknownToggleIds.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList(),
            ResolvedModCount = resolvedFinalIntents.Count,
            ResolvedFinalIntents = resolvedFinalIntents
        };

        state.ContentApply = new ContentApplySummary
        {
            AppliedModCount = appliedContent.AppliedModCount,
            AppliedDefinitionCount = appliedContent.AppliedDefinitionCount,
            AppliedLocalizationDefinitionCount = appliedContent.AppliedLocalizationDefinitionCount,
            AppliedLocalizationEntryCount = appliedContent.AppliedLocalizationEntryCount,
            AppliedBalancePatchCount = appliedContent.AppliedBalancePatchCount,
            AppliedCameraPatchCount = appliedContent.AppliedCameraPatchCount,
            AppliedVisualPatchCount = appliedContent.AppliedVisualPatchCount,
            AppliedIntroSkipCount = appliedContent.AppliedIntroSkipCount,
            DllScannedMods = dllLoad.ScannedModCount,
            DllLoadedMods = dllLoad.LoadedModCount,
            DllErroredMods = dllLoad.ErroredModCount,
            DllDeclaredEntrypoints = dllLoad.DeclaredEntrypointCount,
            DllLoadedEntrypoints = dllLoad.LoadedEntrypointCount
        };

        stateStore.Save(state);

        return new ModDiscoveryResult(
            entries.Count,
            entries.Count(e => e.Errors.Count == 0),
            entries.Count(e => e.Errors.Count > 0),
            effectiveEnabledCount,
            restartRequiredCount,
            restartRequiredMods,
            definitionFileCount,
            localizationDefinitionCount,
            balancePatchDefinitionCount,
            cameraPatchDefinitionCount,
            visualDefinitionCount,
            appliedContent.AppliedModCount,
            appliedContent.AppliedDefinitionCount,
            appliedContent.AppliedLocalizationDefinitionCount,
            appliedContent.AppliedLocalizationEntryCount,
            appliedContent.AppliedBalancePatchCount,
            appliedContent.AppliedCameraPatchCount,
            appliedContent.AppliedVisualPatchCount,
            appliedContent.AppliedIntroSkipCount,
            dllLoad.ScannedModCount,
            dllLoad.LoadedModCount,
            dllLoad.ErroredModCount,
            dllLoad.DeclaredEntrypointCount,
            dllLoad.LoadedEntrypointCount,
            state.StartupToggles.TotalToggleCount,
            state.StartupToggles.CliToggleCount,
            state.StartupToggles.QueuedToggleCount,
            state.StartupToggles.QueueDedupedCount,
            state.StartupToggles.UnknownToggleCount,
            state.StartupToggles.ResolvedModCount,
            safeMode.IsActive,
            loadOrder);
    }

    private static List<DiscoveredModEntry> DiscoverManifestEntries(string modsRoot, string disabledRoot, ManualLogSource log)
    {
        var list = new List<DiscoveredModEntry>();

        foreach (var dir in Directory.EnumerateDirectories(modsRoot).OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
        {
            var folderName = Path.GetFileName(dir);
            if (string.Equals(dir.TrimEnd('\\'), disabledRoot.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var manifestPath = Path.Combine(dir, "eicmod.json");
            if (!File.Exists(manifestPath))
            {
                log.LogWarning($"Skipping folder '{folderName}' because eicmod.json is missing.");
                continue;
            }

            var entry = new DiscoveredModEntry
            {
                FolderName = folderName,
                DirectoryPath = dir,
                ManifestPath = manifestPath
            };

            try
            {
                var json = File.ReadAllText(manifestPath);
                entry.Manifest = JsonSerializer.Deserialize<ModManifest>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                entry.Errors.Add($"Manifest parse error: {ex.Message}");
            }

            list.Add(entry);
        }

        return list;
    }

    private static void ValidateEntry(DiscoveredModEntry entry, HashSet<string> duplicateIds)
    {
        var manifest = entry.Manifest;
        if (manifest is null)
        {
            if (entry.Errors.Count == 0)
            {
                entry.Errors.Add("Manifest is null after parse.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            entry.Errors.Add("Missing required field 'id'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            entry.Errors.Add("Missing required field 'name'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            entry.Errors.Add("Missing required field 'version'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Author))
        {
            entry.Errors.Add("Missing required field 'author'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Description))
        {
            entry.Errors.Add("Missing required field 'description'.");
        }

        if (!string.IsNullOrWhiteSpace(manifest.Id) && !ModIdPattern.IsMatch(manifest.Id))
        {
            entry.Errors.Add("Field 'id' must be reverse-domain style (for example: author.mod-id).");
        }

        if (!string.Equals(manifest.TargetGame, TargetGameName, StringComparison.OrdinalIgnoreCase))
        {
            entry.Errors.Add($"targetGame must be '{TargetGameName}'.");
        }

        if (!string.IsNullOrWhiteSpace(manifest.TargetUnity) &&
            !string.Equals(manifest.TargetUnity, TargetUnityVersion, StringComparison.OrdinalIgnoreCase))
        {
            entry.Warnings.Add(
                $"targetUnity '{manifest.TargetUnity}' differs from expected '{TargetUnityVersion}'.");
        }

        if (!string.IsNullOrWhiteSpace(manifest.Id) && duplicateIds.Contains(manifest.Id))
        {
            entry.Errors.Add("Duplicate mod id detected.");
        }

        if (string.IsNullOrWhiteSpace(manifest.LoaderVersion))
        {
            entry.Errors.Add("Missing required field 'loaderVersion'.");
        }
        else if (!manifest.LoaderVersion.StartsWith($"{TargetLoaderMajor}.", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(manifest.LoaderVersion, $"{TargetLoaderMajor}.x", StringComparison.OrdinalIgnoreCase))
        {
            entry.Warnings.Add(
                $"loaderVersion '{manifest.LoaderVersion}' is outside expected major '{TargetLoaderMajor}.x'.");
        }

        if (manifest.Content is null)
        {
            entry.Errors.Add("Missing required field 'content'.");
        }

        ValidateRelativePaths(entry, "catalog", manifest.Content?.Catalogs);
        ValidateRelativePaths(entry, "assetBundle", manifest.Content?.AssetBundles);
        ValidateRelativePaths(entry, "definition", manifest.Content?.Definitions);

        foreach (var assembly in manifest.Assemblies ?? new List<string>())
        {
            if (!TryResolvePathUnderModRoot(entry.DirectoryPath, assembly, out var resolved, out var reason))
            {
                entry.Errors.Add($"Referenced assembly path is invalid: {assembly} ({reason})");
                continue;
            }

            if (!File.Exists(resolved))
            {
                entry.Errors.Add($"Referenced assembly is missing: {assembly}");
            }
        }

        var entryPoints = manifest.EntryPoints ?? new List<string>();
        foreach (var entryPoint in entryPoints)
        {
            if (string.IsNullOrWhiteSpace(entryPoint))
            {
                entry.Errors.Add("Entrypoint type name is empty.");
            }
        }

        var hasAssemblies = (manifest.Assemblies?.Count ?? 0) > 0;
        var hasEntryPoints = entryPoints.Count > 0;
        if (hasAssemblies != hasEntryPoints)
        {
            entry.Errors.Add("DLL mods must declare both 'assemblies' and 'entrypoints'.");
        }
    }

    private static void ValidateRelativePaths(DiscoveredModEntry entry, string kind, List<string>? paths)
    {
        foreach (var relativePath in paths ?? new List<string>())
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                entry.Errors.Add($"{kind} path is empty.");
                continue;
            }

            if (!TryResolvePathUnderModRoot(entry.DirectoryPath, relativePath, out var resolved, out var reason))
            {
                entry.Errors.Add($"{kind} path is invalid '{relativePath}': {reason}");
                continue;
            }

            if (!File.Exists(resolved))
            {
                entry.Errors.Add($"Referenced {kind} file is missing: {relativePath}");
            }
        }
    }

    private static bool TryResolvePathUnderModRoot(string modDirectory, string relativePath, out string resolvedPath, out string reason)
    {
        resolvedPath = string.Empty;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            reason = "path is empty";
            return false;
        }

        if (Path.IsPathRooted(relativePath))
        {
            reason = "path must be relative";
            return false;
        }

        if (relativePath.Contains("..", StringComparison.Ordinal))
        {
            reason = "path cannot traverse parent directories";
            return false;
        }

        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullModRoot = Path.GetFullPath(modDirectory);
        var candidate = Path.GetFullPath(Path.Combine(fullModRoot, normalized));
        var rootPrefix = fullModRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            reason = "resolved path escaped mod folder";
            return false;
        }

        resolvedPath = candidate;
        return true;
    }

    private static List<string> BuildRestartReasons(ModManifest manifest, bool hasToggle)
    {
        if (!hasToggle)
        {
            return new List<string>();
        }

        var reasons = new List<string>();
        if ((manifest.Assemblies?.Count ?? 0) > 0)
        {
            reasons.Add("code_assemblies");
        }

        if ((manifest.Content?.Catalogs?.Count ?? 0) > 0)
        {
            reasons.Add("addressables_catalogs");
        }

        if ((manifest.Content?.AssetBundles?.Count ?? 0) > 0)
        {
            reasons.Add("asset_bundles");
        }

        return reasons;
    }

    private static List<string> DiscoverDefinitionTypes(DiscoveredModEntry entry)
    {
        var manifest = entry.Manifest;
        var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relativePath in manifest?.Content?.Definitions ?? new List<string>())
        {
            var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var resolved = Path.Combine(entry.DirectoryPath, normalized);
            if (!File.Exists(resolved))
            {
                continue;
            }

            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText(resolved));
                if (!json.RootElement.TryGetProperty("type", out var typeProperty) ||
                    typeProperty.ValueKind != JsonValueKind.String)
                {
                    entry.Warnings.Add($"Definition file '{relativePath}' is missing string field 'type'.");
                    continue;
                }

                var definitionType = typeProperty.GetString();
                if (string.IsNullOrWhiteSpace(definitionType))
                {
                    entry.Warnings.Add($"Definition file '{relativePath}' has empty 'type'.");
                    continue;
                }

                discovered.Add(definitionType);
                if (AppliedDefinitionTypes.Contains(definitionType))
                {
                    continue;
                }

                if (DiscoveredOnlyDefinitionTypes.Contains(definitionType))
                {
                    entry.Warnings.Add(
                        $"Definition file '{relativePath}' uses recognized future type '{definitionType}'. This type is discovered for planning/prototyping but has no runtime applicator yet.");
                    continue;
                }

                entry.Warnings.Add($"Definition file '{relativePath}' uses unsupported type '{definitionType}'.");
            }
            catch (Exception ex)
            {
                entry.Warnings.Add($"Definition file '{relativePath}' parse error: {ex.Message}");
            }
        }

        return discovered.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> ResolveLoadOrder(List<DiscoveredModEntry> validEntries)
    {
        var idToEntry = validEntries
            .Where(e => e.Manifest?.Id is not null)
            .ToDictionary(e => e.Manifest!.Id!, e => e, StringComparer.OrdinalIgnoreCase);

        var indegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var edges = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in idToEntry.Keys)
        {
            indegree[id] = 0;
            edges[id] = new List<string>();
        }

        foreach (var entry in validEntries)
        {
            var id = entry.Manifest!.Id!;
            foreach (var dep in entry.Manifest.Dependencies ?? new List<string>())
            {
                if (!idToEntry.ContainsKey(dep))
                {
                    continue;
                }

                edges[dep].Add(id);
                indegree[id] = indegree[id] + 1;
            }
        }

        foreach (var adjacency in edges.Values)
        {
            adjacency.Sort(StringComparer.OrdinalIgnoreCase);
        }

        var queue = new Queue<string>(indegree
            .Where(kv => kv.Value == 0)
            .Select(kv => kv.Key)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase));
        var ordered = new List<string>(idToEntry.Count);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            ordered.Add(current);

            foreach (var next in edges[current])
            {
                indegree[next] = indegree[next] - 1;
                if (indegree[next] == 0)
                {
                    queue.Enqueue(next);
                }
            }
        }

        if (ordered.Count != idToEntry.Count)
        {
            var unresolved = idToEntry.Keys
                .Except(ordered, StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var id in unresolved)
            {
                idToEntry[id].Errors.Add("Dependency cycle detected.");
            }

            ordered.RemoveAll(id => unresolved.Contains(id, StringComparer.OrdinalIgnoreCase));
        }

        return ordered;
    }
}

public sealed class DiscoveredModEntry
{
    public string FolderName { get; set; } = string.Empty;
    public string DirectoryPath { get; set; } = string.Empty;
    public string ManifestPath { get; set; } = string.Empty;
    public ModManifest? Manifest { get; set; }
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
}

public sealed class ModDiscoveryResult
{
    public ModDiscoveryResult(
        int scannedCount,
        int validCount,
        int erroredCount,
        int effectiveEnabledCount,
        int restartRequiredCount,
        IReadOnlyList<string> restartRequiredMods,
        int definitionFileCount,
        int localizationDefinitionCount,
        int balancePatchDefinitionCount,
        int cameraPatchDefinitionCount,
        int visualDefinitionCount,
        int appliedModCount,
        int appliedDefinitionCount,
        int appliedLocalizationDefinitionCount,
        int appliedLocalizationEntryCount,
        int appliedBalancePatchCount,
        int appliedCameraPatchCount,
        int appliedVisualPatchCount,
        int appliedIntroSkipCount,
        int dllScannedModCount,
        int dllLoadedModCount,
        int dllErroredModCount,
        int dllDeclaredEntrypointCount,
        int dllLoadedEntrypointCount,
        int startupTotalToggleCount,
        int startupCliToggleCount,
        int startupQueuedToggleCount,
        int startupQueueDedupedCount,
        int startupUnknownToggleCount,
        int startupResolvedModCount,
        bool safeModeActive,
        IReadOnlyList<string> loadOrder)
    {
        ScannedCount = scannedCount;
        ValidCount = validCount;
        ErroredCount = erroredCount;
        EffectiveEnabledCount = effectiveEnabledCount;
        RestartRequiredCount = restartRequiredCount;
        RestartRequiredMods = restartRequiredMods;
        DefinitionFileCount = definitionFileCount;
        LocalizationDefinitionCount = localizationDefinitionCount;
        BalancePatchDefinitionCount = balancePatchDefinitionCount;
        CameraPatchDefinitionCount = cameraPatchDefinitionCount;
        VisualDefinitionCount = visualDefinitionCount;
        AppliedModCount = appliedModCount;
        AppliedDefinitionCount = appliedDefinitionCount;
        AppliedLocalizationDefinitionCount = appliedLocalizationDefinitionCount;
        AppliedLocalizationEntryCount = appliedLocalizationEntryCount;
        AppliedBalancePatchCount = appliedBalancePatchCount;
        AppliedCameraPatchCount = appliedCameraPatchCount;
        AppliedVisualPatchCount = appliedVisualPatchCount;
        AppliedIntroSkipCount = appliedIntroSkipCount;
        DllScannedModCount = dllScannedModCount;
        DllLoadedModCount = dllLoadedModCount;
        DllErroredModCount = dllErroredModCount;
        DllDeclaredEntrypointCount = dllDeclaredEntrypointCount;
        DllLoadedEntrypointCount = dllLoadedEntrypointCount;
        StartupTotalToggleCount = startupTotalToggleCount;
        StartupCliToggleCount = startupCliToggleCount;
        StartupQueuedToggleCount = startupQueuedToggleCount;
        StartupQueueDedupedCount = startupQueueDedupedCount;
        StartupUnknownToggleCount = startupUnknownToggleCount;
        StartupResolvedModCount = startupResolvedModCount;
        SafeModeActive = safeModeActive;
        LoadOrder = loadOrder;
    }

    public int ScannedCount { get; }
    public int ValidCount { get; }
    public int ErroredCount { get; }
    public int EffectiveEnabledCount { get; }
    public int RestartRequiredCount { get; }
    public IReadOnlyList<string> RestartRequiredMods { get; }
    public int DefinitionFileCount { get; }
    public int LocalizationDefinitionCount { get; }
    public int BalancePatchDefinitionCount { get; }
    public int CameraPatchDefinitionCount { get; }
    public int VisualDefinitionCount { get; }
    public int AppliedModCount { get; }
    public int AppliedDefinitionCount { get; }
    public int AppliedLocalizationDefinitionCount { get; }
    public int AppliedLocalizationEntryCount { get; }
    public int AppliedBalancePatchCount { get; }
    public int AppliedCameraPatchCount { get; }
    public int AppliedVisualPatchCount { get; }
    public int AppliedIntroSkipCount { get; }
    public int DllScannedModCount { get; }
    public int DllLoadedModCount { get; }
    public int DllErroredModCount { get; }
    public int DllDeclaredEntrypointCount { get; }
    public int DllLoadedEntrypointCount { get; }
    public int StartupTotalToggleCount { get; }
    public int StartupCliToggleCount { get; }
    public int StartupQueuedToggleCount { get; }
    public int StartupQueueDedupedCount { get; }
    public int StartupUnknownToggleCount { get; }
    public int StartupResolvedModCount { get; }
    public bool SafeModeActive { get; }
    public IReadOnlyList<string> LoadOrder { get; }
}
