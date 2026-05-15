using Crabtya.ModApi;
using UnityEngine;

namespace Crabtya.BundleTest;

/// <summary>
/// End-to-end AssetBundle proof of concept for Crabtya v1.
///
/// This entrypoint is called after Crabtya has loaded the bundle declared in eicmod.json.
/// It uses ICrabtyaModContext.AssetBundles to list and extract assets. All results are
/// written to LogOutput.log.
///
/// Prerequisites:
///   - Build a Unity 6000.2.15f1 StandaloneWindows64 AssetBundle.
///   - Name the bundle "test-assets" and place it at:
///       game/Mods/crabtya.bundle-test/bundles/test-assets
///   - Include at least one Texture2D asset named "test_texture" (64x64 or larger).
///   - Enable this mod in the Mods menu and restart the game.
///
/// Expected log output on success:
///   [Info  :crabtya.bundle-test] BundleTest: bundle retrieved (key=crabtya.bundle-test:bundles/test-assets).
///   [Info  :crabtya.bundle-test] BundleTest: asset count=N. Names: assets/test_texture.png, ...
///   [Info  :crabtya.bundle-test] BundleTest: PASS - Texture2D 'test_texture' loaded. Size=64x64.
/// </summary>
public sealed class BundleTestEntrypoint : ICrabtyaMod
{
    private const string BundlePath = "bundles/test-assets";
    private const string TestAssetName = "test_texture";

    public string Id => "crabtya.bundle-test";
    public string Name => "Crabtya Bundle Proof";
    public string Version => "1.0.0";

    public void OnLoad(ICrabtyaModContext context)
    {
        context.Logger.Info("BundleTest: OnLoad reached.");

        if (!context.AssetBundles.TryGetBundle(BundlePath, out _))
        {
            context.Logger.Warning(
                "BundleTest: bundle not available for '" + BundlePath + "'. " +
                "Ensure the file exists at that path inside the mod folder and was " +
                "built with Unity 6000.2.15f1 for StandaloneWindows64. " +
                "Check the AssetBundleApplicator warning in the log for details.");
            return;
        }

        context.Logger.Info(
            "BundleTest: bundle retrieved (path='" + BundlePath + "').");

        var names = context.AssetBundles.GetAssetNames(BundlePath);
        if (names.Count == 0)
        {
            context.Logger.Warning("BundleTest: bundle loaded but contains no assets.");
        }
        else
        {
            context.Logger.Info(
                "BundleTest: asset count=" + names.Count + ". " +
                "Names: " + string.Join(", ", names.Take(10)));
        }

        var allTextures = context.AssetBundles.LoadAllAssets<Texture2D>(BundlePath);
        var texture = allTextures.FirstOrDefault(v => v != null && v.name == TestAssetName);

        if (texture != null)
        {
            context.Logger.Info(
                "BundleTest: PASS - Texture2D '" + TestAssetName + "' loaded successfully. " +
                "Size=" + texture.width + "x" + texture.height + ".");
        }
        else
        {
            context.Logger.Warning(
                "BundleTest: Texture2D '" + TestAssetName + "' not found. " +
                "Total textures in bundle=" + allTextures.Count + ". " +
                "Asset .name is the Unity object name (not the bundle path). " +
                "Check the asset names logged above.");
        }

        var directTexture = context.AssetBundles.LoadAsset<Texture2D>(
            BundlePath,
            "assets/test_texture.png");
        if (directTexture != null)
        {
            context.Logger.Info(
                "BundleTest: PASS - context.AssetBundles.LoadAsset<Texture2D> loaded 'assets/test_texture.png'. " +
                "Size=" + directTexture.width + "x" + directTexture.height + ".");
        }
        else
        {
            context.Logger.Warning(
                "BundleTest: context.AssetBundles.LoadAsset<Texture2D> returned null for 'assets/test_texture.png'.");
        }
    }
}
