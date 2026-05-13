using System.Reflection;
using System.Text.Json;
using BepInEx.Logging;
using HarmonyLib;

namespace EIC.ModLoader;

public static class BalancePatchApplicator
{
    private const string TargetPrefix = "player.stat.";

    private static readonly string[] PreferredTypeNames =
    {
        "World.Stats.PlayerStats",
        "PlayerStats"
    };

    private static readonly string[] PreferredMethodNames =
    {
        "ResetStatsToDefaultState",
        "MarkStatsWhichCanBeReceiveStatChangeMultiplierEffect",
        "EnableEnforceRangeLimits"
    };

    private static ManualLogSource _log;
    private static bool _hookFired;
    private static string _hookTargetSignature = "<unset>";

    public static void Install(ManualLogSource log)
    {
        _log = log;

        var target = ResolvePreferredHook(log, out var diagnostics);
        if (target is null)
        {
            diagnostics = string.IsNullOrWhiteSpace(diagnostics) ? "none" : diagnostics;
            log.LogError(
                $"BalancePatchApplicator: unable to find a supported player-stats refresh hook. Expected type 'World.Stats.PlayerStats' and one of methods [{string.Join(", ", PreferredMethodNames)}]. Diagnostics={diagnostics}. Balance patches will not apply.");
            return;
        }

        _hookTargetSignature = GetMethodSignature(target);
        var harmony = new Harmony("eic.modloader.balancepatchapplicator");
        var postfix = new HarmonyMethod(typeof(BalancePatchApplicator).GetMethod(nameof(OnStatsRefresh), BindingFlags.NonPublic | BindingFlags.Static));
        harmony.Patch(target, postfix: postfix);

        log.LogInfo($"BalancePatchApplicator: hook installed on {_hookTargetSignature}");
    }

    private static MethodInfo ResolvePreferredHook(ManualLogSource log, out string diagnostics)
    {
        diagnostics = string.Empty;

        foreach (var typeName in PreferredTypeNames)
        {
            var type = ResolveType(typeName);
            if (type is null)
            {
                continue;
            }

            foreach (var methodName in PreferredMethodNames)
            {
                var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (method is not null)
                {
                    log.LogInfo($"BalancePatchApplicator: resolved preferred hook candidate {GetMethodSignature(method)}");
                    return method;
                }
            }

            diagnostics = $"Type '{type.FullName}' found but methods missing.";
        }

        var forceRefreshCandidates = DiscoverMethodsByName("ForceRefreshStats", 4);
        if (forceRefreshCandidates.Count > 0)
        {
            var candidateSummary = string.Join(", ", forceRefreshCandidates);
            diagnostics = string.IsNullOrWhiteSpace(diagnostics)
                ? $"Observed ForceRefreshStats on: {candidateSummary}"
                : $"{diagnostics} Observed ForceRefreshStats on: {candidateSummary}";
        }

        return null;
    }

    private static void OnStatsRefresh(object __instance)
    {
        if (!_hookFired)
        {
            _hookFired = true;
            _log?.LogInfo($"BalancePatchApplicator: stats refresh hook fired. Hook={_hookTargetSignature}");
        }

        var playerStats = ResolvePlayerStats(__instance);
        if (playerStats is null)
        {
            _log?.LogWarning(
                $"BalancePatchApplicator: hook fired on '{__instance?.GetType().FullName ?? "<null>"}' but no player stats instance could be resolved. Hook={_hookTargetSignature}");
            return;
        }

        ApplyRegisteredPatches(playerStats);
    }

    private static object ResolvePlayerStats(object instance)
    {
        if (instance is null)
        {
            return null;
        }

        var instanceType = instance.GetType();
        if (string.Equals(instanceType.FullName, "World.Stats.PlayerStats", StringComparison.Ordinal))
        {
            return instance;
        }

        var entityStatsProperty = instanceType.GetProperty("EntityStats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (entityStatsProperty is not null)
        {
            try
            {
                var entityStats = entityStatsProperty.GetValue(instance);
                if (entityStats is not null && string.Equals(entityStats.GetType().FullName, "World.Stats.PlayerStats", StringComparison.Ordinal))
                {
                    return entityStats;
                }
            }
            catch
            {
                // Ignore and continue with singleton fallback.
            }
        }

        var singleton = TryResolvePlayerStatsSingleton();
        if (singleton is not null)
        {
            return singleton;
        }

        return null;
    }

