namespace Crabtya.ModApi;

public interface ICrabtyaMod
{
    string Id { get; }

    string Name { get; }

    string Version { get; }

    void OnLoad(ICrabtyaModContext context);
}
