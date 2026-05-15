using BepInEx.Logging;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Bindings;

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
                var resolvedPath = Path.GetFullPath(Path.Combine(entry.DirectoryPath, normalized));
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
                    var bundle = TryLoadBundle(resolvedPath, modId, relativePath, log);
                    _loaded[key] = bundle;

                    if (bundle is null)
                    {
                        var diagnostics = BuildBundleDiagnostics(resolvedPath);
                        log.LogWarning(
                            $"AssetBundleApplicator: LoadFromFile returned null for '{relativePath}' (mod='{modId}'). " +
                            $"The bundle may have been built with a different Unity version ({TargetUnityVersion}). {diagnostics}");
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
    /// Compatibility shim for Unity 6 + BepInEx IL2CPP builds where
    /// <see cref="AssetBundle.LoadAsset(string, Il2CppSystem.Type)"/> throws due to
    /// ReadOnlySpan marshalling in generated interop wrappers.
    /// </summary>
    public static UnityEngine.Object? LoadAssetCompat(
        AssetBundle bundle,
        string assetName,
        Il2CppSystem.Type assetType)
    {
        if (bundle is null || string.IsNullOrWhiteSpace(assetName) || assetType is null)
        {
            return null;
        }

        try
        {
            // AssetBundle injected iCalls expect Unity's marshalled object pointer, not the
            // raw IL2CPP wrapper-object pointer.
            var bundlePtr = UnityEngine.Object.MarshalledUnityObject.MarshalNotNull(bundle);
            if (bundlePtr == IntPtr.Zero)
            {
                return null;
            }

            unsafe
            {
                fixed (char* assetNamePtr = assetName)
                {
                    var nameSpan = new ManagedSpanWrapper(assetNamePtr, assetName.Length);
                    var loadedPtr = AssetBundle.LoadAsset_Internal_Injected(bundlePtr, ref nameSpan, assetType);
                    return loadedPtr == IntPtr.Zero
                        ? null
                        : Unmarshal.UnmarshalUnityObject<UnityEngine.Object>(loadedPtr);
                }
            }
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"AssetBundleApplicator: LoadAssetCompat threw for '{assetName}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Typed convenience wrapper around <see cref="LoadAssetCompat(AssetBundle, string, Il2CppSystem.Type)"/>.
    /// </summary>
    public static T? LoadAssetCompat<T>(AssetBundle bundle, string assetName)
        where T : UnityEngine.Object
    {
        var il2CppType = Il2CppSystem.Type.GetTypeFromHandle(
            RuntimeReflectionHelper.GetRuntimeTypeHandle<T>());
        var loaded = LoadAssetCompat(bundle, assetName, il2CppType);
        return loaded?.TryCast<T>();
    }

    /// <summary>
    /// Compatibility shim for Unity 6 + BepInEx IL2CPP builds where
    /// <see cref="AssetBundle.LoadAllAssets()"/> throws due to ReadOnlySpan marshalling
    /// in generated interop wrappers.
    /// </summary>
    public static Il2CppReferenceArray<UnityEngine.Object>? LoadAllAssetsCompat(AssetBundle bundle)
    {
        if (bundle is null)
        {
            return null;
        }

        try
        {
            var bundlePtr = UnityEngine.Object.MarshalledUnityObject.MarshalNotNull(bundle);
            if (bundlePtr == IntPtr.Zero)
            {
                return null;
            }

            ManagedSpanWrapper emptyNameSpan = default;
            if (!StringMarshaller.TryMarshalEmptyOrNullString(string.Empty, ref emptyNameSpan))
            {
                _log?.LogWarning("AssetBundleApplicator: failed to marshal empty asset name for LoadAllAssetsCompat.");
                return null;
            }

            var objectType = Il2CppSystem.Type.GetTypeFromHandle(
                RuntimeReflectionHelper.GetRuntimeTypeHandle<UnityEngine.Object>());

            return AssetBundle.LoadAssetWithSubAssets_Internal_Injected(
                bundlePtr,
                ref emptyNameSpan,
                objectType);
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"AssetBundleApplicator: LoadAllAssetsCompat threw: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Typed compatibility helper that filters <see cref="LoadAllAssetsCompat(AssetBundle)"/>
    /// results by runtime type.
    /// </summary>
    public static IReadOnlyList<T> LoadAllAssetsCompat<T>(AssetBundle bundle)
        where T : UnityEngine.Object
    {
        var loaded = LoadAllAssetsCompat(bundle);
        if (loaded is null || loaded.Length == 0)
        {
            return Array.Empty<T>();
        }

        var typed = new List<T>(loaded.Length);
        foreach (var asset in loaded)
        {
            if (asset is null)
            {
                continue;
            }

            var cast = asset.TryCast<T>();
            if (cast is not null)
            {
                typed.Add(cast);
            }
        }

        return typed;
    }

    private static AssetBundle? TryLoadBundle(
        string resolvedPath,
        string modId,
        string relativePath,
        ManualLogSource log)
    {
        // Attempt 1: stream load via Il2CppSystem.IO.MemoryStream — confirmed working on this
        // Unity 6000.2.15f1 IL2CPP build. Passes an IL2CPP object pointer to the native side,
        // which avoids the string→ReadOnlySpan<char> GetPinnableReference interop gap that
        // causes LoadFromFile to throw on this build.
        try
        {
            var bytes = File.ReadAllBytes(resolvedPath);
            var ms = new Il2CppSystem.IO.MemoryStream(bytes);
            var bundle = AssetBundle.LoadFromStream(ms);
            if (bundle is not null)
            {
                log.LogInfo(
                    $"AssetBundleApplicator: loaded bundle '{relativePath}' for mod '{modId}' via LoadFromStream.");
                return bundle;
            }

            log.LogWarning(
                $"AssetBundleApplicator: LoadFromStream returned null for '{relativePath}' (mod='{modId}').");
        }
        catch (Exception ex)
        {
            log.LogWarning(
                $"AssetBundleApplicator: LoadFromStream threw for '{relativePath}' (mod='{modId}'): {ex.Message}");
        }

        // Attempts 2 & 3: path-based fallbacks (known to throw GetPinnableReference on this
        // build, kept for diagnostic completeness so the log always shows which path failed).
        //
        // NOTE: We intentionally do not use LoadFromMemory(byte[]) here. On this runtime,
        // corrupt bundle payloads can trigger a fatal native AccessViolation in
        // LoadFromMemory_Internal before managed exception handling can isolate the failure.
        // Skipping that path preserves mod isolation for bad bundle files.
        AssetBundle? pathBundle;
        try
        {
            pathBundle = AssetBundle.LoadFromFile(resolvedPath);
        }
        catch (Exception ex)
        {
            log.LogWarning(
                $"AssetBundleApplicator: primary LoadFromFile threw for '{relativePath}' (mod='{modId}'): {ex.Message}");
            pathBundle = null;
        }

        if (pathBundle is not null)
        {
            return pathBundle;
        }

        var normalizedFullPath = resolvedPath.Replace('\\', '/');
        if (!string.Equals(normalizedFullPath, resolvedPath, StringComparison.Ordinal))
        {
            try
            {
                pathBundle = AssetBundle.LoadFromFile(normalizedFullPath);
            }
            catch (Exception ex)
            {
                log.LogWarning(
                    $"AssetBundleApplicator: separator-normalized LoadFromFile threw for '{relativePath}' (mod='{modId}'): {ex.Message}");
                pathBundle = null;
            }

            if (pathBundle is not null)
            {
                log.LogInfo(
                    $"AssetBundleApplicator: loaded bundle '{relativePath}' for mod '{modId}' using normalized path separators.");
                return pathBundle;
            }
        }

        return null;
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

    private static string BuildBundleDiagnostics(string resolvedPath)
    {
        try
        {
            var info = new FileInfo(resolvedPath);
            var header = TryReadAsciiHeader(resolvedPath, 32);
            return $"Path='{resolvedPath}', SizeBytes={info.Length}, Header='{header}'";
        }
        catch (Exception ex)
        {
            return $"Path='{resolvedPath}', SizeBytes=<unavailable:{ex.GetType().Name}>";
        }
    }

    private static string TryReadAsciiHeader(string path, int byteCount)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[Math.Max(1, byteCount)];
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0)
            {
                return "<empty>";
            }

            var text = System.Text.Encoding.ASCII.GetString(buffer, 0, read);
            return text.Replace("\0", string.Empty).Trim();
        }
        catch (Exception ex)
        {
            return $"<unavailable:{ex.GetType().Name}>";
        }
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
