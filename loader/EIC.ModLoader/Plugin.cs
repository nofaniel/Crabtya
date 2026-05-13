using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace EIC.ModLoader;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "eic.modloader";
    public const string PluginName = "Crabtya";
    public const string PluginVersion = "1.0.0";
    public static Plugin? Instance { get; private set; }

    public override void Load()
    {
        Instance = this;
        Log.LogInfo("Crabtya bootstrap loaded.");
        LogBuildFingerprint();

        var gameRoot = AppContext.BaseDirectory;
        var cliStartupCommands = StartupCommands.Parse(Environment.GetCommandLineArgs(), Log).ToList();
        var startupCommands = new List<ModToggleCommand>(cliStartupCommands);
        var queuedCommandPath = Path.Combine(gameRoot, "Mods", "startup-commands.json");
        var queueLoadResult = StartupCommandQueue.LoadAndClear(queuedCommandPath, Log);
        if (queueLoadResult.Toggles.Count > 0)
        {
            startupCommands.InsertRange(0, queueLoadResult.Toggles);
            Log.LogInfo($"Queued startup mod-toggle commands consumed: {queueLoadResult.Toggles.Count}");
        }

        var safeMode = SafeModeState.Detect(gameRoot);
        if (safeMode.IsActive)
        {
            Log.LogWarning($"Safe mode active. Reason={safeMode.Reason}");
        }

        if (startupCommands.Count > 0)
        {
            Log.LogInfo($"Startup mod-toggle commands detected: {startupCommands.Count}");
            LogResolvedStartupToggleOutcome(startupCommands);
        }

        var result = ModDiscovery.Run(
            gameRoot,
            safeMode,
            startupCommands,
            cliStartupCommands.Count,
            queueLoadResult.Toggles.Count,
            queueLoadResult.DedupedCount,
            queueLoadResult.QueueFileConsumed,
            Log);
        var loadOrder = result.LoadOrder.Count == 0 ? "<none>" : string.Join(", ", result.LoadOrder);
        var restartSensitive = result.RestartRequiredMods.Count == 0
            ? "<none>"
            : string.Join(", ", result.RestartRequiredMods);
        Log.LogInfo(
            $"Discovery complete. Scanned={result.ScannedCount}, Valid={result.ValidCount}, Errored={result.ErroredCount}, EffectiveEnabled={result.EffectiveEnabledCount}, RestartRequired={result.RestartRequiredCount}, Definitions={result.DefinitionFileCount}, LocalizationDefs={result.LocalizationDefinitionCount}, BalancePatchDefs={result.BalancePatchDefinitionCount}, CameraPatchDefs={result.CameraPatchDefinitionCount}, VisualDefs={result.VisualDefinitionCount}, AppliedMods={result.AppliedModCount}, AppliedDefinitions={result.AppliedDefinitionCount}, AppliedLocalizationDefs={result.AppliedLocalizationDefinitionCount}, AppliedLocalizationEntries={result.AppliedLocalizationEntryCount}, AppliedBalancePatches={result.AppliedBalancePatchCount}, AppliedCameraPatches={result.AppliedCameraPatchCount}, AppliedVisualPatches={result.AppliedVisualPatchCount}, AppliedIntroSkips={result.AppliedIntroSkipCount}, DllModsScanned={result.DllScannedModCount}, DllModsLoaded={result.DllLoadedModCount}, DllModsErrored={result.DllErroredModCount}, DllEntrypointsDeclared={result.DllDeclaredEntrypointCount}, DllEntrypointsLoaded={result.DllLoadedEntrypointCount}, StartupToggles={result.StartupTotalToggleCount}, StartupCli={result.StartupCliToggleCount}, StartupQueue={result.StartupQueuedToggleCount}, StartupQueueDeduped={result.StartupQueueDedupedCount}, StartupUnknown={result.StartupUnknownToggleCount}, StartupResolved={result.StartupResolvedModCount}, SafeMode={result.SafeModeActive}, LoadOrder={loadOrder}");

        if (result.RestartRequiredCount > 0)
        {
            Log.LogWarning($"Restart required for toggled mods: {restartSensitive}");
        }

        SessionIsolationGuard.Install(gameRoot, Log);
        RuntimeHooks.Install(Log);
        RuntimeHooks.InstallUiBlockers(Log);
        LocalizationHooks.Install(Log);
        BalancePatchApplicator.Install(Log);
        EnemyStatPatchApplicator.Install(Log);
        CameraPatchApplicator.Install(Log);
        VisualPatchApplicator.Install(Log);
        IntroSkipApplicator.Install(Log);
        UiScaleApplicator.Install(Log);
        FovSettingsSliderApplicator.Install(Log);
        CrabtyaNativeSettingsApplicator.Install(Log);
    }

    private void LogBuildFingerprint()
    {
        try
        {
            var assembly = typeof(Plugin).Assembly;
            var location = assembly.Location;
            var info = new FileInfo(location);
            var stamp = info.Exists ? info.LastWriteTimeUtc.ToString("o") : "unknown";
            var size = info.Exists ? info.Length.ToString() : "unknown";
            Log.LogInfo($"Plugin binary fingerprint: Path={location}, LastWriteUtc={stamp}, Size={size}");
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Unable to read plugin binary fingerprint: {ex.Message}");
        }
    }

    private void LogResolvedStartupToggleOutcome(IReadOnlyList<ModToggleCommand> startupCommands)
    {
        var grouped = startupCommands
            .GroupBy(c => c.ModId, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        if (grouped.Count == 0)
        {
            return;
        }

        foreach (var group in grouped)
        {
            var chain = string.Join(
                " -> ",
                group.Select(c => $"{(c.Enabled ? "enable" : "disable")}({(string.IsNullOrWhiteSpace(c.Source) ? "unknown" : c.Source)})"));
            var winner = group.Last();
            var winnerSource = string.IsNullOrWhiteSpace(winner.Source) ? "unknown" : winner.Source;
            Log.LogWarning(
                $"Multiple startup toggles detected for '{group.Key}'. Resolution chain={chain}. Effective startup intent={((winner.Enabled ? "enable" : "disable"))}({winnerSource}).");
        }
    }
}
