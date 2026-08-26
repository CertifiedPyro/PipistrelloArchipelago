using System.Reflection;
using System.Text.Json;
using MelonLoader;
using PipistrelloArchipelago;

[assembly: MelonInfo(typeof(PipArchMod), "PipistrelloArchipelago", "0.2.0", "CertifiedPyro")]
[assembly: MelonGame("Pocket Trap", "Pipistrello")]

namespace PipistrelloArchipelago;

public class PipArchMod : MelonMod
{
    public override void OnInitializeMelon()
    {
        // Initialize MelonLoader settings.
        ModSettings.Category = MelonPreferences.CreateCategory("Archipelago");
        ModSettings.Host = ModSettings.Category.CreateEntry("Host", "archipelago.gg");
        ModSettings.Port = ModSettings.Category.CreateEntry("Port", 0);
        ModSettings.SlotName = ModSettings.Category.CreateEntry("Slot Name", "");
        ModSettings.Password = ModSettings.Category.CreateEntry(
            "Password", "", description: "WARNING: Password is not hidden");

        const string deathLinkDescription = """
                                            Once connected, toggles death link if it was originally enabled.
                                            Note: You cannot disable death link in race mode.
                                            """;
        ModSettings.DeathLink = ModSettings.Category.CreateEntry(
            "Death Link",
            false,
            description: deathLinkDescription,
            validator: new ModSettings.DeathLinkValidator());

        ModSettings.AllowItemReceiveMessages = ModSettings.Category.CreateEntry("Item Receive Messages", true);
        ModSettings.AllowChatMessages = ModSettings.Category.CreateEntry("Chat Messages", true);
        ModSettings.AllowServerMessages = ModSettings.Category.CreateEntry("Server Messages", true);

        ReadObjectIdMapping();
        ExportArchipelagoSprites();
    }

    private static void ReadObjectIdMapping()
    {
        try
        {
            const string file = $"{nameof(PipistrelloArchipelago)}.object_id_mapping.json";
            var data = LoadBytesFromResource(file) ?? throw new Exception($"Missing embedded resource: {file}");
            Global.GlobalObjectIdToLocationName = JsonSerializer.Deserialize<Dictionary<string, string>>(data);
            Global.LocationNameToGlobalObjectId = Global.GlobalObjectIdToLocationName
                .ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
        }
        catch (Exception e)
        {
            Melon<PipArchMod>.Logger.BigError($"Failed to read object id mapping: {e.Message}");
        }
    }

    private static void ExportArchipelagoSprites()
    {
        try
        {
            var spritesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps", "Sprites");
            var mapPinsFolder = Path.Combine(spritesFolder, "ui", "mapPins");
            var filesToPaths = new List<Tuple<string, string>>
            {
                new(spritesFolder, $"{Constants.ArchMediumSpriteName}.png"),
                new(mapPinsFolder, $"{Constants.ArchSmallSpriteName}.png"),
                new(spritesFolder, $"{Constants.MoneyBagMediumSpriteName}.png"),
                new(mapPinsFolder, $"{Constants.MoneyBagSmallSpriteName}.png")
            };
            foreach (var (path, file) in filesToPaths)
            {
                var fullPath = Path.Combine(path, file);
                var data = LoadBytesFromResource($"PipistrelloArchipelago.Images.{file}")
                           ?? throw new Exception($"Could not find embedded resource 'PipistrelloArchipelago.{file}'");
                File.WriteAllBytes(fullPath, data);
                Melon<PipArchMod>.Logger.Msg($"Archipelago sprite deployed to: {fullPath}");
            }
        }
        catch (Exception e)
        {
            Melon<PipArchMod>.Logger.Error($"Failed to export Archipelago sprite: {e.Message}");
        }
    }

    private static byte[] LoadBytesFromResource(string path)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
        if (stream == null)
        {
            return null;
        }

        var buffer = new byte[stream.Length];
        var bytesRead = stream.Read(buffer, 0, buffer.Length);
        return bytesRead == buffer.Length ? buffer : null;
    }
}
