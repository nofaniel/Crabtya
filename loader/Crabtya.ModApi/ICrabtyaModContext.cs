namespace Crabtya.ModApi;

public interface ICrabtyaModContext
{
    string ModId { get; }

    string ModName { get; }

    string ModVersion { get; }

    string ModDirectory { get; }

    ICrabtyaLogger Logger { get; }

    ICrabtyaSettingsRegistry Settings { get; }

    ICrabtyaAssetBundleRegistry AssetBundles { get; }
}
