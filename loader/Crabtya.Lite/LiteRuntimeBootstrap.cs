using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace Crabtya.Lite;

public static class LiteRuntimeBootstrap
{
    private static bool _registered;
    private static bool _created;

    public static void EnsureCreated()
    {
        if (!_registered)
        {
            ClassInjector.RegisterTypeInIl2Cpp<LiteRuntimeBehaviour>();
            _registered = true;
        }

        if (_created)
        {
            return;
        }

        var gameObject = new GameObject("Crabtya.Lite.Runtime");
        UnityEngine.Object.DontDestroyOnLoad(gameObject);
        gameObject.hideFlags = HideFlags.HideAndDontSave;
        gameObject.AddComponent<LiteRuntimeBehaviour>();
        _created = true;

        Plugin.Instance?.Log.LogInfo("Crabtya Lite runtime bootstrap created.");
    }
}

public sealed class LiteRuntimeBehaviour : MonoBehaviour
{
    public LiteRuntimeBehaviour(IntPtr ptr)
        : base(ptr)
    {
    }

    public void Update()
    {
        LiteNativeSettingsApplicator.Update();
        LiteCameraApplicator.Update();
    }

    public void LateUpdate()
    {
        LiteCameraApplicator.LateUpdate();
    }
}
