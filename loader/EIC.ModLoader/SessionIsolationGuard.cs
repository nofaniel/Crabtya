using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppDirectoryInfo = Il2CppSystem.IO.DirectoryInfo;
using Il2CppFileInfo = Il2CppSystem.IO.FileInfo;

namespace EIC.ModLoader;

public static class SessionIsolationGuard
{
    private const string SaveManagerTypeName = "SaveSystem.SaveManager";
    private const string SteamAchievementManagerTypeName = "Unlockables.Achievements.Steam_AchievementManager";
    private const string SteamUserStatsTypeName = "Steamworks.SteamUserStats";

    private static ManualLogSource? _log;
    private static string _sandboxRoot = string.Empty;
    private static int _achievementWriteBypassCount;
    private static int _steamWriteBypassCount;
    private static bool _saveRedirectLogEmitted;

    public static void Install(string gameRoot, ManualLogSource log)
    {
        _log = log;
        _sandboxRoot = Path.GetFullPath(Path.Combine(gameRoot, "CrabtyaData", "IsolatedSave"));
        Directory.CreateDirectory(_sandboxRoot);
        log.LogInfo($"SessionIsolationGuard: save sandbox root set to '{_sandboxRoot}'.");

        var harmony = new Harmony("eic.modloader.sessionisolation");
        var installed = 0;

        installed += PatchPrefix(harmony, SaveManagerTypeName, "get_BASE_SAVE_MANAGER_DATA_PATH", 0, nameof(OverrideBaseSaveManagerDataPath), log);
        installed += PatchPrefix(harmony, SaveManagerTypeName, "get_LAST_USED_SAVE_SLOT_PATH", 0, nameof(OverrideLastUsedSaveSlotPath), log);
        installed += PatchPrefix(harmony, SaveManagerTypeName, "get_SaveFileLocation", 0, nameof(OverrideSaveFileLocation), log);
        installed += PatchPrefix(harmony, SaveManagerTypeName, "GetSaveFolderPath", 1, nameof(OverrideSaveFolderPath), log);
        installed += PatchPrefix(harmony, SaveManagerTypeName, "GetSaveDataFilePath", 4, nameof(OverrideSaveDataFilePath), log);

        installed += PatchPrefix(harmony, SteamAchievementManagerTypeName, "UnlockAchievement", 2, nameof(BlockAchievementWrite), log);
        installed += PatchPrefix(harmony, SteamAchievementManagerTypeName, "SetStatProgress", 3, nameof(BlockAchievementWrite), log);

        installed += PatchPrefix(harmony, SteamUserStatsTypeName, "SetAchievement", 1, nameof(BlockSteamWrite), log);
        installed += PatchPrefix(harmony, SteamUserStatsTypeName, "IndicateAchievementProgress", 3, nameof(BlockSteamWrite), log);
        installed += PatchPrefix(harmony, SteamUserStatsTypeName, "StoreStats", 0, nameof(BlockSteamWrite), log);
        installed += PatchPrefix(harmony, SteamUserStatsTypeName, "SetStat", 2, nameof(BlockSteamWrite), log);
        installed += PatchPrefix(harmony, SteamUserStatsTypeName, "UpdateAvgRateStat", 3, nameof(BlockSteamWrite), log);
        installed += PatchPrefix(harmony, SteamUserStatsTypeName, "ResetAllStats", 1, nameof(BlockSteamWrite), log);

        log.LogInfo($"SessionIsolationGuard: installation complete. Installed={installed} patches.");
    }

    private static int PatchPrefix(
        Harmony harmony,
        string typeName,
        string methodName,
        int parameterCount,
        string patchMethodName,
        ManualLogSource log)
    {
        var target = ResolveMethod(typeName, methodName, parameterCount);
        if (target is null)
        {
            log.LogWarning($"SessionIsolationGuard: target not found for patch {typeName}::{methodName}/{parameterCount}.");
            return 0;
        }

        var patch = typeof(SessionIsolationGuard).GetMethod(patchMethodName, BindingFlags.NonPublic | BindingFlags.Static);
        if (patch is null)
        {
            log.LogError($"SessionIsolationGuard: patch method missing: {patchMethodName}.");
            return 0;
        }

        harmony.Patch(target, prefix: new HarmonyMethod(patch));
        log.LogInfo($"SessionIsolationGuard: patched {target.DeclaringType?.FullName}::{target.Name}.");
        return 1;
    }

