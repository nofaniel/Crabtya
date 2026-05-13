using System.Text.Json;

namespace EIC.ModLoader;

public sealed class ModStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;

    public ModStateStore(string path)
    {
        _path = path;
    }

    public ModStateFile Load()
    {
        if (!File.Exists(_path))
        {
            return new ModStateFile();
        }

        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<ModStateFile>(json) ?? new ModStateFile();
        }
        catch
        {
            return new ModStateFile();
        }
    }

    public void Save(ModStateFile state)
    {
        var parent = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(_path, json);
    }
}

public sealed class ModStateFile
{
    public bool SafeModeActive { get; set; }
    public string SafeModeReason { get; set; } = "none";
    public string LastRunUtc { get; set; } = string.Empty;
    public StartupToggleSummary StartupToggles { get; set; } = new();
    public ContentApplySummary ContentApply { get; set; } = new();
    public Dictionary<string, ModStateEntry> Mods { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ModStateEntry
{
    public string? Name { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public bool Enabled { get; set; }
    public bool EffectiveEnabled { get; set; }
    public bool RestartRequired { get; set; }
    public List<string> RestartReasons { get; set; } = new();
    public int DefinitionFileCount { get; set; }
    public List<string> DefinitionTypes { get; set; } = new();
    public int AppliedDefinitionCount { get; set; }
    public int AppliedLocalizationEntryCount { get; set; }
    public int AppliedBalancePatchCount { get; set; }
    public int AppliedCameraPatchCount { get; set; }
    public int AppliedVisualPatchCount { get; set; }
    public int DeclaredAssemblyCount { get; set; }
    public int DeclaredEntrypointCount { get; set; }
    public int AppliedDllEntrypointCount { get; set; }
    public List<string> AppliedDllEntrypoints { get; set; } = new();
    public List<string> AppliedDefinitionTypes { get; set; } = new();
    public List<string> AppliedDefinitionIds { get; set; } = new();
    public List<string> AppliedLocales { get; set; } = new();
    public string Status { get; set; } = "unknown";
    public string ConflictStatus { get; set; } = "none"; // "none", "info", "warning", "blocked"
    public List<string> ConflictNotices { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool StartupToggleApplied { get; set; }
    public bool? StartupRequestedEnabled { get; set; }
    public string StartupToggleSource { get; set; } = string.Empty;
    public bool? PendingStartupQueuedEnabled { get; set; }
    public string PendingStartupQueuedSource { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? FolderName { get; set; }
}

public sealed class StartupToggleSummary
{
    public string ProcessedAtUtc { get; set; } = string.Empty;
    public bool QueueFileConsumed { get; set; }
    public int CliToggleCount { get; set; }
    public int QueuedToggleCount { get; set; }
    public int QueueDedupedCount { get; set; }
    public int TotalToggleCount { get; set; }
    public int UnknownToggleCount { get; set; }
    public List<string> UnknownModIds { get; set; } = new();
    public int ResolvedModCount { get; set; }
    public List<string> ResolvedFinalIntents { get; set; } = new();
}

public sealed class ContentApplySummary
{
    public int AppliedModCount { get; set; }
    public int AppliedDefinitionCount { get; set; }
    public int AppliedLocalizationDefinitionCount { get; set; }
    public int AppliedLocalizationEntryCount { get; set; }
    public int AppliedBalancePatchCount { get; set; }
    public int AppliedCameraPatchCount { get; set; }
    public int AppliedVisualPatchCount { get; set; }
    public int AppliedIntroSkipCount { get; set; }
    public int DllScannedMods { get; set; }
    public int DllLoadedMods { get; set; }
    public int DllErroredMods { get; set; }
    public int DllDeclaredEntrypoints { get; set; }
    public int DllLoadedEntrypoints { get; set; }
}
