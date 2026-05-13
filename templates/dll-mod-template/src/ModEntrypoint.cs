using Crabtya.ModApi;

namespace Author.DllMod;

/// <summary>
/// Entrypoint for the DLL mod template.
/// Rename this class and update eicmod.json's "entrypoints" field to match.
/// </summary>
public sealed class ModEntrypoint : ICrabtyaMod
{
    // These must match eicmod.json exactly.
    public string Id => "author.dll-mod";
    public string Name => "My DLL Mod";
    public string Version => "1.0.0";

    public void OnLoad(ICrabtyaModContext context)
    {
        // Log a message at startup.
        context.Logger.Info("My DLL mod loaded.");

        // Register settings that will appear in the Crabtya Mods settings window.
        // Values are persisted in game/Mods/runtime-settings.json.
        RegisterSettings(context.Settings);

        // Read a previously-persisted setting value.
        if (context.Settings.TryGetBool("enabled", out var isEnabled))
        {
            context.Logger.Info($"Feature enabled: {isEnabled}");
        }
    }

    private static void RegisterSettings(ICrabtyaSettingsContext settings)
    {
        // Toggle (bool)
        settings.RegisterBool(
            key: "enabled",
            label: "Enable Feature",
            defaultValue: true);

        // Integer slider
        settings.RegisterInt(
            key: "count",
            label: "Count",
            defaultValue: 3,
            min: 1,
            max: 10,
            step: 1);

        // Float slider
        settings.RegisterFloat(
            key: "scale",
            label: "Scale",
            defaultValue: 1.0f,
            min: 0.5f,
            max: 2.0f,
            step: 0.05f);

        // Option cycle button
        settings.RegisterOption(
            key: "mode",
            label: "Mode",
            defaultValue: "Normal",
            options: new[] { "Normal", "Hard", "Relaxed" });
    }
}
