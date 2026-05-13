using System.Reflection;
using BepInEx.Logging;
using Crabtya.ModApi;

namespace EIC.ModLoader;

public static class DllModLoader
{
    public static DllModLoadResult LoadAndInvoke(
        IReadOnlyList<string> loadOrder,
        IReadOnlyDictionary<string, DiscoveredModEntry> entriesById,
        IDictionary<string, ModStateEntry> modState,
        ManualLogSource log)
    {
        var result = new DllModLoadResult();
        CrabtyaSettingsRegistry.Reset();

        foreach (var modId in loadOrder)
        {
            if (!entriesById.TryGetValue(modId, out var entry) ||
                !modState.TryGetValue(modId, out var state) ||
                !state.EffectiveEnabled ||
                entry.Manifest is null)
            {
                continue;
            }

            var assemblies = (entry.Manifest.Assemblies ?? new List<string>())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var entrypoints = (entry.Manifest.EntryPoints ?? new List<string>())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            state.DeclaredAssemblyCount = assemblies.Count;
            state.DeclaredEntrypointCount = entrypoints.Count;

            if (assemblies.Count == 0 && entrypoints.Count == 0)
            {
                continue;
            }

            result.ScannedModCount++;
            result.DeclaredEntrypointCount += entrypoints.Count;

            var modHadError = false;
            if (assemblies.Count == 0 || entrypoints.Count == 0)
            {
                modHadError = true;
                EnsureStateCollections(state);
                var message = assemblies.Count == 0
                    ? "DLL mod declares entrypoints but has no assemblies."
                    : "DLL mod declares assemblies but has no entrypoints.";
                state.Errors.Add(message);
                state.Status = "runtime-error";
                log.LogError($"DLL mod load failed for '{modId}': {message}");
                result.ErroredModCount++;
                continue;
            }

            var loadedAssemblies = new List<Assembly>();
            for (var i = 0; i < assemblies.Count; i++)
            {
                var relativePath = assemblies[i];
                if (!TryResolveSafeModFile(entry.DirectoryPath, relativePath, out var fullPath, out var reason))
                {
                    modHadError = true;
                    EnsureStateCollections(state);
                    state.Errors.Add($"Referenced assembly path is invalid: {relativePath} ({reason})");
                    state.Status = "runtime-error";
                    log.LogError($"DLL mod '{modId}' assembly path rejected: {relativePath}. Reason={reason}");
                    continue;
                }

                try
                {
                    var assembly = Assembly.LoadFrom(fullPath);
                    loadedAssemblies.Add(assembly);
                }
                catch (Exception ex)
                {
                    modHadError = true;
                    EnsureStateCollections(state);
                    state.Errors.Add($"Assembly load failed: {relativePath} ({ex.Message})");
                    state.Status = "runtime-error";
                    log.LogError($"DLL mod '{modId}' assembly load failed: {relativePath}. Error={ex.Message}");
                }
            }

            var loadedEntrypointsThisMod = 0;
            for (var i = 0; i < entrypoints.Count; i++)
            {
                var entrypointName = entrypoints[i];
                try
                {
                    var type = ResolveEntrypointType(entrypointName, loadedAssemblies, entry.DirectoryPath);
                    if (type is null)
                    {
                        modHadError = true;
                        EnsureStateCollections(state);
                        state.Errors.Add($"Entrypoint type not found: {entrypointName}");
                        state.Status = "runtime-error";
                        log.LogError($"DLL mod '{modId}' entrypoint not found: {entrypointName}");
                        continue;
                    }

                    if (!typeof(ICrabtyaMod).IsAssignableFrom(type))
                    {
                        modHadError = true;
                        EnsureStateCollections(state);
                        state.Errors.Add($"Entrypoint does not implement ICrabtyaMod: {entrypointName}");
                        state.Status = "runtime-error";
                        log.LogError($"DLL mod '{modId}' entrypoint does not implement ICrabtyaMod: {entrypointName}");
                        continue;
                    }

                    if (type.GetConstructor(Type.EmptyTypes) is null)
                    {
                        modHadError = true;
                        EnsureStateCollections(state);
                        state.Errors.Add($"Entrypoint requires a public parameterless constructor: {entrypointName}");
                        state.Status = "runtime-error";
                        log.LogError($"DLL mod '{modId}' entrypoint requires parameterless constructor: {entrypointName}");
                        continue;
                    }

                    var instance = (ICrabtyaMod)Activator.CreateInstance(type)!;
                    var context = new CrabtyaModContext(
                        entry.Manifest.Id ?? modId,
                        entry.Manifest.Name ?? modId,
                        entry.Manifest.Version ?? "unknown",
                        entry.DirectoryPath,
                        new CrabtyaModLogger(log, modId),
                        CrabtyaSettingsRegistry.CreateForMod(
                            entry.Manifest.Id ?? modId,
                            entry.Manifest.Name ?? modId,
                            entry.Manifest.Version ?? "unknown"));

                    instance.OnLoad(context);
                    loadedEntrypointsThisMod++;
                    result.LoadedEntrypointCount++;
                    EnsureStateCollections(state);
                    state.AppliedDllEntrypointCount++;
                    state.AppliedDllEntrypoints.Add(entrypointName);
                    log.LogInfo($"DLL mod '{modId}' entrypoint loaded: {entrypointName}");
                }
                catch (Exception ex)
                {
                    modHadError = true;
                    EnsureStateCollections(state);
                    state.Errors.Add($"Entrypoint threw during OnLoad: {entrypointName} ({ex.Message})");
                    state.Status = "runtime-error";
                    log.LogError($"DLL mod '{modId}' entrypoint failed: {entrypointName}. Error={ex}");
                }
            }

            if (loadedEntrypointsThisMod > 0)
            {
                result.LoadedModCount++;
            }

            if (modHadError)
            {
                result.ErroredModCount++;
            }
        }

        return result;
    }

