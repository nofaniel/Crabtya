using Crabtya.ModApi;
using EIC.ModLoader;
using UnityEngine;

namespace Crabtya.BundleTest;

/// <summary>
/// End-to-end AssetBundle proof of concept for Crabtya v1.
///
/// This entrypoint is called after AssetBundleApplicator has loaded the bundle declared in
/// eicmod.json. It retrieves the bundle, lists all contained assets, and attempts to load
/// a Texture2D named "test_texture". All results are written to LogOutput.log.
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
    private const string ModId = "crabtya.bundle-test";
    private const string BundlePath = "bundles/test-assets";
    private const string TestAssetName = "test_texture";

    public string Id => ModId;
    public string Name => "Crabtya Bundle Proof";
    public string Version => "1.0.0";

    public void OnLoad(ICrabtyaModContext context)
    {
        context.Logger.Info("BundleTest: OnLoad reached.");

        // Retrieve the bundle that AssetBundleApplicator loaded at startup.
        var bundle = AssetBundleApplicator.GetBundle(ModId, BundlePath);

        if (bundle == null)
        {
            context.Logger.Warning(
                "BundleTest: bundle not available for '" + BundlePath + "'. " +
                "Ensure the file exists at that path inside the mod folder and was " +
                "built with Unity 6000.2.15f1 for StandaloneWindows64. " +
                "Check the AssetBundleApplicator warning in the log for details.");
            return;
        }

        context.Logger.Info(
            "BundleTest: bundle retrieved (key=" + ModId + ":" + BundlePath + ").");

        // List all assets in the bundle.
        try
        {
            var names = bundle.GetAllAssetNames();
            if (names == null || names.Length == 0)
            {
                context.Logger.Warning("BundleTest: bundle loaded but contains no assets.");
            }
            else
            {
                context.Logger.Info(
                    "BundleTest: asset count=" + names.Length + ". " +
                    "Names: " + string.Join(", ", names.Take(10)));
            }
        }
        catch (Exception ex)
        {
            context.Logger.Warning("BundleTest: GetAllAssetNames threw: " + ex.Message);
        }

        // Load all assets via non-generic LoadAllAssets() then cast with TryCast<Texture2D>().
        // Both LoadAsset<T>(string) and LoadAllAssets<T>() resolve the type T as a string
        // internally (ReadOnlySpan<char>), throwing GetPinnableReference on this IL2CPP build.
        // The non-generic overload avoids that path. TryCast<T>() uses IL2CPP class handles,
        // not string lookups.
        try
        {
            var allAssets = bundle.LoadAllAssets();
            Texture2D texture = null;
            if (allAssets != null)
            {
                foreach (var asset in allAssets)
                {
                    if (asset == null) continue;
                    var tex = asset.TryCast<Texture2D>();
                    if (tex != null && tex.name == TestAssetName)
                    {
                        texture = tex;
                        break;
                    }
                }
            }

            if (texture != null)
            {
                context.Logger.Info(
                    "BundleTest: PASS - Texture2D '" + TestAssetName + "' loaded successfully. " +
                    "Size=" + texture.width + "x" + texture.height + ".");
            }
            else
            {
                var count = allAssets?.Length ?? 0;
                context.Logger.Warning(
                    "BundleTest: Texture2D '" + TestAssetName + "' not found. " +
                    "Total assets in bundle=" + count + ". " +
                    "Asset .name is the Unity object name (not the bundle path). " +
                    "Check the asset names logged above.");
            }
        }
        catch (Exception ex)
        {
            context.Logger.Warning("BundleTest: LoadAllAssets threw: " + ex.Message);
        }
    }
}
