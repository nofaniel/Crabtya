using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace EIC.SmokeTestPlugin;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "eic.smoketest.plugin";
    public const string PluginName = "EIC Smoke Test Plugin";
    public const string PluginVersion = "0.1.0";

    public override void Load()
    {
        Log.LogInfo("EIC Smoke Test Plugin loaded.");
    }
}
