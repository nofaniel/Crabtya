using System.Text.Json;
using BepInEx.Logging;

namespace EIC.ModLoader;

public sealed class StartupCommandQueueFile
{
    public string LastUpdatedUtc { get; set; } = string.Empty;
    public List<ModToggleCommand> Toggles { get; set; } = new();
}

public sealed class StartupQueueLoadResult
{
    public List<ModToggleCommand> Toggles { get; set; } = new();
    public int DedupedCount { get; set; }
    public bool QueueFileConsumed { get; set; }
}

public static class StartupCommandQueue
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static StartupQueueLoadResult LoadAndClear(string path, ManualLogSource log)
    {
        if (!File.Exists(path))
        {
            return new StartupQueueLoadResult();
        }

        try
        {
            var json = File.ReadAllText(path);
            var file = JsonSerializer.Deserialize<StartupCommandQueueFile>(json) ?? new StartupCommandQueueFile();
            var normalized = Normalize(file.Toggles, out var dedupedCount);
            if (dedupedCount > 0)
            {
                log.LogWarning(
                    $"Startup command queue contained duplicate mod ids. Deduped={dedupedCount}, Remaining={normalized.Count}.");
            }

            File.Delete(path);
            return new StartupQueueLoadResult
            {
                Toggles = normalized.ToList(),
                DedupedCount = dedupedCount,
                QueueFileConsumed = true
            };
        }
        catch (Exception ex)
        {
            log.LogWarning($"Failed to read startup command queue '{path}': {ex.Message}");
            return new StartupQueueLoadResult();
        }
    }

    public static bool EnqueueToggle(string path, ModToggleCommand command, out string error)
    {
        error = string.Empty;

        try
        {
            var parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            StartupCommandQueueFile file;
            if (File.Exists(path))
            {
                var existingJson = File.ReadAllText(path);
                file = JsonSerializer.Deserialize<StartupCommandQueueFile>(existingJson) ?? new StartupCommandQueueFile();
            }
            else
            {
                file = new StartupCommandQueueFile();
            }

            var replaced = file.Toggles.RemoveAll(t => string.Equals(t.ModId, command.ModId, StringComparison.OrdinalIgnoreCase));
            var normalizedSource = string.IsNullOrWhiteSpace(command.Source) ? "runtime_queue" : command.Source;
            file.Toggles.Add(command with { Source = normalizedSource });
            file.LastUpdatedUtc = DateTimeOffset.UtcNow.ToString("o");

            var json = JsonSerializer.Serialize(file, JsonOptions);
            File.WriteAllText(path, json);
            if (replaced > 0)
            {
                error = "replaced_existing";
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static IReadOnlyList<ModToggleCommand> Peek(string path)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<ModToggleCommand>();
        }

        try
        {
            var json = File.ReadAllText(path);
            var file = JsonSerializer.Deserialize<StartupCommandQueueFile>(json) ?? new StartupCommandQueueFile();
            return Normalize(file.Toggles, out _);
        }
        catch
        {
            return Array.Empty<ModToggleCommand>();
        }
    }

    public static bool Clear(string path, out string error)
    {
        error = string.Empty;
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool RemoveToggle(string path, string modId, out bool removed, out string error)
    {
        removed = false;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(modId))
        {
            error = "mod id is empty";
            return false;
        }

        try
        {
            if (!File.Exists(path))
            {
                return true;
            }

            var json = File.ReadAllText(path);
            var file = JsonSerializer.Deserialize<StartupCommandQueueFile>(json) ?? new StartupCommandQueueFile();
            var removedCount = file.Toggles.RemoveAll(t => string.Equals(t.ModId, modId, StringComparison.OrdinalIgnoreCase));
            removed = removedCount > 0;
            if (!removed)
            {
                return true;
            }

            if (file.Toggles.Count == 0)
            {
                File.Delete(path);
                return true;
            }

            file.LastUpdatedUtc = DateTimeOffset.UtcNow.ToString("o");
            File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions));
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static IReadOnlyList<ModToggleCommand> Normalize(IReadOnlyList<ModToggleCommand> toggles, out int dedupedCount)
    {
        var deduped = new Dictionary<string, ModToggleCommand>(StringComparer.OrdinalIgnoreCase);
        foreach (var toggle in toggles)
        {
            if (string.IsNullOrWhiteSpace(toggle.ModId))
            {
                continue;
            }

            deduped[toggle.ModId] = toggle;
        }

        dedupedCount = Math.Max(0, toggles.Count - deduped.Count);
        return deduped.Values.ToList();
    }
}
