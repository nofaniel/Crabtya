using BepInEx.Logging;
using UnityEngine;

namespace EIC.ModLoader;

/// <summary>
/// Loads Unity AssetBundles declared in mod manifests (<c>content.assetBundles</c>) for
/// every enabled mod, in load order, before DLL entrypoints are invoked.
///
/// Loaded bundles are cached by key <c>"modId:relativePath"</c> and survive for the
/// lifetime of the game session.  Asset bundles cannot be safely unloaded in IL2CPP
/// at runtime, so mods that declare them are always marked restart-required by
/// <see cref="LiveModRegistry.RequiresRestart"/>.
///
/// DLL mods can retrieve a loaded bundle via <see cref="GetBundle"/> from their
/// <c>OnLoad</c> implementation.
/// </summary>
public static class AssetBundleApplicator
{
    // Key: "modId:relativePath" (case-insensitive)
    private static readonly Dictionary<string, AssetBundle?> _loaded =
        new(StringComparer.OrdinalIgnoreCase);

    private static ManualLogSource? _log;

    /// <summary>
    /// All loaded bundles keyed by <c>"modId:relativePath"</c>.
    /// A <c>null</c> value means the bundle path was declared but failed to load.
    /// </summary>
    public static IReadOnlyDictionary<string, AssetBundle?> AllLoaded => _loaded;

    /// <summary>
    /// Iterates every enabled mod (in resolved load order) and loads any declared
    /// <c>content.assetBundles</c> that have not already been loaded this session.
    /// </summary>
    public static AssetBundleLoadResult LoadForMods(
        IEnumerable<DiscoveredModEntry> entries,
        IReadOnlyList<string> loadOrder,
        IReadOnlyDictionary<string, ModStateEntry> modState,
        ManualLogSource log)
    {
        _log = log;

        var entryById = entries
            .Where(e => e.Manifest?.Id is not null)
            .ToDictionary(e => e.Manifest!.Id!, e => e, StringComparer.OrdinalIgnoreCase);

        var loadedCount = 0;
        var erroredCount = 0;
        var skippedCount = 0;

        foreach (var modId in loadOrder)
        {
            if (!entryById.TryGetValue(modId, out var entry) ||
                !modState.TryGetValue(modId, out var state) ||
                !state.EffectiveEnabled)
            {
                continue;
            }

            var bundles = entry.Manifest?.Content?.AssetBundles;
            if (bundles is null || bundles.Count == 0)
            {
                continue;
            }

            foreach (var relativePath in bundles)
            {
                if (string.IsNullOrWhiteSpace(relativePath))
                {
                    continue;
                }

                var key = BuildBundleKey(modId, relativePath);
                if (_loaded.ContainsKey(key))
                {
                    skippedCount++;
                    log.LogInfo($"AssetBundleApplicator: bundle already loaded (skipped): mod='{modId}' path='{relativePath}'");
                    continue;
                }

                var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
                var resolvedPath = Path.Combine(entry.DirectoryPath, normalized);
                if (!File.Exists(resolvedPath))
                {
                    log.LogWarning(
                        $"AssetBundleApplicator: bundle file not found: '{resolvedPath}' (mod='{modId}', declared='{relativePath}').");
                    _loaded[key] = null;
                    erroredCount++;
                    continue;
                }

                try
                {
                    var bundle = AssetBundle.LoadFromFile(resolvedPath);
                    _loaded[key] = bundle;

                    if (bundle is null)
                    {
                        log.LogWarning(
                            $"AssetBundleApplicator: LoadFromFile returned null for '{relativePath}' (mod='{modId}'). " +
                            $"The bundle may have been built with a different Unity version ({TargetUnityVersion}).");
                        erroredCount++;
                    }
                    else
                    {
                        string assetPreview;
                        try
                        {
                            var names = bundle.GetAllAssetNames();
                            assetPreview = names is null || names.Length == 0
                                ? "<none>"
                                : string.Join(", ", names.Take(5)) + (names.Length > 5 ? $"… (+{names.Length - 5})" : string.Empty);
                        }
                        catch
                        {
                            assetPreview = "<unavailable>";
                        }

                        log.LogInfo(
                            $"AssetBundleApplicator: loaded bundle '{relativePath}' for mod '{modId}'. Assets={assetPreview}");
                        loadedCount++;
                    }
                }
                catch (Exception ex)
                {
                    log.LogWarning(
                        $"AssetBundleApplicator: exception loading bundle '{relativePath}' for mod '{modId}': {ex.Message}");
                    _loaded[key] = null;
                    erroredCount++;
                }
            }
        }

        log.LogInfo(
            $"AssetBundleApplicator: LoadForMods complete. Loaded={loadedCount}, Errored={erroredCount}, Skipped={skippedCount}");

        return new AssetBundleLoadResult
        {
            LoadedCount = loadedCount,
            ErroredCount = erroredCount,
            SkippedCount = skippedCount
        };
    }

    /// <summary>
    /// Returns the <see cref="AssetBundle"/> loaded from <paramref name="relativePath"/>
    /// inside <paramref name="modId"/>'s folder, or <c>null</c> if it was not loaded or
    /// failed to load.
    /// </summary>
    public static AssetBundle? GetBundle(string modId, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var key = BuildBundleKey(modId, relativePath);
        return _loaded.TryGetValue(key, out var bundle) ? bundle : null;
    }

    /// <summary>
    /// Returns all bundles loaded for a given mod, in declaration order.
    /// </summary>
    public static IReadOnlyList<AssetBundle> GetBundlesForMod(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            return Array.Empty<AssetBundle>();
        }

        var prefix = $"{modId}:";
        return _loaded
            .Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && kv.Value is not null)
            .Select(kv => kv.Value!)
            .ToList();
    }

    private static string BuildBundleKey(string modId, string relativePath)
    {
        var normalized = relativePath
            .Replace('\\', '/')
            .Trim();
        return $"{modId}:{normalized}";
    }

    private const string TargetUnityVersion = "6000.2.15f1";
}

/// <summary>
/// Summary returned by <see cref="AssetBundleApplicator.LoadForMods"/>.
/// </summary>
public sealed class AssetBundleLoadResult
{
    public int LoadedCount { get; init; }
    public int ErroredCount { get; init; }
    public int SkippedCount { get; init; }
}
