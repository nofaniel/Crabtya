using BepInEx.Logging;
using UnityEngine;

namespace EIC.ModLoader;

public static class CrabtyaNativeSettingsApplicator
{
    private const string LegacySectionObjectName = "Crabtya.NativeSettings.Section";

    private static ManualLogSource? _log;
    private static bool _installLogged;
    private static bool _legacyCleanupDone;

    // Crabtya v1 direction uses a dedicated native-style Mods menu window.
    // Keep this true so older fallback injectors (for example FovSettingsSliderApplicator)
    // stay disabled and do not split settings across two surfaces.
    public static bool OwnsCameraPatchSettings => true;

    public static void Install(ManualLogSource log)
    {
        _log = log;
        if (_installLogged)
        {
            return;
        }

        _installLogged = true;
        _log.LogInfo("Crabtya native settings injection is intentionally disabled. Mod settings are rendered in the dedicated native-style Mods menu window.");
    }

    public static void Update()
    {
        // Best-effort cleanup in case an old build left this object in the live hierarchy.
        if (_legacyCleanupDone)
        {
            return;
        }

        _legacyCleanupDone = true;
        try
        {
            var legacySection = GameObject.Find(LegacySectionObjectName);
            if (legacySection is not null)
            {
                UnityEngine.Object.Destroy(legacySection);
                _log?.LogInfo("Crabtya removed legacy native-settings section host from previous builds.");
            }
        }
        catch
        {
        }
    }
}
