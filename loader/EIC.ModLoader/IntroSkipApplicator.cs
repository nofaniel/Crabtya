using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Video;

namespace EIC.ModLoader;

public static class IntroSkipApplicator
{
    private static readonly string[] CandidateMethods =
    {
        "UnityEngine.Video.VideoPlayer:Play",
        "UnityEngine.Video.VideoPlayer:Prepare",
        "UnityEngine.Video.VideoPlayer:set_playOnAwake"
    };

    private static ManualLogSource _log;
    private static int _loggedVideoProbeCount;
    private static readonly HashSet<string> LoggedFastForwardKeys = new(StringComparer.OrdinalIgnoreCase);

    public static void Install(ManualLogSource log)
    {
        _log = log;
        var harmony = new Harmony("eic.modloader.introskipapplicator");
        var installed = 0;

        foreach (var candidate in CandidateMethods)
        {
            var target = ResolveMethod(candidate);
            if (target is null)
            {
                log.LogWarning($"IntroSkipApplicator: hook candidate not found: {candidate}");
                continue;
            }

            var prefix = new HarmonyMethod(
                typeof(IntroSkipApplicator).GetMethod(nameof(OnVideoPlaybackPrefix), BindingFlags.NonPublic | BindingFlags.Static));
            harmony.Patch(target, prefix: prefix);
            installed++;
            log.LogInfo($"IntroSkipApplicator: hook installed: {candidate}");
        }

        log.LogInfo($"IntroSkipApplicator: hook installation complete. Installed={installed}, Candidates={CandidateMethods.Length}");
    }

    public static void ApplyToLiveVideoPlayers()
    {
        var definitions = RuntimeContentState.Current.IntroSkips.Values.ToList();
        if (definitions.Count == 0)
        {
            return;
        }

        VideoPlayer[] players;
        try
        {
            players = UnityEngine.Object.FindObjectsOfType<VideoPlayer>();
        }
        catch
        {
            return;
        }

        for (var i = 0; i < players.Length; i++)
        {
            var player = players[i];
            if (player is null)
            {
                continue;
            }

            var probe = BuildSourceProbe(player);
            if (!ShouldSkipAny(definitions, probe))
            {
                continue;
            }

            if (TryFastForwardVideoPlayer(player, "live-scan", out var details))
            {
                LogFastForwardOnce(player, $"IntroSkipApplicator: fast-forwarded live video player. Probe='{probe}', Details={details}");
            }
        }
    }

    private static bool OnVideoPlaybackPrefix(object __instance)
    {
        var definitions = RuntimeContentState.Current.IntroSkips.Values.ToList();
        if (definitions.Count == 0 || __instance is null)
        {
            return true;
        }

        var probe = BuildSourceProbe(__instance);
        if (_loggedVideoProbeCount < 5)
        {
            _loggedVideoProbeCount++;
            _log?.LogInfo($"IntroSkipApplicator: observed video probe '{probe}'.");
        }

        if (!ShouldSkipAny(definitions, probe))
        {
            return true;
        }

        if (TryFastForwardVideoPlayer(__instance, "play-prefix", out var details))
        {
            LogFastForwardOnce(__instance, $"IntroSkipApplicator: fast-forwarded intro video playback by hook. Probe='{probe}', Details={details}");
        }

        return true;
    }

    private static bool ShouldSkipAny(IReadOnlyList<IntroSkipDefinition> definitions, string probe)
    {
        for (var i = 0; i < definitions.Count; i++)
        {
            if (ShouldSkip(definitions[i], probe))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldSkip(IntroSkipDefinition definition, string probe)
    {
        if (string.Equals(definition.Mode, "all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var containsRules = definition.MatchContains
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();
        if (containsRules.Count == 0)
        {
            containsRules.Add("intro");
            containsRules.Add("logo");
            containsRules.Add("splash");
            containsRules.Add("opening");
            containsRules.Add("cinematic");
            containsRules.Add("cutscene");
        }

        for (var i = 0; i < containsRules.Count; i++)
        {
            if (probe.Contains(containsRules[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFastForwardVideoPlayer(object videoPlayerInstance, string source, out string details)
    {
        details = string.Empty;
        if (videoPlayerInstance is VideoPlayer vp)
        {
            var changed = false;
            var detailParts = new List<string>();

            try
            {
                if (vp.canSetPlaybackSpeed)
                {
                    vp.playbackSpeed = 16f;
                    detailParts.Add("playbackSpeed=16");
                    changed = true;
                }
            }
            catch
            {
                // best effort
            }

            try
            {
                if (vp.frameCount > 2)
                {
                    vp.frame = (long)vp.frameCount - 2;
                    detailParts.Add($"frame={vp.frame}/{vp.frameCount}");
                    changed = true;
                }
            }
            catch
            {
                // best effort
            }

            if (!changed)
            {
                try
                {
                    if (vp.length > 0.1d)
                    {
                        vp.time = Math.Max(0.0d, vp.length - 0.05d);
                        detailParts.Add($"time={vp.time:0.###}/{vp.length:0.###}");
                        changed = true;
                    }
                }
                catch
                {
                    // best effort
                }
            }

            details = detailParts.Count == 0 ? $"source={source}, no-seek-available" : $"{string.Join(", ", detailParts)}, source={source}";
            return changed;
        }

        return false;
    }

    private static void LogFastForwardOnce(object videoPlayerInstance, string message)
    {
        var key = videoPlayerInstance.GetHashCode().ToString();
        if (!LoggedFastForwardKeys.Add(key))
        {
            return;
        }

        _log?.LogInfo(message);
    }

    private static string BuildSourceProbe(object videoPlayerInstance)
    {
        var type = videoPlayerInstance.GetType();
        var parts = new List<string>
        {
            type.FullName ?? type.Name
        };

        TryAppendMember(parts, videoPlayerInstance, type, "url");
        TryAppendMember(parts, videoPlayerInstance, type, "clip");
        TryAppendMember(parts, videoPlayerInstance, type, "name");
        TryAppendMember(parts, videoPlayerInstance, type, "gameObject");
        return string.Join(" | ", parts);
    }

    private static void TryAppendMember(List<string> parts, object instance, Type type, string memberName)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        try
        {
            var property = type.GetProperty(memberName, flags);
            if (property is not null && property.CanRead)
            {
                var value = property.GetValue(instance);
                if (value is not null)
                {
                    parts.Add($"{memberName}={value}");
                }
            }
        }
        catch
        {
            // best effort only
        }
    }

    private static Type ResolveType(string typeName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var type = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
                if (type is not null)
                {
                    return type;
                }
            }
            catch
            {
                // keep probing
            }
        }

        return null;
    }

    private static MethodInfo ResolveMethod(string candidate)
    {
        var separatorIndex = candidate.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= candidate.Length - 1)
        {
            return null;
        }

        var typeName = candidate[..separatorIndex];
        var methodName = candidate[(separatorIndex + 1)..];

        var type = ResolveType(typeName);
        if (type is null)
        {
            return null;
        }

        try
        {
            return type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        }
        catch
        {
            return null;
        }
    }
}
