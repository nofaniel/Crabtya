using System.IO.Compression;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace EIC.ModLoader;

public static class VisualPatchApplicator
{
    // --- Supported target identifiers ---
    private const string PlayerSpriteTarget = "player.baseSprite";
    private const string EnemySpriteTarget = "enemy.baseSprite";
    private const string GameObjectNamedPrefix = "gameobject.named:";
    private const string SpriteNamedPrefix = "sprite.named:";

    private static readonly string[] CandidateMethods =
    {
        "World.Characters.PlayerCharacter:Start",
        "World.Characters.PlayerCharacter:OnEnable",
        "World.Characters.PlayerCharacter:SetupInitialStats"
    };

    private static readonly Dictionary<string, Sprite> SpriteCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> LoggedApplications = new(StringComparer.OrdinalIgnoreCase);
    private static ManualLogSource _log;

    public static void Install(ManualLogSource log)
    {
        _log = log;
        var harmony = new Harmony("eic.modloader.visualpatchapplicator");
        var installed = 0;

        foreach (var candidate in CandidateMethods)
        {
            var target = ResolveMethod(candidate);
            if (target is null)
            {
                log.LogWarning($"VisualPatchApplicator: hook candidate not found: {candidate}");
                continue;
            }

            var postfix = new HarmonyMethod(
                typeof(VisualPatchApplicator).GetMethod(nameof(OnVisualSurfaceUpdated), BindingFlags.NonPublic | BindingFlags.Static));
            harmony.Patch(target, postfix: postfix);
            installed++;
            log.LogInfo($"VisualPatchApplicator: hook installed: {candidate}");
        }

        log.LogInfo($"VisualPatchApplicator: hook installation complete. Installed={installed}, Candidates={CandidateMethods.Length}");
    }

