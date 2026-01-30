namespace PipistrelloArchipelago;

static class Constants
{
    public static string ArchItemObjectIdSuffix = "_architem";
    public static string ArchSpriteName = "archipelago";
    public static string ArchMapPinSpriteName = "archipelagoMapPin";
}

static class Utils
{
    public static bool IsArchItemId(string id)
    {
        return id != null && id.Contains(Constants.ArchItemObjectIdSuffix);
    }
}
