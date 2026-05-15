namespace Crabtya.ModApi;

public interface ICrabtyaAssetBundleRegistry
{
    object? GetBundle(string relativePath);

    bool TryGetBundle(string relativePath, out object? bundle);

    IReadOnlyList<string> GetAssetNames(string relativePath);

    TAsset? LoadAsset<TAsset>(string relativePath, string assetPath) where TAsset : class;

    IReadOnlyList<TAsset> LoadAllAssets<TAsset>(string relativePath) where TAsset : class;
}
