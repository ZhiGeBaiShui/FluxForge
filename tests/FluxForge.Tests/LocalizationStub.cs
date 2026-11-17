// Selection-rule tests never render text; keep the native localization boundary out of this assembly.
public static class ModHelper
{
    public static string GetLocalized(string modId, string key)
    {
        return key;
    }
}