    private static void EnsureStateCollections(ModStateEntry state)
    {
        state.Errors ??= new List<string>();
        state.AppliedDllEntrypoints ??= new List<string>();
    }

    private static Type? ResolveEntrypointType(string entrypointTypeName, IReadOnlyList<Assembly> assemblies, string modDirectory)
    {
        for (var i = 0; i < assemblies.Count; i++)
        {
            var type = assemblies[i].GetType(entrypointTypeName, throwOnError: false, ignoreCase: false);
            if (type is not null)
            {
                return type;
            }
        }

        string fullModRoot;
        try
        {
            fullModRoot = Path.GetFullPath(modDirectory);
        }
        catch
        {
            return null;
        }

        var loaded = AppDomain.CurrentDomain.GetAssemblies();
        for (var i = 0; i < loaded.Length; i++)
        {
            if (!IsAssemblyUnderModRoot(loaded[i], fullModRoot))
            {
                continue;
            }

            var type = loaded[i].GetType(entrypointTypeName, throwOnError: false, ignoreCase: false);
            if (type is not null)
            {
                return type;
            }
        }

        return null;
    }

    private static bool IsAssemblyUnderModRoot(Assembly assembly, string fullModRoot)
    {
        try
        {
            var location = assembly.Location;
            if (string.IsNullOrWhiteSpace(location))
            {
                return false;
            }

            var fullAssemblyPath = Path.GetFullPath(location);
            var rootPrefix = fullModRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullAssemblyPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryResolveSafeModFile(string modDirectory, string relativePath, out string resolvedPath, out string reason)
    {
        resolvedPath = string.Empty;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            reason = "empty path";
            return false;
        }

        if (Path.IsPathRooted(relativePath))
        {
            reason = "path must be relative";
            return false;
        }

        if (relativePath.Contains("..", StringComparison.Ordinal))
        {
            reason = "path cannot traverse parent directories";
            return false;
        }

        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullModRoot = Path.GetFullPath(modDirectory);
        var candidate = Path.GetFullPath(Path.Combine(fullModRoot, normalized));
        var rootPrefix = fullModRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            reason = "resolved path escaped mod folder";
            return false;
        }

        if (!File.Exists(candidate))
        {
            reason = "file is missing";
            return false;
        }

        resolvedPath = candidate;
        return true;
    }
}

public sealed class DllModLoadResult
{
    public int ScannedModCount { get; set; }

    public int LoadedModCount { get; set; }

    public int ErroredModCount { get; set; }

    public int DeclaredEntrypointCount { get; set; }

    public int LoadedEntrypointCount { get; set; }
}

public sealed class CrabtyaModLogger : ICrabtyaLogger
{
    private readonly ManualLogSource _log;
    private readonly string _modId;

    public CrabtyaModLogger(ManualLogSource log, string modId)
    {
        _log = log;
        _modId = modId;
    }

    public void Info(string message)
    {
        _log.LogInfo($"[CrabtyaMod:{_modId}] {message}");
    }

    public void Warning(string message)
    {
        _log.LogWarning($"[CrabtyaMod:{_modId}] {message}");
    }

    public void Error(string message)
    {
        _log.LogError($"[CrabtyaMod:{_modId}] {message}");
    }
}

public sealed class CrabtyaModContext : ICrabtyaModContext
{
    public CrabtyaModContext(
        string modId,
        string modName,
        string modVersion,
        string modDirectory,
        ICrabtyaLogger logger,
        ICrabtyaSettingsRegistry settings)
    {
        ModId = modId;
        ModName = modName;
        ModVersion = modVersion;
        ModDirectory = modDirectory;
        Logger = logger;
        Settings = settings;
    }

    public string ModId { get; }

    public string ModName { get; }

    public string ModVersion { get; }

    public string ModDirectory { get; }

    public ICrabtyaLogger Logger { get; }

    public ICrabtyaSettingsRegistry Settings { get; }
}
