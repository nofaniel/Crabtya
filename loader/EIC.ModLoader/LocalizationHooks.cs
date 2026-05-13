using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace EIC.ModLoader;

public static class LocalizationHooks
{
    private const string VerboseTraceArg = "--eic-localization-trace";
    private const string VerboseTraceFlagFile = "eic-localization-trace.flag";

    private static readonly (string TypeName, string MethodName, bool IncludeRelatedStringMethods)[] Candidates =
    {
        ("UnityEngine.Localization.Settings.LocalizedStringDatabase", "GetLocalizedString", true),
        ("UnityEngine.Localization.Settings.LocalizedStringDatabase", "GetLocalizedStringAsync", false),
        ("UnityEngine.Localization.LocalizedString", "GetLocalizedString", true),
        ("UnityEngine.Localization.LocalizedString", "GetLocalizedStringAsync", false),
        ("UnityEngine.Localization.Components.LocalizeStringEvent", "UpdateString", false)
    };

    private static readonly HashSet<string> Fired = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ObservedLookups = new(StringComparer.OrdinalIgnoreCase);
    private static bool _verboseLookupTracing;

    public static void Install(ManualLogSource log)
    {
        _verboseLookupTracing = IsVerboseLookupTracingEnabled();
        log.LogInfo(
            _verboseLookupTracing
                ? "Localization lookup tracing is enabled (verbose mode)."
                : "Localization lookup tracing is disabled (quiet mode). Use --eic-localization-trace or eic-localization-trace.flag to enable.");

        var harmony = new Harmony("eic.modloader.localizationhooks");
        var probeVoid = typeof(LocalizationHooks).GetMethod(nameof(OnProbeVoid), BindingFlags.NonPublic | BindingFlags.Static);
        var probeUpdateString = typeof(LocalizationHooks).GetMethod(nameof(OnLocalizeStringEventUpdated), BindingFlags.NonPublic | BindingFlags.Static);
        var probeStringResult = typeof(LocalizationHooks).GetMethod(nameof(OnProbeStringResult), BindingFlags.NonPublic | BindingFlags.Static);
        var probeResult = typeof(LocalizationHooks).GetMethod(nameof(OnProbeResult), BindingFlags.NonPublic | BindingFlags.Static);
        if (probeVoid is null || probeUpdateString is null || probeStringResult is null || probeResult is null)
        {
            log.LogWarning("Localization hook probe method(s) not found.");
            return;
        }

        var installed = 0;
        var patchedSignatures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in Candidates)
        {
            var targets = ResolveMethods(candidate.TypeName, candidate.MethodName, candidate.IncludeRelatedStringMethods);
            if (targets.Count == 0)
            {
                log.LogWarning($"Localization hook candidate not found: {candidate.TypeName}:{candidate.MethodName}");
                continue;
            }

            foreach (var target in targets)
            {
                var targetSignature = GetMethodSignature(target);
                if (!patchedSignatures.Add(targetSignature))
                {
                    continue;
                }

                try
                {
                    var useUpdateStringProbe = string.Equals(candidate.TypeName, "UnityEngine.Localization.Components.LocalizeStringEvent", StringComparison.Ordinal) &&
                                               string.Equals(candidate.MethodName, "UpdateString", StringComparison.Ordinal);
                    var postfix = target.ReturnType == typeof(void)
                        ? useUpdateStringProbe
                            ? new HarmonyMethod(probeUpdateString)
                            : new HarmonyMethod(probeVoid)
                        : target.ReturnType == typeof(string)
                            ? new HarmonyMethod(probeStringResult)
                            : new HarmonyMethod(probeResult);
                    harmony.Patch(target, postfix: postfix);
                    installed++;
                    log.LogInfo($"Localization hook installed: {targetSignature}");
                }
                catch (Exception ex)
                {
                    log.LogWarning($"Localization hook install failed for {targetSignature}: {ex.Message}");
                }
            }
        }

