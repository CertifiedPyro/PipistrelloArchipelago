using Il2CppPipistrello;
using System.Text.RegularExpressions;

namespace PipistrelloArchipelago;

static class Utils
{
    private static readonly Regex PetalFlagRegex = new($@"^{Regex.Escape(Game.FLAG_PETALCONTAINER_PREFIX)}(?<id>.*):acquired", RegexOptions.Compiled);

    public static string GetPetalPhysicalFlag(string id)
    {
        return $"{Game.FLAG_PETALCONTAINER_PREFIX}{id}:physicalAcquired";
    }

    public static string GetPetalVirtualFlag(string id)
    {
        return $"{Game.FLAG_PETALCONTAINER_PREFIX}{id}:virtualAcquired";
    }

    public static bool GetPetalIdFromFlag(string flag, out string id)
    {
        if (PetalFlagRegex.Match(flag) is { Success: true } match)
        {
            id = match.Groups["id"].Value;
            return true;
        }

        id = null;
        return false;
    }
}
