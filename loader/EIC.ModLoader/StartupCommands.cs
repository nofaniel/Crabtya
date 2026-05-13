using BepInEx.Logging;

namespace EIC.ModLoader;

public readonly record struct ModToggleCommand(string ModId, bool Enabled, string? Source = null);

public static class StartupCommands
{
    public const string EnablePrefix = "--eic-enable-mod=";
    public const string DisablePrefix = "--eic-disable-mod=";

    public static IReadOnlyList<ModToggleCommand> Parse(string[] args, ManualLogSource log)
    {
        var commands = new List<ModToggleCommand>();

        foreach (var rawArg in args)
        {
            if (rawArg.StartsWith(EnablePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var modId = rawArg.Substring(EnablePrefix.Length).Trim();
                if (string.IsNullOrWhiteSpace(modId))
                {
                    log.LogWarning("Ignoring empty --eic-enable-mod argument.");
                    continue;
                }

                commands.Add(new ModToggleCommand(modId, true, "cli"));
                continue;
            }

            if (rawArg.StartsWith(DisablePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var modId = rawArg.Substring(DisablePrefix.Length).Trim();
                if (string.IsNullOrWhiteSpace(modId))
                {
                    log.LogWarning("Ignoring empty --eic-disable-mod argument.");
                    continue;
                }

                commands.Add(new ModToggleCommand(modId, false, "cli"));
            }
        }

        return commands;
    }
}