        log.LogInfo($"Localization hook installation complete. Installed={installed}, Candidates={Candidates.Length}");
    }

    private static List<MethodInfo> ResolveMethods(string typeName, string methodName, bool includeRelatedStringMethods)
    {
        var resolved = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? type;
            try
            {
                type = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
            }
            catch
            {
                continue;
            }

            if (type is null)
            {
                continue;
            }

            var allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var methods = allMethods
                .Where(m => string.Equals(m.Name, methodName, StringComparison.Ordinal))
                .OrderByDescending(m => m.GetParameters().Length)
                .ToList();

            if (includeRelatedStringMethods)
            {
                var relatedStringMethods = allMethods
                    .Where(m => m.ReturnType == typeof(string) &&
                                m.Name.IndexOf("LocalizedString", StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                methods.AddRange(relatedStringMethods);
            }

            foreach (var method in methods)
            {
                resolved[GetMethodSignature(method)] = method;
            }
        }

        return resolved.Values
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ThenByDescending(m => m.GetParameters().Length)
            .ToList();
    }

    private static string GetMethodSignature(MethodBase method)
    {
        var declaringType = method.DeclaringType?.FullName ?? "<unknown>";
        var parameters = method.GetParameters();
        var parameterSignature = string.Join(", ", parameters.Select(p => p.ParameterType.FullName ?? p.ParameterType.Name));
        var returnType = method is MethodInfo methodInfo
            ? methodInfo.ReturnType.FullName ?? methodInfo.ReturnType.Name
            : "System.Void";
        return $"{declaringType}:{method.Name}({parameterSignature})->{returnType}";
    }

    private static void OnProbeVoid(MethodBase __originalMethod, object?[] __args)
    {
        var signature = GetMethodSignature(__originalMethod);
        if (!Fired.Add(signature))
        {
            LogLookupProbe(signature, __args, null, didOverride: false);
            return;
        }

        Plugin.Instance?.Log.LogInfo($"Localization hook fired: {signature}. ResultType=<void>");
        LogLookupProbe(signature, __args, null, didOverride: false);
    }

    private static void OnLocalizeStringEventUpdated(MethodBase __originalMethod, object __instance, object?[] __args)
    {
        var signature = GetMethodSignature(__originalMethod);
        if (Fired.Add(signature))
        {
            Plugin.Instance?.Log.LogInfo($"Localization hook fired: {signature}. ResultType=<void>");
        }

        if (TryApplyLocalizeStringEventOverride(__instance, out var locale, out var key, out var value))
        {
            Plugin.Instance?.Log.LogInfo($"Localization override applied: Locale={locale}, Key={key}, Source=runtime_registry");
            LogLookupProbeForKey(signature, locale, key, value, didOverride: true);
            return;
        }

        var keys = ExtractLocalizeStringEventKeys(__instance);
        if (keys.Count == 0)
        {
            var fallbackLocale = ResolveLocale(new object?[] { __instance });
            LogLookupProbeForKey(signature, fallbackLocale, "<no-key>", null, didOverride: false);
            return;
        }

        var resolvedLocale = ResolveLocale(new object?[] { __instance });
        foreach (var candidate in keys)
        {
            LogLookupProbeForKey(signature, resolvedLocale, candidate, null, didOverride: false);
        }
    }

    private static void OnProbeStringResult(MethodBase __originalMethod, object?[] __args, ref string __result)
    {
        var signature = GetMethodSignature(__originalMethod);
        if (Fired.Add(signature))
        {
            Plugin.Instance?.Log.LogInfo($"Localization hook fired: {signature}. ResultType={typeof(string).FullName}");
        }

        if (TryResolveRuntimeLocalizationOverride(__args, out var locale, out var key, out var value))
        {
            __result = value;
            Plugin.Instance?.Log.LogInfo($"Localization override applied: Locale={locale}, Key={key}, Source=runtime_registry");
            LogLookupProbe(signature, __args, __result, didOverride: true);
            return;
        }

        LogLookupProbe(signature, __args, __result, didOverride: false);
    }

    private static void OnProbeResult(MethodBase __originalMethod, object?[] __args, object? __result)
    {
        var signature = GetMethodSignature(__originalMethod);
        if (!Fired.Add(signature))
        {
            LogLookupProbe(signature, __args, __result, didOverride: false);
            return;
        }

        var resultPreview = __result is null ? "<null>" : __result.GetType().FullName ?? __result.ToString() ?? "<unknown>";
        Plugin.Instance?.Log.LogInfo($"Localization hook fired: {signature}. ResultType={resultPreview}");
        LogLookupProbe(signature, __args, __result, didOverride: false);
    }

    private static bool TryResolveRuntimeLocalizationOverride(object?[] args, out string locale, out string key, out string value)
    {
        value = string.Empty;
        key = string.Empty;
        locale = ResolveLocale(args);
        var fallbackLocales = string.Equals(locale, "en", StringComparison.OrdinalIgnoreCase)
            ? new[] { locale }
            : new[] { locale, "en" };

        var keys = ExtractCandidateKeys(args);
        foreach (var candidateKey in keys)
        {
            foreach (var candidateLocale in fallbackLocales)
            {
                if (!RuntimeContentState.TryGetLocalization(candidateLocale, candidateKey, out value))
                {
                    continue;
                }

                key = candidateKey;
                locale = candidateLocale;
                return true;
            }
        }

        return false;
    }

    private static bool TryApplyLocalizeStringEventOverride(object instance, out string locale, out string key, out string value)
    {
        locale = ResolveLocale(new object?[] { instance });
        key = string.Empty;
        value = string.Empty;

        var keys = ExtractLocalizeStringEventKeys(instance);
        if (keys.Count == 0)
        {
            return false;
        }

        var localeCandidates = string.Equals(locale, "en", StringComparison.OrdinalIgnoreCase)
            ? new[] { locale }
            : new[] { locale, "en" };

        foreach (var candidateKey in keys)
        {
            foreach (var candidateLocale in localeCandidates)
            {
                if (!RuntimeContentState.TryGetLocalization(candidateLocale, candidateKey, out var candidateValue))
                {
                    continue;
                }

                if (!TryPushLocalizeStringEventValue(instance, candidateValue))
                {
                    continue;
                }

                locale = candidateLocale;
                key = candidateKey;
                value = candidateValue;
                return true;
            }
        }

        return false;
    }

    private static List<string> ExtractLocalizeStringEventKeys(object instance)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var localizedString = TryGetMemberValue(instance, "StringReference") ?? TryGetMemberValue(instance, "m_StringReference");
        if (localizedString is null)
        {
            return keys.ToList();
        }

        var tableEntryReference = TryGetMemberValue(localizedString, "TableEntryReference") ??
                                  TryGetMemberValue(localizedString, "m_TableEntryReference");
        if (tableEntryReference is not null)
        {
            var directKey = TryGetMemberValue(tableEntryReference, "Key")?.ToString();
            if (TryAddSpecificLocalizeKey(keys, directKey))
            {
                return keys.ToList();
            }

            var referenceKey = TryGetMemberValue(tableEntryReference, "Reference")?.ToString();
            if (TryAddSpecificLocalizeKey(keys, referenceKey))
            {
                return keys.ToList();
            }

            TryAddSpecificLocalizeKey(keys, tableEntryReference.ToString());
        }

        return keys.ToList();
    }

    private static object? TryGetMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();

        var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is not null && property.GetIndexParameters().Length == 0 && property.CanRead)
        {
            try
            {
                return property.GetValue(instance);
            }
            catch
            {
                // ignored
            }
        }

        var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (field is null)
        {
            return null;
        }

        try
        {
            return field.GetValue(instance);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryAddSpecificLocalizeKey(HashSet<string> keys, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var trimmed = candidate.Trim();
        if (trimmed.Length < 3 || trimmed.Length > 180)
        {
            return false;
        }

        if (trimmed.Contains(' ') || trimmed.Contains('\n') || trimmed.Contains('\r'))
        {
            return false;
        }

        if (!trimmed.Any(ch => char.IsLetterOrDigit(ch)))
        {
            return false;
        }

        if (trimmed.Contains("UnityEngine", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("Il2Cpp", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("System.", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        keys.Add(trimmed);
        return true;
    }

    private static bool TryPushLocalizeStringEventValue(object instance, string value)
    {
        var type = instance.GetType();
        var wroteCurrentString = false;

        var currentStringField = type.GetField("m_CurrentString", BindingFlags.Instance | BindingFlags.NonPublic);
        if (currentStringField is not null &&
            TryCreateInvokeArgument(currentStringField.FieldType, value, out var convertedCurrentString))
        {
            try
            {
                currentStringField.SetValue(instance, convertedCurrentString);
                wroteCurrentString = true;
            }
            catch
            {
                // ignored
            }
        }

        object? onUpdateTarget = null;
        var onUpdateProperty = type.GetProperty("OnUpdateString", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (onUpdateProperty is not null && onUpdateProperty.GetIndexParameters().Length == 0 && onUpdateProperty.CanRead)
        {
            try
            {
                onUpdateTarget = onUpdateProperty.GetValue(instance);
            }
            catch
            {
                onUpdateTarget = null;
            }
        }

        onUpdateTarget ??= type.GetField("OnUpdateString", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(instance);
        if (onUpdateTarget is null)
        {
            return wroteCurrentString;
        }

        var invokeCandidates = onUpdateTarget
            .GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => string.Equals(m.Name, "Invoke", StringComparison.Ordinal) && m.GetParameters().Length == 1)
            .ToList();

        if (invokeCandidates.Count == 0)
        {
            return wroteCurrentString;
        }

        foreach (var invoke in invokeCandidates)
        {
            var parameterType = invoke.GetParameters()[0].ParameterType;
            if (!TryCreateInvokeArgument(parameterType, value, out var argument))
            {
                continue;
            }

            try
            {
                invoke.Invoke(onUpdateTarget, new[] { argument });
                return true;
            }
            catch
            {
                // try next invoke candidate
            }
        }

        return wroteCurrentString;
    }

    private static bool TryCreateInvokeArgument(Type parameterType, string value, out object argument)
    {
        argument = value;
        if (parameterType == typeof(string) || parameterType == typeof(object))
        {
            return true;
        }

        if (!string.Equals(parameterType.FullName, "Il2CppSystem.String", StringComparison.Ordinal))
        {
            return false;
        }

        var implicitFromString = parameterType.GetMethod(
            "op_Implicit",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string) },
            modifiers: null);
        if (implicitFromString is null)
        {
            return false;
        }

        try
        {
            var converted = implicitFromString.Invoke(null, new object[] { value });
            if (converted is null)
            {
                return false;
            }

            argument = converted;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void LogLookupProbe(string signature, object?[] args, object? result, bool didOverride)
    {
        var locale = ResolveLocale(args);
        var keys = ExtractCandidateKeys(args);
        foreach (var key in keys)
        {
            LogLookupProbeForKey(signature, locale, key, result, didOverride);
        }
    }

    private static void LogLookupProbeForKey(string signature, string locale, string key, object? result, bool didOverride)
    {
        if (!_verboseLookupTracing)
        {
            return;
        }

        var probeKey = $"{signature}|{locale}|{key}|override={didOverride}";
        if (!ObservedLookups.Add(probeKey))
        {
            return;
        }

        var resultPreview = result is null
            ? "<null>"
            : result is string s
                ? TrimPreview(s)
                : result.GetType().Name;
        Plugin.Instance?.Log.LogInfo($"Localization lookup observed: Method={signature}, Locale={locale}, Key={key}, Override={didOverride}, Result={resultPreview}");
    }

    private static List<string> ExtractCandidateKeys(object?[] args)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Preferred path: extract key from the specific TableEntryReference or StringTableEntry arg.
        // This avoids false-positive matches against TableReference (table-name strings) which can
        // collide with override-registry keys when those table names are scanned as key candidates.
        var hasTableEntryArg = false;
        foreach (var arg in args)
        {
            if (arg is null) continue;
            var typeName = arg.GetType().FullName ?? string.Empty;
            if (!typeName.Contains("TableEntryReference", StringComparison.Ordinal) &&
                !typeName.Contains("StringTableEntry", StringComparison.Ordinal))
            {
                continue;
            }

            hasTableEntryArg = true;
            var key = TryGetMemberValue(arg, "Key")?.ToString();
            if (!TryAddSpecificLocalizeKey(keys, key))
            {
                // TableEntryReference also exposes a Reference string as a secondary path.
                var reference = TryGetMemberValue(arg, "Reference")?.ToString();
                TryAddSpecificLocalizeKey(keys, reference);
            }
        }

        if (hasTableEntryArg)
        {
            // Do NOT fall back to broad scan. TableReference.ToString() returns table names
            // (e.g. "UI_Challenges") that match override keys, causing false-positive overrides
            // for entries in those tables that are not themselves the targeted key.
            return keys.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // Fall back to broad scan for methods that do not include a TableEntryReference arg
        // (e.g. LocalizedString:GetLocalizedString() with no parameters).
        foreach (var arg in args)
        {
            CollectCandidateKeys(arg, keys, 0);
        }

        return keys.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void CollectCandidateKeys(object? arg, HashSet<string> keys, int depth)
    {
        if (arg is null || depth > 2)
        {
            return;
        }

        if (arg is string str)
        {
            TryAddKey(keys, str);
            return;
        }

        var type = arg.GetType();
        if (type.IsPrimitive || type.IsEnum)
        {
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0 || !property.CanRead)
            {
                continue;
            }

            object? value;
            try
            {
                value = property.GetValue(arg);
            }
            catch
            {
                continue;
            }

            if (value is string s)
            {
                if (property.Name.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    property.Name.IndexOf("entry", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    TryAddKey(keys, s);
                }
                else
                {
                    TryAddKey(keys, s);
                }

                continue;
            }

            CollectCandidateKeys(value, keys, depth + 1);
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            object? value;
            try
            {
                value = field.GetValue(arg);
            }
            catch
            {
                continue;
            }

            if (value is string s)
            {
                if (field.Name.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    field.Name.IndexOf("entry", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    TryAddKey(keys, s);
                }

                continue;
            }

            if (field.Name.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0 ||
                field.Name.IndexOf("entry", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                CollectCandidateKeys(value, keys, depth + 1);
            }
        }

        var text = arg.ToString();
        if (!string.IsNullOrWhiteSpace(text))
        {
            TryAddKey(keys, text);
        }
    }

    private static void TryAddKey(HashSet<string> keys, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var candidate = value.Trim();
        if (candidate.Length < 3 || candidate.Length > 180)
        {
            return;
        }

        if (candidate.Contains(' ') || candidate.Contains('\n') || candidate.Contains('\r'))
        {
            return;
        }

        if (!candidate.Any(ch => ch == '.' || ch == '_' || ch == '-'))
        {
            return;
        }

        if (candidate.StartsWith("Il2Cpp", StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith("UnityEngine.", StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains('[') ||
            candidate.Contains(']'))
        {
            return;
        }

        keys.Add(candidate);
    }

    private static string ResolveLocale(object?[] args)
    {
        foreach (var arg in args)
        {
            if (arg is null)
            {
                continue;
            }

            if (arg is string str && LooksLikeLocale(str))
            {
                return str;
            }

            var type = arg.GetType();
            if (type.Name.IndexOf("Locale", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            foreach (var propertyName in new[] { "Identifier", "Code", "LocaleName", "name" })
            {
                var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property is null || property.GetIndexParameters().Length > 0 || !property.CanRead)
                {
                    continue;
                }

                object? value;
                try
                {
                    value = property.GetValue(arg);
                }
                catch
                {
                    continue;
                }

                if (value is string locale && LooksLikeLocale(locale))
                {
                    return locale;
                }

                if (value is not null)
                {
                    var text = value.ToString();
                    if (!string.IsNullOrWhiteSpace(text) && LooksLikeLocale(text))
                    {
                        return text;
                    }
                }
            }
        }

        return "en";
    }

    private static bool LooksLikeLocale(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.Length is >= 2 and <= 10 && trimmed.All(ch => char.IsLetter(ch) || ch == '-' || ch == '_');
    }

    private static string TrimPreview(string value)
    {
        if (value.Length <= 90)
        {
            return value;
        }

        return value[..90] + "...";
    }

    private static bool IsVerboseLookupTracingEnabled()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Any(a => string.Equals(a, VerboseTraceArg, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var markerPath = Path.Combine(AppContext.BaseDirectory, VerboseTraceFlagFile);
        return File.Exists(markerPath);
    }
}
