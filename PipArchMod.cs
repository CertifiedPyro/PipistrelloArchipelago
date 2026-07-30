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
        ReadObjectIdMapping();
        ExportArchipelagoSprites();

        // Initialize MelonLoader settings.
        ModSettings.Category = MelonPreferences.CreateCategory("Archipelago");
        ModSettings.Host = ModSettings.Category.CreateEntry<string>("Host", "archipelago.gg");
        ModSettings.Port = ModSettings.Category.CreateEntry<int>("Port", 0);
        ModSettings.SlotName = ModSettings.Category.CreateEntry<string>("Slot Name", string.Empty);
        ModSettings.Password = ModSettings.Category.CreateEntry<string>("Password", null);
    }

    private static void ReadObjectIdMapping()
    {
        try
        {
            var file = "object_id_mapping.json";
            var data = LoadBytesFromResource($"PipistrelloArchipelago.{file}");
            if (data != null)
            {
                GlobalState.GlobalObjectIdToLocationName = JsonSerializer.Deserialize<Dictionary<string, string>>(data);
                GlobalState.LocationNameToGlobalObjectId = GlobalState.GlobalObjectIdToLocationName.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
            }
            else
            {
                Melon<PipArchMod>.Logger.Error($"Could not find embedded resource 'PipistrelloArchipelago.{file}'");
            }
        }
        catch (Exception e)
        {
            Melon<PipArchMod>.Logger.Error($"Failed to read object id mapping: {e.Message}");
        }
    }

    private static void ExportArchipelagoSprites()
    {
        try
        {
            var filesToPaths = new List<Tuple<string, string>>()
            {
                new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps", "Sprites"), $"{Constants.ArchMediumSpriteName}.png"),
                new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps", "Sprites"), $"{Constants.ArchMoneyBagSpriteName}.png"),
                new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps", "Sprites", "ui", "mapPins"), $"{Constants.ArchSmallSpriteName}.png"),
                new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps", "Sprites", "ui", "mapPins"), $"{Constants.ArchMoneyBagSpriteName}.png"),
                new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps", "Sprites", "ui", "icons"), $"{Constants.ArchSmallSpriteName}.png"),
            };
            foreach (var (path, file) in filesToPaths)
            {
                var fullPath = Path.Combine(path, file);
                var data = LoadBytesFromResource($"PipistrelloArchipelago.Images.{file}");
                if (data != null)
                {
                    File.WriteAllBytes(fullPath, data);
                    Melon<PipArchMod>.Logger.Msg($"Archipelago sprite deployed to: {fullPath}");
                }
                else
                {
                    Melon<PipArchMod>.Logger.Error($"Could not find embedded resource 'PipistrelloArchipelago.{file}'");
                }
            }
        }
        catch (Exception e)
        {
            Melon<PipArchMod>.Logger.Error($"Failed to export sprite: {e.Message}");
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