    private static object TryResolvePlayerStatsSingleton()
    {
        var type = ResolveType("World.Stats.PlayerStats") ?? ResolveType("PlayerStats");
        if (type is null)
        {
            return null;
        }

        var instanceProperty = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (instanceProperty is not null)
        {
            try
            {
                var value = instanceProperty.GetValue(null);
                if (value is not null)
                {
                    return value;
                }
            }
            catch
            {
                // Ignore and continue.
            }
        }

        var getter = type.GetMethod("get_Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (getter is not null)
        {
            try
            {
                var value = getter.Invoke(null, null);
                if (value is not null)
                {
                    return value;
                }
            }
            catch
            {
                // Ignore and continue.
            }
        }

        return null;
    }

    private static void ApplyRegisteredPatches(object playerStats)
    {
        var patches = RuntimeContentState.Current.BalancePatches;
        if (patches.Count == 0)
        {
            return;
        }

        var statsType = playerStats.GetType();
        foreach (var patch in patches.Values)
        {
            if (!patch.Target.StartsWith(TargetPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryGetNumericValue(patch.Value, out var patchValue))
            {
                _log?.LogWarning($"BalancePatchApplicator: patch '{patch.Id}' has non-numeric value '{patch.GetValuePreview()}' and was skipped.");
                continue;
            }

            var memberName = patch.Target[TargetPrefix.Length..];
            if (string.IsNullOrWhiteSpace(memberName))
            {
                _log?.LogWarning($"BalancePatchApplicator: patch '{patch.Id}' has empty member target and was skipped.");
                continue;
            }

            if (!TryApplyPatch(statsType, playerStats, memberName, patch, patchValue, out var applyLog))
            {
                _log?.LogWarning(
                    $"BalancePatchApplicator: patch '{patch.Id}' failed for member '{memberName}' on {statsType.FullName}. Details={applyLog}");
                continue;
            }

            _log?.LogInfo($"BalancePatchApplicator: patch '{patch.Id}' applied. {applyLog}");
        }
    }

    private static bool TryApplyPatch(
        Type statsType,
        object statsInstance,
        string memberName,
        BalancePatchDefinition patch,
        float patchValue,
        out string applyLog)
    {
        applyLog = string.Empty;

        var property = statsType.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (property is not null)
        {
            if (!property.CanRead)
            {
                applyLog = "property exists but is not readable";
                return false;
            }

            object propertyValue;
            try
            {
                propertyValue = property.GetValue(statsInstance);
            }
            catch (Exception ex)
            {
                applyLog = $"property read failed: {ex.Message}";
                return false;
            }

            if (propertyValue is null)
            {
                applyLog = "property value is null";
                return false;
            }

            if (IsNumericType(property.PropertyType))
            {
                if (!property.CanWrite)
                {
                    applyLog = "numeric property is read-only";
                    return false;
                }

                var before = Convert.ToSingle(propertyValue);
                var after = ApplyOperation(patch.Operation, before, patchValue);
                property.SetValue(statsInstance, Convert.ChangeType(after, property.PropertyType));
                applyLog = $"Member={memberName}, Op={patch.Operation}, Patch={patchValue}, Before={before}, After={after}";
                return true;
            }

            if (!TryApplyToValueHolder(property.PropertyType, propertyValue, patch.Operation, patchValue, memberName, out applyLog))
            {
                return false;
            }

            if (property.CanWrite)
            {
                try
                {
                    property.SetValue(statsInstance, propertyValue);
                }
                catch
                {
                    // Value holder mutation is usually enough; ignore if setter is blocked.
                }
            }

            return true;
        }

        var field = statsType.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field is null)
        {
            applyLog = $"field/property not found. Available={FormatAvailableMembers(statsType)}";
            return false;
        }

        object fieldValue;
        try
        {
            fieldValue = field.GetValue(statsInstance);
        }
        catch (Exception ex)
        {
            applyLog = $"field read failed: {ex.Message}";
            return false;
        }

        if (fieldValue is null)
        {
            applyLog = "field value is null";
            return false;
        }

        if (IsNumericType(field.FieldType))
        {
            var before = Convert.ToSingle(fieldValue);
            var after = ApplyOperation(patch.Operation, before, patchValue);
            field.SetValue(statsInstance, Convert.ChangeType(after, field.FieldType));
            applyLog = $"Member={memberName}, Op={patch.Operation}, Patch={patchValue}, Before={before}, After={after}";
            return true;
        }

        if (!TryApplyToValueHolder(field.FieldType, fieldValue, patch.Operation, patchValue, memberName, out applyLog))
        {
            return false;
        }

        try
        {
            field.SetValue(statsInstance, fieldValue);
        }
        catch
        {
            // Value holder mutation is usually enough; ignore if setter is blocked.
        }

        return true;
    }

