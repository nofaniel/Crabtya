using System.Text.Json.Serialization;

namespace EIC.ModLoader;

public sealed class ModManifest
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("targetGame")]
    public string? TargetGame { get; set; }

    [JsonPropertyName("targetUnity")]
    public string? TargetUnity { get; set; }

    [JsonPropertyName("loaderVersion")]
    public string? LoaderVersion { get; set; }

    [JsonPropertyName("dependencies")]
    public List<string>? Dependencies { get; set; }

    [JsonPropertyName("conflicts")]
    public List<string>? Conflicts { get; set; }

    [JsonPropertyName("content")]
    public ModContentDeclaration? Content { get; set; }

    [JsonPropertyName("assemblies")]
    public List<string>? Assemblies { get; set; }

    [JsonPropertyName("entrypoints")]
    public List<string>? EntryPoints { get; set; }

    [JsonPropertyName("defaultEnabled")]
    public bool? DefaultEnabled { get; set; }
}

public sealed class ModContentDeclaration
{
    [JsonPropertyName("catalogs")]
    public List<string>? Catalogs { get; set; }

    [JsonPropertyName("assetBundles")]
    public List<string>? AssetBundles { get; set; }

    [JsonPropertyName("definitions")]
    public List<string>? Definitions { get; set; }
}