    /// <summary>
    /// Scans all live Unity objects and applies every active visual patch to any
    /// matching target.  Called on a ~1 s tick from <c>RuntimeOverlayBehaviour.Update</c>.
    /// </summary>
    public static void ApplyToLiveObjects()
    {
        var patches = RuntimeContentState.Current.VisualPatches;
        if (patches.Count == 0)
        {
            return;
        }

        // Bucket patches by target type and pre-load sprites.
        var playerPatches = new List<(VisualPatchDefinition p, Sprite s)>();
        var enemyPatches = new List<(VisualPatchDefinition p, Sprite s)>();
        var goNamePatches = new List<(VisualPatchDefinition p, Sprite s, string pattern)>();
        var spriteNamePatches = new List<(VisualPatchDefinition p, Sprite s, string pattern)>();

        foreach (var patch in patches.Values)
        {
            var sprite = GetOrLoadSprite(patch);
            if (sprite is null)
            {
                continue;
            }

            if (string.Equals(patch.Target, PlayerSpriteTarget, StringComparison.OrdinalIgnoreCase))
            {
                playerPatches.Add((patch, sprite));
            }
            else if (string.Equals(patch.Target, EnemySpriteTarget, StringComparison.OrdinalIgnoreCase))
            {
                enemyPatches.Add((patch, sprite));
            }
            else if (patch.Target.StartsWith(GameObjectNamedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var pattern = patch.Target[GameObjectNamedPrefix.Length..];
                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    goNamePatches.Add((patch, sprite, pattern));
                }
            }
            else if (patch.Target.StartsWith(SpriteNamedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var pattern = patch.Target[SpriteNamedPrefix.Length..];
                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    spriteNamePatches.Add((patch, sprite, pattern));
                }
            }
        }

        if (playerPatches.Count == 0 && enemyPatches.Count == 0 &&
            goNamePatches.Count == 0 && spriteNamePatches.Count == 0)
        {
            return;
        }

        // Single FindObjectsOfType pass for component-based targets
        if (playerPatches.Count > 0 || enemyPatches.Count > 0 || goNamePatches.Count > 0)
        {
            Component[] components;
            try
            {
                components = UnityEngine.Object.FindObjectsOfType<Component>();
            }
            catch
            {
                return;
            }

            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component is null)
                {
                    continue;
                }

                var typeName = component.GetType().FullName ?? string.Empty;

                if (playerPatches.Count > 0 &&
                    string.Equals(typeName, "World.Characters.PlayerCharacter", StringComparison.Ordinal))
                {
                    foreach (var (patch, sprite) in playerPatches)
                    {
                        ApplySpriteToComponent(component, sprite, patch);
                    }
                }

                if (enemyPatches.Count > 0 && IsEnemyLikeComponent(typeName))
                {
                    foreach (var (patch, sprite) in enemyPatches)
                    {
                        ApplySpriteToComponent(component, sprite, patch);
                    }
                }

                if (goNamePatches.Count > 0)
                {
                    var goName = component.gameObject?.name ?? string.Empty;
                    foreach (var (patch, sprite, pattern) in goNamePatches)
                    {
                        if (goName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            ApplySpriteToComponent(component, sprite, patch);
                        }
                    }
                }
            }
        }

        // Separate SpriteRenderer scan for sprite-named patches
        if (spriteNamePatches.Count > 0)
        {
            SpriteRenderer[] renderers;
            try
            {
                renderers = UnityEngine.Object.FindObjectsOfType<SpriteRenderer>();
            }
            catch
            {
                return;
            }

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer is null || renderer.sprite is null)
                {
                    continue;
                }

                var spriteName = renderer.sprite.name ?? string.Empty;
                foreach (var (patch, sprite, pattern) in spriteNamePatches)
                {
                    if (!spriteName.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                        ReferenceEquals(renderer.sprite, sprite))
                    {
                        continue;
                    }

                    renderer.sprite = sprite;
                    var logKey = $"named:{renderer.GetInstanceID()}:{patch.Id}";
                    if (LoggedApplications.Add(logKey))
                    {
                        _log?.LogInfo(
                            $"VisualPatchApplicator: sprite-named patch '{patch.Id}' applied to sprite '{spriteName}' on '{renderer.gameObject?.name}'. Mod={patch.ModId}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Returns <c>true</c> when the component type name looks like an enemy/NPC rather
    /// than the player.  Uses common naming conventions found in the TopDownEngine and
    /// game-specific World.Characters namespace without requiring exact class names.
    /// </summary>
    private static bool IsEnemyLikeComponent(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return false;
        }

        if (typeName.Contains("PlayerCharacter", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return typeName.Contains("Enemy", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("EnemyChar", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("Creature", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("Boss", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("Npc", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("Monster", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Applies <paramref name="sprite"/> to all <see cref="SpriteRenderer"/>s that
    /// are children of <paramref name="root"/>.
    /// </summary>
    private static void ApplySpriteToComponent(Component root, Sprite sprite, VisualPatchDefinition patch)
    {
        SpriteRenderer[] renderers;
        try
        {
            renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        }
        catch (Exception ex)
        {
            _log?.LogWarning(
                $"VisualPatchApplicator: unable to enumerate SpriteRenderers on '{root.name}': {ex.Message}");
            return;
        }

        var changed = 0;
        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer is null || renderer.sprite is null || ReferenceEquals(renderer.sprite, sprite))
            {
                continue;
            }

            renderer.sprite = sprite;
            changed++;
        }

        if (changed <= 0)
        {
            return;
        }

        var logKey = $"{root.GetInstanceID()}:{patch.Id}";
        if (LoggedApplications.Add(logKey))
        {
            _log?.LogInfo(
                $"VisualPatchApplicator: patch '{patch.Id}' ({patch.Target}) applied to {root.GetType().Name} '{root.name}'. RendersChanged={changed}, Mod={patch.ModId}");
        }
    }

    private static void OnVisualSurfaceUpdated(object __instance)
    {
        TryApply(__instance);
    }

    private static void TryApply(object source)
    {
        var patch = RuntimeContentState.Current.VisualPatches.Values
            .Where(p => string.Equals(p.Target, PlayerSpriteTarget, StringComparison.OrdinalIgnoreCase))
            .LastOrDefault();
        if (patch is null)
        {
            return;
        }

        var playerComponent = ResolvePlayerComponent(source);
        if (playerComponent is null)
        {
            return;
        }

        var sprite = GetOrLoadSprite(patch);
        if (sprite is null)
        {
            return;
        }

        SpriteRenderer[] renderers;
        try
        {
            renderers = playerComponent.GetComponentsInChildren<SpriteRenderer>(true);
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"VisualPatchApplicator: unable to enumerate player SpriteRenderers: {ex.Message}");
            return;
        }

        var changed = 0;
        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer is null || renderer.sprite is null || ReferenceEquals(renderer.sprite, sprite))
            {
                continue;
            }

            renderer.sprite = sprite;
            changed++;
        }

        if (changed <= 0)
        {
            return;
        }

        var playerId = playerComponent.GetInstanceID();
        var logKey = $"{playerId}:{patch.Id}";
        if (LoggedApplications.Add(logKey))
        {
            _log?.LogInfo(
                $"VisualPatchApplicator: player sprite replacement applied. Patch={patch.Id}, Mod={patch.ModId}, Image={patch.Image}, RenderersChanged={changed}, Player={playerComponent.name}");
        }
    }

    private static Component ResolvePlayerComponent(object source)
    {
        if (source is not Component component)
        {
            return null;
        }

        var typeName = source.GetType().FullName ?? string.Empty;
        if (string.Equals(typeName, "World.Characters.PlayerCharacter", StringComparison.Ordinal))
        {
            return component;
        }

        return null;
    }

    private static Sprite GetOrLoadSprite(VisualPatchDefinition patch)
    {
        if (SpriteCache.TryGetValue(patch.Id, out var cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var image = DecodePng(File.ReadAllBytes(patch.ResolvedImagePath));
            var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false);
            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    var offset = ((y * image.Width) + x) * 4;
                    texture.SetPixel(
                        x,
                        y,
                        new Color32(
                            image.Rgba[offset],
                            image.Rgba[offset + 1],
                            image.Rgba[offset + 2],
                            image.Rgba[offset + 3]));
                }
            }

            texture.Apply(false, false);

            texture.name = $"EIC_{patch.Id}_Texture";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                patch.PixelsPerUnit);
            sprite.name = $"EIC_{patch.Id}_Sprite";
            SpriteCache[patch.Id] = sprite;

            _log?.LogInfo(
                $"VisualPatchApplicator: loaded sprite patch '{patch.Id}'. Size={texture.width}x{texture.height}, PixelsPerUnit={patch.PixelsPerUnit}, Image={patch.Image}");
            return sprite;
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"VisualPatchApplicator: sprite load failed for patch '{patch.Id}': {ex.Message}");
            return null;
        }
    }

    private static DecodedPng DecodePng(byte[] bytes)
    {
        var signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        if (bytes.Length < signature.Length || !signature.SequenceEqual(bytes.Take(signature.Length)))
        {
            throw new InvalidDataException("Image is not a PNG file.");
        }

        var width = 0;
        var height = 0;
        var bitDepth = 0;
        var colorType = 0;
        var compressionMethod = 0;
        var filterMethod = 0;
        var interlaceMethod = 0;
        var idat = new List<byte>();
        var offset = signature.Length;

        while (offset + 12 <= bytes.Length)
        {
            var length = ReadUInt32BigEndian(bytes, offset);
            offset += 4;
            if (length > int.MaxValue || offset + 4 + length + 4 > bytes.Length)
            {
                throw new InvalidDataException("PNG chunk length is invalid.");
            }

            var chunkType = System.Text.Encoding.ASCII.GetString(bytes, offset, 4);
            offset += 4;
            var chunkStart = offset;
            offset += (int)length;
            offset += 4;

            if (chunkType == "IHDR")
            {
                width = (int)ReadUInt32BigEndian(bytes, chunkStart);
                height = (int)ReadUInt32BigEndian(bytes, chunkStart + 4);
                bitDepth = bytes[chunkStart + 8];
                colorType = bytes[chunkStart + 9];
                compressionMethod = bytes[chunkStart + 10];
                filterMethod = bytes[chunkStart + 11];
                interlaceMethod = bytes[chunkStart + 12];
            }
            else if (chunkType == "IDAT")
            {
                for (var i = 0; i < length; i++)
                {
                    idat.Add(bytes[chunkStart + i]);
                }
            }
            else if (chunkType == "IEND")
            {
                break;
            }
        }

        if (width <= 0 || height <= 0 || bitDepth != 8 || compressionMethod != 0 || filterMethod != 0 || interlaceMethod != 0)
        {
            throw new InvalidDataException("PNG must be non-interlaced 8-bit RGBA or RGB.");
        }

        if (colorType != 6 && colorType != 2)
        {
            throw new InvalidDataException($"PNG color type {colorType} is not supported for visual patches.");
        }

        var bytesPerPixel = colorType == 6 ? 4 : 3;
        var stride = width * bytesPerPixel;
        byte[] inflated;
        using (var compressed = new MemoryStream(idat.ToArray()))
        using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
            zlib.CopyTo(output);
            inflated = output.ToArray();
        }

        var expectedMinimum = (stride + 1) * height;
        if (inflated.Length < expectedMinimum)
        {
            throw new InvalidDataException("PNG image data is truncated.");
        }

        var previous = new byte[stride];
        var current = new byte[stride];
        var rgba = new byte[width * height * 4];
        var source = 0;

        for (var pngY = 0; pngY < height; pngY++)
        {
            var filter = inflated[source++];
            for (var x = 0; x < stride; x++)
            {
                var value = inflated[source++];
                var left = x >= bytesPerPixel ? current[x - bytesPerPixel] : 0;
                var up = previous[x];
                var upLeft = x >= bytesPerPixel ? previous[x - bytesPerPixel] : 0;
                current[x] = filter switch
                {
                    0 => value,
                    1 => unchecked((byte)(value + left)),
                    2 => unchecked((byte)(value + up)),
                    3 => unchecked((byte)(value + ((left + up) / 2))),
                    4 => unchecked((byte)(value + Paeth(left, up, upLeft))),
                    _ => throw new InvalidDataException($"PNG filter type {filter} is not supported.")
                };
            }

            var unityY = height - 1 - pngY;
            for (var x = 0; x < width; x++)
            {
                var rowOffset = x * bytesPerPixel;
                var rgbaOffset = ((unityY * width) + x) * 4;
                rgba[rgbaOffset] = current[rowOffset];
                rgba[rgbaOffset + 1] = current[rowOffset + 1];
                rgba[rgbaOffset + 2] = current[rowOffset + 2];
                rgba[rgbaOffset + 3] = colorType == 6 ? current[rowOffset + 3] : (byte)255;
            }

            var swap = previous;
            previous = current;
            current = swap;
            Array.Clear(current, 0, current.Length);
        }

        return new DecodedPng(width, height, rgba);
    }

    private static uint ReadUInt32BigEndian(byte[] bytes, int offset)
    {
        return ((uint)bytes[offset] << 24) |
            ((uint)bytes[offset + 1] << 16) |
            ((uint)bytes[offset + 2] << 8) |
            bytes[offset + 3];
    }

    private static int Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var distanceLeft = Math.Abs(estimate - left);
        var distanceUp = Math.Abs(estimate - up);
        var distanceUpLeft = Math.Abs(estimate - upLeft);
        if (distanceLeft <= distanceUp && distanceLeft <= distanceUpLeft)
        {
            return left;
        }

        return distanceUp <= distanceUpLeft ? up : upLeft;
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
                // Keep probing.
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

    private sealed class DecodedPng
    {
        public DecodedPng(int width, int height, byte[] rgba)
        {
            Width = width;
            Height = height;
            Rgba = rgba;
        }

        public int Width { get; }

        public int Height { get; }

        public byte[] Rgba { get; }
    }
}