    private static bool TryApplyToValueHolder(
        Type memberType,
        object memberValue,
        string operation,
        float patchValue,
        string memberName,
        out string applyLog)
    {
        applyLog = string.Empty;

        var flatProp = memberType.GetProperty("FlatValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (flatProp is not null && flatProp.CanRead && flatProp.CanWrite && IsNumericType(flatProp.PropertyType))
        {
            var before = Convert.ToSingle(flatProp.GetValue(memberValue));
            var after = ApplyOperation(operation, before, patchValue);
            flatProp.SetValue(memberValue, Convert.ChangeType(after, flatProp.PropertyType));
            applyLog = $"Member={memberName}.FlatValue, Op={operation}, Patch={patchValue}, Before={before}, After={after}";
            return true;
        }

        var percentageProp = memberType.GetProperty("PercentageValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (percentageProp is not null && percentageProp.CanRead && percentageProp.CanWrite && IsNumericType(percentageProp.PropertyType))
        {
            var before = Convert.ToSingle(percentageProp.GetValue(memberValue));
            var after = ApplyOperation(operation, before, patchValue);
            percentageProp.SetValue(memberValue, Convert.ChangeType(after, percentageProp.PropertyType));
            applyLog = $"Member={memberName}.PercentageValue, Op={operation}, Patch={patchValue}, Before={before}, After={after}";
            return true;
        }

        var multiplierProp = memberType.GetProperty("PercentageMultiplier", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (multiplierProp is not null && multiplierProp.CanRead && multiplierProp.CanWrite && IsNumericType(multiplierProp.PropertyType))
        {
            var before = Convert.ToSingle(multiplierProp.GetValue(memberValue));
            var after = ApplyOperation(operation, before, patchValue);
            multiplierProp.SetValue(memberValue, Convert.ChangeType(after, multiplierProp.PropertyType));
            applyLog = $"Member={memberName}.PercentageMultiplier, Op={operation}, Patch={patchValue}, Before={before}, After={after}";
            return true;
        }

        applyLog = $"unsupported member type '{memberType.FullName}'";
        return false;
    }

    private static float ApplyOperation(string op, float current, float patch)
    {
        return op.ToLowerInvariant() switch
        {
            "set" => patch,
            "add" => current + patch,
            "multiply" => current * patch,
            _ => current
        };
    }

    private static string FormatAvailableMembers(Type statsType)
    {
        var names = statsType
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(p => p.Name)
            .Concat(statsType
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Select(f => f.Name))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        return names.Count == 0 ? "<none>" : string.Join(", ", names);
    }

    private static bool TryGetNumericValue(JsonElement element, out float value)
    {
        value = 0f;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetSingle(out value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return float.TryParse(
                element.GetString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
        }

        return false;
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(float) || type == typeof(double) || type == typeof(int)
            || type == typeof(long) || type == typeof(short) || type == typeof(byte)
            || type == typeof(uint) || type == typeof(ulong) || type == typeof(decimal);
    }

    private static Type ResolveType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type;
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

    private static string GetMethodSignature(MethodInfo method)
    {
        var parameters = string.Join(
            ", ",
            method.GetParameters().Select(p => p.ParameterType.FullName ?? p.ParameterType.Name));
        return $"{method.DeclaringType?.FullName}::{method.Name}({parameters})";
    }

    private static List<string> DiscoverMethodsByName(string methodName, int maxItems)
    {
        var results = new List<string>();
        if (string.IsNullOrWhiteSpace(methodName) || maxItems <= 0)
        {
            return results;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException rtle)
            {
                types = rtle.Types.Where(t => t is not null).ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var type in types)
            {
                MethodInfo method;
                try
                {
                    method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                }
                catch
                {
                    continue;
                }

                if (method is null)
                {
                    continue;
                }

                results.Add(GetMethodSignature(method));
                if (results.Count >= maxItems)
                {
                    return results;
                }
            }
        }

        return results;
    }
}
