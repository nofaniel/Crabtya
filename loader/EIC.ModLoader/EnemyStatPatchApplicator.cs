using System.Reflection;
using System.Text.Json;
using BepInEx.Logging;
using HarmonyLib;

namespace EIC.ModLoader;

/// <summary>
/// Applies <c>enemy.stat.*</c> and <c>enemy.&lt;archetype&gt;.stat.*</c> balance patches to
/// <see cref="World.Characters.Enemies.BasicEnemyCharacter"/> instances via a Harmony postfix
/// on <c>InitEnemyInstanceStatsIfNeeded</c>.
///
/// <para>Hook type: <c>World.Characters.Enemies.BasicEnemyCharacter</c></para>
/// <para>Hook method: <c>InitEnemyInstanceStatsIfNeeded</c></para>
/// <para>Stats resolved from: <c>BasicEnemyCharacter.EntityStats</c> (type <c>World.Stats.EnemyStats</c>)</para>
/// <para>Archetype resolved from: <c>BasicEnemyCharacter.EnemyArchetype</c> (type <c>EEnemyArchetype</c>)</para>
///
/// <para>Confirmed enemy stats properties (from interop DLL inspection 2026-05-13):</para>
/// <para>AttackAreaModifier, BaseAbilityDamage, BasePhysicalDamage, CataclysmReductionPercentage,
/// CharmResistance, ChaseSpeed, ColdAdaptation, CooldownModifier, FeedingDistance, FeedingSpeed,
/// FleeSpeed, GeneralDamageMultiplier, HeatAdaptation, HpRegeneration, MaxHp,
/// MaxHpIncreaseFromHpFood, MaxPlating, MovementSpeed, NumExtraPoisonTics, PoisonDamageMultiplier,
/// PoisonReductionPercentage, PoisonTicksSpeedModifier, SandTerrainAdaptation,
/// ShieldedStatusDamageResistance, Size, SnowTerrainAdaptation, SoakAdaptation,
/// SprintRecoveryImpactSpeedMultiplier, SprintSpeedMultiplier, StunDurationMultiplier,
/// StunReductionPercentage, TerrainAdaptation, TurningSpeed, WaterTerrainAdaptation</para>
///
/// <para>Confirmed EEnemyArchetype values (partial): Invalid, BlobFish, BlobFish_Mid, BlobFish_Late,
/// Beeware, Beeware_Mid, Beeware_Late, SnowHare, SnowHare_Mid, SnowHare_Late, Turtoid,
/// Turtoid_Mid, Turtoid_Late, HatBirb, HatBirb_Mid, HatBirb_Late, Pantther, Pantther_Mid,
/// Pantther_Late, Mimic, Mimic_Mid, Mimic_Late, Thief, Thief_Mid, Thief_Late, Sandshark,
/// Sandshark_Mid, Sandshark_Late, Spitfish, Spitfish_Mid, Spitfish_Late, Spiderfrog,
/// Spiderfrog_Mid, Spiderfrog_Late, Crabbybara, Crabbybara_Mid, Boss_Crabtaur,
/// Boss_Aquaconda, Boss_SpiderCrabette, Boss_Shellephant, Boss_Krabaroo,
/// FinalBoss_Krabken, Special_Spiderling, Special_Krabken_Tentacle, and various Baby/Mid/Late.</para>
/// </summary>
public static class EnemyStatPatchApplicator
{
    // Target prefix for global (all-enemy) patches: enemy.stat.<member>
    private const string GlobalPrefix = "enemy.stat.";

    // Target prefix for per-archetype patches: enemy.<archetype>.stat.<member>
    private const string EnemyPrefix = "enemy.";
    private const string StatSegment = ".stat.";

    private static readonly string[] HookTypeNames =
    {
        "World.Characters.Enemies.BasicEnemyCharacter",
        "BasicEnemyCharacter"
    };

    private const string HookMethodName = "InitEnemyInstanceStatsIfNeeded";

