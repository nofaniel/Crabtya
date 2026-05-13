using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using World.UI;

namespace EIC.ModLoader;

public static class RuntimeHooks
{
    private static readonly string[] CandidateMethods =
    {
        "Config:Initialize",
        "ProgressManager:InitializeStatics",
        "GameFlow.GameFlowController:InitializeRunInBackground",
        "World.UI.MainMenuScreen.MainMenuScreenUI:OnEnable"
    };

    private static readonly HashSet<string> FiredHooks = new(StringComparer.OrdinalIgnoreCase);

    public static void Install(ManualLogSource log)
    {
        var harmony = new Harmony("eic.modloader.runtimehooks");
        var installed = 0;

        foreach (var methodName in CandidateMethods)
        {
            var target = ResolveMethod(methodName);
            if (target is null)
            {
                log.LogWarning($"Runtime hook candidate not found: {methodName}");
                continue;
            }

            var postfix = new HarmonyMethod(typeof(RuntimeHooks).GetMethod(nameof(OnHookFired), BindingFlags.NonPublic | BindingFlags.Static));
            harmony.Patch(target, postfix: postfix);
            installed++;
            log.LogInfo($"Runtime hook installed: {methodName}");
        }

        log.LogInfo($"Runtime hook installation complete. Installed={installed}, Candidates={CandidateMethods.Length}");
    }

    private static MethodInfo? ResolveMethod(string candidate)
    {
        var separatorIndex = candidate.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= candidate.Length - 1)
        {
            return null;
        }

        var typeName = candidate[..separatorIndex];
        var methodName = candidate[(separatorIndex + 1)..];

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? type;
            try
            {
                type = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
            }
            catch
            {
                continue;
            }

            if (type is null)
            {
                continue;
            }

            return type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        }

        return null;
    }

    private static void OnHookFired(MethodBase __originalMethod)
    {
        var signature = $"{__originalMethod.DeclaringType?.FullName}:{__originalMethod.Name}";
        if (!FiredHooks.Add(signature))
        {
            return;
        }

        var log = Plugin.Instance?.Log;
        if (log is null)
        {
            return;
        }

        var state = RuntimeContentState.Current;
        log.LogInfo(
            $"Runtime hook fired: {signature}. AppliedMods={state.AppliedModCount}, AppliedDefinitions={state.AppliedDefinitionCount}, AppliedLocalizationEntries={state.AppliedLocalizationEntryCount}, AppliedBalancePatches={state.AppliedBalancePatchCount}, AppliedCameraPatches={state.AppliedCameraPatchCount}, AppliedVisualPatches={state.AppliedVisualPatchCount}");

        if (string.Equals(signature, "GameFlow.GameFlowController:InitializeRunInBackground", StringComparison.Ordinal) ||
            string.Equals(signature, "World.UI.MainMenuScreen.MainMenuScreenUI:OnEnable", StringComparison.Ordinal))
        {
            RuntimeOverlay.EnsureCreated();
        }

        foreach (var mod in state.Mods.Values.OrderBy(v => v.ModId, StringComparer.OrdinalIgnoreCase))
        {
            log.LogInfo(
                $"Applied mod snapshot: Id={mod.ModId}, Definitions={mod.AppliedDefinitionCount}, LocalizationEntries={mod.AppliedLocalizationEntryCount}, BalancePatches={mod.AppliedBalancePatchCount}, CameraPatches={mod.AppliedCameraPatchCount}, VisualPatches={mod.AppliedVisualPatchCount}, Locales={(mod.AppliedLocales.Count == 0 ? "<none>" : string.Join(", ", mod.AppliedLocales))}");
        }
    }

    /// <summary>
    /// Installs Harmony prefixes on the pause-screen close paths so that pressing ESC
    /// (or clicking Resume) while the Crabtya Mods window is open closes the mod window
    /// instead of resuming the game.  The prefix calls <see cref="CrabtyaModsSettingsWindow.Hide"/>
    /// and suppresses the game method; a second ESC press then closes the pause screen normally.
    /// Patches: <see cref="PauseScreenUI.OnClose"/> (ESC via Input System) and
    /// <see cref="PauseScreenUI.BTN_ClosePauseScreen"/> (Resume button click).
    /// </summary>
    public static void InstallUiBlockers(ManualLogSource log)
    {
        try
        {
            var harmony = new Harmony("eic.modloader.uiblockers");
            var prefix = new HarmonyMethod(
                typeof(RuntimeHooks).GetMethod(
                    nameof(HideModsWindowAndSuppress),
                    BindingFlags.NonPublic | BindingFlags.Static));

            // OnClose — called by the Input System when the "Close" action fires (ESC key).
            TryPatchMethod(harmony, typeof(PauseScreenUI), "OnClose", prefix, log);
            // BTN_ClosePauseScreen — called by the Resume button click.
            TryPatchMethod(harmony, typeof(PauseScreenUI), "BTN_ClosePauseScreen", prefix, log);
        }
        catch (Exception ex)
        {
            log.LogWarning($"Crabtya UI blocker install failed: {ex.Message}");
        }
    }

    private static void TryPatchMethod(Harmony harmony, Type type, string methodName, HarmonyMethod prefix, ManualLogSource log)
    {
        try
        {
            var target = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            if (target is null)
            {
                log.LogWarning($"Crabtya UI blocker: {type.Name}.{methodName} not found — skipped.");
                return;
            }

            harmony.Patch(target, prefix: prefix);
            log.LogInfo($"Crabtya UI blocker: patched {type.Name}.{methodName}.");
        }
        catch (Exception ex)
        {
            log.LogWarning($"Crabtya UI blocker: failed to patch {type.Name}.{methodName}: {ex.Message}");
        }
    }

    // Prefix shared by all patched pause-close methods.
    // Returns false (suppress the game method) when the Crabtya Mods window is open,
    // and closes the window first so the behaviour feels natural.
    // Hide() is called HERE rather than in Update() to avoid Update-order races where
    // our MonoBehaviour fires before the game and sets IsVisible=false too early.
    private static bool HideModsWindowAndSuppress()
    {
        if (!CrabtyaModsSettingsWindow.IsVisible)
        {
            return true; // mod window not open — let the game method run
        }

        CrabtyaModsSettingsWindow.Hide();
        return false; // mod window was open — suppress the game method this frame
    }
}
