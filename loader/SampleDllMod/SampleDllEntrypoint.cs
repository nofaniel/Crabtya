using Crabtya.ModApi;

namespace Faniel.DllSample;

public sealed class SampleDllEntrypoint : ICrabtyaMod
{
    public string Id => "faniel.dll-sample";

    public string Name => "Sample DLL Mod";

    public string Version => "1.0.0";

    public void OnLoad(ICrabtyaModContext context)
    {
        context.Logger.Info("Sample DLL mod OnLoad reached.");
        context.Settings.RegisterBool("enableGreeting", "Enable Greeting", true, "Toggles startup greeting log.");
        context.Settings.RegisterInt("bonusChoices", "Bonus Choices", 2, 0, 5, 1, "Extra evolution choices used by sample mods.");
        context.Settings.RegisterFloat("zoomMultiplier", "Zoom Multiplier", 1.0f, 0.75f, 1.75f, 0.05f, "Sample float setting.");
        context.Settings.RegisterOption("mode", "Mode", "Default", new[] { "Default", "Aggressive", "Relaxed" }, "Sample enum-like option.");

        if (context.Settings.TryGetBool("enableGreeting", out var enabled) && enabled)
        {
            context.Logger.Info("Hello from Sample DLL Mod.");
        }
    }
}