    private static ManualLogSource _log;
    private static bool _hookFired;
    private static string _hookTargetSignature = "<unset>";

    public static void Install(ManualLogSource log)
    {
        _log = log;

        var target = ResolveHook(log, out var diagnostics);
        if (target is null)
        {
            diagnostics = string.IsNullOrWhiteSpace(diagnostics) ? "none" : diagnostics;
            log.LogError(
                $"EnemyStatPatchApplicator: unable to find hook '{HookMethodName}' on " +
                $"[{string.Join(", ", HookTypeNames)}]. Diagnostics={diagnostics}. " +
                $"Enemy stat patches will not apply.");
            return;
        }

        _hookTargetSignature = GetMethodSignature(target);
        var harmony = new Harmony("eic.modloader.enemystatpatchapplicator");
        var postfix = new HarmonyMethod(
            typeof(EnemyStatPatchApplicator).GetMethod(
                nameof(OnEnemyInit),
                BindingFlags.NonPublic | BindingFlags.Static));
        harmony.Patch(target, postfix: postfix);

        log.LogInfo($"EnemyStatPatchApplicator: hook installed on {_hookTargetSignature}");
    }

    // ---------------------------------------------------------------------------
    // Hook resolution
    // ---------------------------------------------------------------------------

    private static MethodInfo ResolveHook(ManualLogSource log, out string diagnostics)
    {
        diagnostics = string.Empty;

        foreach (var typeName in HookTypeNames)
        {
            var type = ResolveType(typeName);
            if (type is null)
            {
                continue;
            }

            var method = type.GetMethod(
                HookMethodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (method is not null)
            {
                log.LogInfo($"EnemyStatPatchApplicator: resolved hook {GetMethodSignature(method)}");
                return method;
            }

            diagnostics = $"Type '{type.FullName}' found but method '{HookMethodName}' is missing.";
        }

        return null;
    }

    // ---------------------------------------------------------------------------
    // Harmony postfix
    // ---------------------------------------------------------------------------

    private static void OnEnemyInit(object __instance)
    {
        if (!_hookFired)
        {
            _hookFired = true;
            _log?.LogInfo(
                $"EnemyStatPatchApplicator: enemy init hook fired. Hook={_hookTargetSignature}");
        }

        var enemyStats = ResolveEnemyStats(__instance);
        if (enemyStats is null)
        {
            _log?.LogWarning(
                $"EnemyStatPatchApplicator: hook fired on '{__instance?.GetType().FullName ?? "<null>"}' " +
                $"but no EnemyStats instance could be resolved. Patches skipped for this enemy.");
            return;
        }

        var archetype = TryGetArchetypeString(__instance);
        ApplyRegisteredPatches(enemyStats, archetype);
    }

    // ---------------------------------------------------------------------------
    // Stats / archetype resolution
    // ---------------------------------------------------------------------------

    private static object ResolveEnemyStats(object instance)
    {
        if (instance is null)
        {
            return null;
        }

        var instanceType = instance.GetType();

        // Primary: EntityStats property (typed EnemyStats on BasicEnemyCharacter)
        var entityStatsProp = instanceType.GetProperty(
            "EntityStats",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (entityStatsProp is not null)
        {
            try
            {
                var stats = entityStatsProp.GetValue(instance);
                if (stats is not null)
                {
                    return stats;
                }
            }
            catch
            {
                // Fall through.
            }
        }

        // Fallback: _enemyStatsForThisInstance backing field
        var backingField = instanceType.GetField(
            "_enemyStatsForThisInstance",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (backingField is not null)
        {
            try
            {
                var stats = backingField.GetValue(instance);
                if (stats is not null)
                {
                    return stats;
                }
            }
            catch
            {
                // Give up.
            }
        }

        return null;
    }

    private static string TryGetArchetypeString(object instance)
    {
        if (instance is null)
        {
            return null;
        }

        var instanceType = instance.GetType();

        // Try common names for the archetype enum field/property.
        foreach (var name in new[] { "EnemyArchetype", "_enemyArchetype", "Archetype" })
        {
            var prop = instanceType.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (prop is not null)
            {
                try
                {
                    var value = prop.GetValue(instance);
                    if (value is not null)
                    {
                        return value.ToString();
                    }
                }
                catch { }
            }

            var field = instanceType.GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (field is not null)
            {
                try
                {
                    var value = field.GetValue(instance);
                    if (value is not null)
                    {
                        return value.ToString();
                    }
                }
                catch { }
            }
        }

        return null;
    }

    // ---------------------------------------------------------------------------
    // Patch application
    // ---------------------------------------------------------------------------

    private static void ApplyRegisteredPatches(object enemyStats, string archetypeStr)
    {
        var patches = RuntimeContentState.Current.BalancePatches;
        if (patches.Count == 0)
        {
            return;
        }

        var statsType = enemyStats.GetType();

        foreach (var patch in patches.Values)
        {
            string memberName;
            string appliedArchetype;

            if (patch.Target.StartsWith(GlobalPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // Global: enemy.stat.<member> — applies to every enemy regardless of archetype.
                memberName = patch.Target[GlobalPrefix.Length..];
                appliedArchetype = "all";
            }
            else if (patch.Target.StartsWith(EnemyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // Per-archetype: enemy.<archetype>.stat.<member>
                var rest = patch.Target[EnemyPrefix.Length..];
                var statIdx = rest.IndexOf(StatSegment, StringComparison.OrdinalIgnoreCase);
                if (statIdx < 0)
                {
                    // Not a stat patch (e.g. enemy.baseSprite visual patch) — skip silently.
                    continue;
                }

                var targetArchetype = rest[..statIdx];
                memberName = rest[(statIdx + StatSegment.Length)..];

                // If we couldn't determine the archetype at runtime, skip per-archetype patches.
                if (string.IsNullOrWhiteSpace(archetypeStr))
                {
                    continue;
                }

                if (!string.Equals(targetArchetype, archetypeStr, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                appliedArchetype = archetypeStr;
            }
            else
            {
                // Not an enemy patch — handled by another applicator.
                continue;
            }

            if (string.IsNullOrWhiteSpace(memberName))
            {
                _log?.LogWarning(
                    $"EnemyStatPatchApplicator: patch '{patch.Id}' has empty member name and was skipped.");
                continue;
            }

            if (!TryGetNumericValue(patch.Value, out var patchValue))
            {
                _log?.LogWarning(
                    $"EnemyStatPatchApplicator: patch '{patch.Id}' has non-numeric value " +
                    $"'{patch.GetValuePreview()}' and was skipped.");
                continue;
            }

            if (!TryApplyPatch(statsType, enemyStats, memberName, patch, patchValue, out var applyLog))
            {
                _log?.LogWarning(
                    $"EnemyStatPatchApplicator: patch '{patch.Id}' failed for member '{memberName}' " +
                    $"on {statsType.FullName}. Details={applyLog}");
                continue;
            }

            _log?.LogInfo(
                $"EnemyStatPatchApplicator: patch '{patch.Id}' applied (archetype={appliedArchetype}). {applyLog}");
        }
    }

    // ---------------------------------------------------------------------------
    // Patch application helpers (mirrors BalancePatchApplicator)
    // ---------------------------------------------------------------------------

    private static bool TryApplyPatch(
        Type statsType,
        object statsInstance,
        string memberName,
        BalancePatchDefinition patch,
        float patchValue,
        out string applyLog)
    {
        applyLog = string.Empty;

        var property = statsType.GetProperty(
            memberName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

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

        var field = statsType.GetField(
            memberName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

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

    // ---------------------------------------------------------------------------
    // Shared reflection helpers
    // ---------------------------------------------------------------------------

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
}