    private static MethodInfo? ResolveMethod(string typeName, string methodName, int parameterCount)
    {
        var type = ResolveType(typeName);
        if (type is null)
        {
            return null;
        }

        return type
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .FirstOrDefault(m => string.Equals(m.Name, methodName, StringComparison.Ordinal)
                                 && m.GetParameters().Length == parameterCount);
    }

    private static Type? ResolveType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? type;
            try
            {
                type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
            }
            catch
            {
                continue;
            }

            if (type is not null)
            {
                return type;
            }
        }

        return null;
    }

    private static bool OverrideBaseSaveManagerDataPath(ref string __result)
    {
        __result = _sandboxRoot;
        return false;
    }

    private static bool OverrideLastUsedSaveSlotPath(ref string __result)
    {
        __result = Path.Combine(_sandboxRoot, "last-used-slot.txt");
        return false;
    }

    private static bool OverrideSaveFileLocation(ref Il2CppDirectoryInfo __result)
    {
        __result = BuildDirectoryInfo(_sandboxRoot);
        return false;
    }

    private static bool OverrideSaveFolderPath(ref Il2CppDirectoryInfo __result, object __0)
    {
        var saveTypeName = SanitizePathComponent(__0?.ToString(), "default-save-type");
        var folder = Path.Combine(_sandboxRoot, saveTypeName);
        __result = BuildDirectoryInfo(folder);
        LogFirstSaveRedirection(folder);
        return false;
    }

    private static bool OverrideSaveDataFilePath(ref Il2CppFileInfo __result, object __0, object __1, object __2, int __3)
    {
        var fileTypeName = SanitizePathComponent(__0?.ToString(), "unknown-file-type");
        var slotName = SanitizePathComponent(__1?.ToString(), "default-slot");
        var saveTypeName = SanitizePathComponent(__2?.ToString(), "default-save-type");
        var saveDirectory = Path.Combine(_sandboxRoot, saveTypeName, slotName);
        Directory.CreateDirectory(saveDirectory);

        var fileName = $"{fileTypeName}.v{Math.Max(0, __3)}.dat";
        var filePath = Path.Combine(saveDirectory, fileName);
        __result = new Il2CppFileInfo(filePath);
        LogFirstSaveRedirection(filePath);
        return false;
    }

    private static bool BlockAchievementWrite(ref bool __result, MethodBase __originalMethod)
    {
        __result = true;
        _achievementWriteBypassCount++;
        if (_achievementWriteBypassCount <= 5 || _achievementWriteBypassCount % 50 == 0)
        {
            _log?.LogInfo(
                $"SessionIsolationGuard: blocked achievement sink write. Method={__originalMethod.DeclaringType?.FullName}::{__originalMethod.Name}, Count={_achievementWriteBypassCount}.");
        }

        return false;
    }

    private static bool BlockSteamWrite(ref bool __result, MethodBase __originalMethod)
    {
        __result = true;
        _steamWriteBypassCount++;
        if (_steamWriteBypassCount <= 5 || _steamWriteBypassCount % 50 == 0)
        {
            _log?.LogInfo(
                $"SessionIsolationGuard: blocked Steam stats/achievement write. Method={__originalMethod.DeclaringType?.FullName}::{__originalMethod.Name}, Count={_steamWriteBypassCount}.");
        }

        return false;
    }

    private static Il2CppDirectoryInfo BuildDirectoryInfo(string path)
    {
        Directory.CreateDirectory(path);
        return new Il2CppDirectoryInfo(path);
    }

    private static string SanitizePathComponent(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var chars = value
            .Trim()
            .Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-')
            .ToArray();
        var normalized = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static void LogFirstSaveRedirection(string path)
    {
        if (_saveRedirectLogEmitted)
        {
            return;
        }

        _saveRedirectLogEmitted = true;
        _log?.LogInfo($"SessionIsolationGuard: base-game save path redirected into sandbox. Sample='{path}'.");
    }
}
