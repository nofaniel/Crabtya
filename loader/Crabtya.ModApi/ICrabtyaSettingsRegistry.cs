namespace Crabtya.ModApi;

public interface ICrabtyaSettingsRegistry
{
    void RegisterBool(string key, string label, bool defaultValue, string description = "");

    void RegisterInt(string key, string label, int defaultValue, int min, int max, int step = 1, string description = "");

    void RegisterFloat(string key, string label, float defaultValue, float min, float max, float step = 0.05f, string description = "");

    void RegisterOption(string key, string label, string defaultValue, IReadOnlyList<string> options, string description = "");

    bool TryGetBool(string key, out bool value);

    bool TryGetInt(string key, out int value);

    bool TryGetFloat(string key, out float value);

    bool TryGetOption(string key, out string value);

    void SetBool(string key, bool value);

    void SetInt(string key, int value);

    void SetFloat(string key, float value);

    void SetOption(string key, string value);
}
