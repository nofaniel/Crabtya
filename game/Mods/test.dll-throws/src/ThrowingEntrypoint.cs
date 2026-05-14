using Crabtya.ModApi;

namespace Crabtya.TestThrowingDll;

public sealed class ThrowingEntrypoint : ICrabtyaMod
{
    public string Id => "test.dll-throws";
    public string Name => "Test Throwing DLL";
    public string Version => "1.0.0";

    public void OnLoad(ICrabtyaModContext context)
    {
        context.Logger.Info("ThrowingEntrypoint: about to throw intentionally for isolation validation.");
        throw new InvalidOperationException("Intentional test exception from test.dll-throws.");
    }
}
