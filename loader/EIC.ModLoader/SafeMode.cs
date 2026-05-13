namespace EIC.ModLoader;

public readonly record struct SafeModeState(bool IsActive, string Reason)
{
    public const string CommandLineArgument = "--eic-safe-mode";
    public const string MarkerFileName = "eic-safe-mode.flag";

    public static SafeModeState Detect(string gameRoot)
    {
        var args = Environment.GetCommandLineArgs();
        var hasArg = args.Any(a => string.Equals(a, CommandLineArgument, StringComparison.OrdinalIgnoreCase));
        if (hasArg)
        {
            return new SafeModeState(true, "command_line");
        }

        var markerPath = Path.Combine(gameRoot, MarkerFileName);
        if (File.Exists(markerPath))
        {
            return new SafeModeState(true, "marker_file");
        }

        return new SafeModeState(false, "none");
    }
}
