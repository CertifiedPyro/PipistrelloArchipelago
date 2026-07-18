namespace PipistrelloArchipelago;

static class Constants
{
    public static string ArchItemObjectIdSuffix = "_architem";
    public static string ArchSpriteName = "arch_medium";
    public static string ArchMapPinSpriteName = "arch_small";
}

static class Utils
{
    public static bool IsArchItemId(string id)
    {
        return id != null && id.Contains(Constants.ArchItemObjectIdSuffix);
    }
}
