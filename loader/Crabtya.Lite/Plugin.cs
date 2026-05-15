using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace Crabtya.Lite;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "crabtya.lite";
    public const string PluginName = "Crabtya Lite";
    public const string PluginVersion = "0.1.0";

    public static Plugin? Instance { get; private set; }

    public override void Load()
    {
        Instance = this;
        Log.LogInfo("Crabtya Lite bootstrap loaded.");
        LogBuildFingerprint();

        if (IsFullCrabtyaDetected() && !HasCoexistOverride())
        {
            Log.LogWarning(
                "Crabtya Lite detected full Crabtya (EIC.ModLoader.dll). Lite will stay inactive to avoid mixed installs. Remove full Crabtya or launch with --crabtya-lite-allow-full for local testing.");
            return;
        }

        LiteSettingsStore.Install(Log);
        LiteCameraApplicator.Install(Log);
        LiteNativeSettingsApplicator.Install(Log);
        LiteRuntimeBootstrap.EnsureCreated();

        Log.LogInfo("Crabtya Lite runtime initialized.");
    }

    private static bool HasCoexistOverride()
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--crabtya-lite-allow-full", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFullCrabtyaDetected()
    {
        try
        {
            var fullPluginPath = Path.Combine(AppContext.BaseDirectory, "BepInEx", "plugins", "EIC.ModLoader.dll");
            if (File.Exists(fullPluginPath))
            {
                return true;
            }
        }
        catch
        {
        }

        return Type.GetType("EIC.ModLoader.Plugin, EIC.ModLoader", throwOnError: false, ignoreCase: false) is not null;
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
}
