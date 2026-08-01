using MelonLoader;
using System.Reflection;
using System.Text.Json;

[assembly: MelonInfo(typeof(PipistrelloArchipelago.PipArchMod), "PipistrelloArchipelago", "0.1.0", "CertifiedPyro", null)]
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
        ModSettings.SlotName = ModSettings.Category.CreateEntry("Slot Name", string.Empty);
        ModSettings.Password = ModSettings.Category.CreateEntry("Password", string.Empty);

        ReadObjectIdMapping();
        ExportArchipelagoSprites();
    }

    private static void ReadObjectIdMapping()
    {
        try
        {
            var file = "object_id_mapping.json";
            var data = LoadBytesFromResource($"PipistrelloArchipelago.{file}") 
                ?? throw new Exception($"Could not find embedded resource 'PipistrelloArchipelago.{file}'");
            Global.GlobalObjectIdToLocationName = JsonSerializer.Deserialize<Dictionary<string, string>>(data);
            Global.LocationNameToGlobalObjectId = Global.GlobalObjectIdToLocationName.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
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
            var filesToPaths = new List<Tuple<string, string>>()
            {
                new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps", "Sprites"), $"{Constants.ArchMediumSpriteName}.png"),
                new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps", "Sprites", "ui", "mapPins"), $"{Constants.ArchSmallSpriteName}.png"),
                new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps", "Sprites"), $"{Constants.MoneyBagMediumSpriteName}.png"),
                new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps", "Sprites", "ui", "mapPins"), $"{Constants.MoneyBagSmallSpriteName}.png"),
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
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
        if (stream == null)
        {
            return null;
        }

        var buffer = new byte[stream.Length];
        stream.Read(buffer, 0, buffer.Length);
        return buffer;
    }
}
