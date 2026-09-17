using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using UnityEngine;
using Object = Il2CppPipistrello.Object;

namespace PipistrelloArchipelago.Patches;

[HarmonyPatch]
internal static class MapChangePatches
{
    private static readonly HashSet<string> ObjectsToRemove =
    [
        "city/ren355/lor2455", // Code that reminds the player to collect both Mega-Batteries before going to North Plaza.
        "city/yug108/lor2496", // Code that reminds player to go to the other dungeon once Faria Mega-Battery is obtained.
        "dungeon1/ren29878/lor570", // Code that teleports player to the Safe House
        "dungeon2/lor1089/lor1282", // Code that teleports player to the Safe House
        "dungeon3/lor2/lor521", // Code that teleports player to the Safe House
        "dungeon3/lor2/lor520", // Trigger area that reminds the player if they're leaving without the Mega-Battery
        "dungeon4/lor155/lor1361", // Code that teleports player to the Safe House
    ];

    /// <summary>
    /// Removes certain objects.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Director), nameof(Director.InstantiateFromMap))]
    private static bool Director_InstantiateFromMap_Prefix(Mapvania.Object mapObj, ref Object __result)
    {
        if (!ObjectsToRemove.Contains(mapObj.globalObjectId.AsString))
        {
            return true;
        }

        __result = null;
        return false;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.LoadProject))]
    private static void Director_LoadProject_Postfix()
    {
        // Block off Old Rattalia Town.
        var map = Global.Director.currentProject.maps.ToArray().FirstOrDefault(m => m.id == "city")!;
        map.roomsById["ren223"].objects.Add(
            new Mapvania.Object
            {
                objectDefId = "lor15",
                objectDefName = "barrier",
                globalObjectId = new Game.GlobalObjectId
                {
                    mapId = "city",
                    roomId = "ren223",
                    objectId = "archBarrier1",
                },
                position = new Vector2(0, 22 * 16),
                width = 16,
                height = 7 * 16,
                properties = JsonValue.Parse("""{"activationFlag": true}"""),
                usesFlags = true,
            });
        map.roomsById["ren4152"].objects.Add(
            new Mapvania.Object
            {
                objectDefId = "lor15",
                objectDefName = "barrier",
                globalObjectId = new Game.GlobalObjectId
                {
                    mapId = "city",
                    roomId = "ren4152",
                    objectId = "archBarrier2",
                },
                position = new Vector2(0, 2 * 16),
                width = 16,
                height = 7 * 16,
                properties = JsonValue.Parse("""{"activationFlag": true}"""),
                usesFlags = true,
            });
        map.roomsById["ren4064"].objects.Add(
            new Mapvania.Object
            {
                objectDefId = "lor15",
                objectDefName = "barrier",
                globalObjectId = new Game.GlobalObjectId
                {
                    mapId = "city",
                    roomId = "ren4064",
                    objectId = "archBarrier3",
                },
                position = new Vector2(0, 4 * 16),
                width = 16,
                height = 2 * 16,
                properties = JsonValue.Parse("""{"activationFlag": true}"""),
                usesFlags = true,
            });

        // Block off Cancelled Subway Station
        map.roomsById["lor1128"].objects.Add(
            new Mapvania.Object
            {
                objectDefId = "lor15",
                objectDefName = "barrier",
                globalObjectId = new Game.GlobalObjectId
                {
                    mapId = "city",
                    roomId = "lor1128",
                    objectId = "archBarrier4",
                },
                position = new Vector2(14 * 16, 8 * 16),
                width = 2 * 16,
                height = 16,
                properties = JsonValue.Parse("""{"activationFlag": true}"""),
                usesFlags = true,
            });

        // Block off water access to Fadalins Neighborhood
        map.roomsById["lor1097"].objects.Add(
            new Mapvania.Object
            {
                objectDefId = "lor15",
                objectDefName = "barrier",
                globalObjectId = new Game.GlobalObjectId
                {
                    mapId = "city",
                    roomId = "lor1097",
                    objectId = "archBarrier5",
                },
                position = new Vector2(4 * 16, 0),
                width = 25 * 16,
                height = 16,
                properties = JsonValue.Parse("""{"activationFlag": true}"""),
                usesFlags = true,
            });

        // Add barrier for defeating the Faria boss and getting the Faria Mega-Battery.
        map = Global.Director.currentProject.maps.ToArray().FirstOrDefault(m => m.id == "city_underground")!;
        map.roomsById["ren984"].objects.Add(
            new Mapvania.Object
            {
                objectDefId = "lor15",
                objectDefName = "barrier",
                globalObjectId = new Game.GlobalObjectId
                {
                    mapId = "city_underground",
                    roomId = "ren984",
                    objectId = "archFariaBarrier",
                },
                position = new Vector2(17 * 16, 16),
                width = 2 * 16,
                height = 16,
                properties = JsonValue.Parse("""{"activationFlag": "!g:bossDefeated2 || !g:megaBattery2"}"""),
                usesFlags = true,
            });

        // Add sign explaining the new barrier requirements.
        const string archBarrierSignCode = """
                                           var flagFariaBoss = \"false\"
                                           if (flag(\"g:bossDefeated2\").isOn()) { flagFariaBoss = \"true\" }
                                           var flagMegaBattery2 = \"false\"
                                           if (flag(\"g:megaBattery2\").isOn()) { flagMegaBattery2 = \"true\" }
                                           this.say(\"Additional barrier requirements:\n\"
                                               + \" - Faria boss defeated: \" + flagFariaBoss + \"\n\"
                                               + \" - Faria Mega-Battery obtained: \" +  flagMegaBattery2)
                                           """;
        map.roomsById["ren984"].objects.Add(
            new Mapvania.Object
            {
                objectDefId = "hen93",
                objectDefName = "sign",
                globalObjectId = new Game.GlobalObjectId
                {
                    mapId = "city_underground",
                    roomId = "ren984",
                    objectId = "archBarrierSign",
                },
                position = new Vector2(20 * 16, 4 * 16),
                width = 16,
                height = 16,
                properties = JsonValue.Parse($$"""{"code": "{{archBarrierSignCode}}", "hideShadow": true}"""),
                usesFlags = true,
            });

        // Prevent Mega-Battery from granting access to Faria dungeon entrance.
        var obj = Utils.GetMapvaniaObject("city/yug108/yug133")!;
        var field = obj.properties.Fields.ToArray().First(f => f.name == "activationFlag");
        obj.properties.Fields.Remove(field);
        obj.properties.SetField(
            "activationFlag",
            field.value.StringValue.Replace(Game.FLAG_MEGABATTERY2, $"{Game.GLOBAL_FLAG_PREFIX}bossDefeated2"));

        // Remove slime NPC from Faria dungeon entrance after boss is defeated.
        obj = Utils.GetMapvaniaObject("city/yug108/yug3097")!;
        field = obj.properties.Fields.ToArray().First(f => f.name == "presenceFlag")!;
        obj.properties.Fields.Remove(field);
        obj.properties.SetField(
            "presenceFlag",
            field.value.StringValue.Replace(Game.FLAG_MEGABATTERY2, $"{Game.GLOBAL_FLAG_PREFIX}bossDefeated2"));

        // Modify dungeon map pin to the arena map pin, which only shows if the boss is not defeated.
        obj = Utils.GetMapvaniaObject("city/yug108/lor515")!;
        field = obj.properties.Fields.ToArray().First(f => f.name == "pinId")!;
        obj.properties.Fields.Remove(field);
        obj.properties.SetField("pinId", "arena");
        obj.properties.SetField("activationFlag", $"!{Game.GLOBAL_FLAG_PREFIX}bossDefeated2");

        // Add a dungeon map pin with a check mark if the boss is defeated.
        map = Global.Director.currentProject.maps.ToArray().FirstOrDefault(m => m.id == "city")!;
        map.roomsById["yug108"].objects.Add(
            new Mapvania.Object
            {
                objectDefId = "lor251",
                objectDefName = "mapPin",
                globalObjectId = new Game.GlobalObjectId
                {
                    mapId = "city",
                    roomId = "yug108",
                    objectId = "archDungeon2MapPin",
                },
                position = new Vector2(obj.position.x, obj.position.y),
                width = 16,
                height = 16,
                properties = JsonValue.Parse(
                    $$"""{"activationFlag": "{{$"{Game.GLOBAL_FLAG_PREFIX}bossDefeated2"}}", "pinId": "{{Constants.BossKillSmallSpriteName}}"}"""),
                usesFlags = true,
            });

        // Modify Faria Mega-Battery holder to control a separate flag.
        obj = Utils.GetMapvaniaObject("dungeon2/lor1089/lor1264")!;
        field = obj.properties.Fields.ToArray().First(f => f.name == "controlsFlag");
        obj.properties.Fields.Remove(field);
        obj.properties.SetField("controlsFlag", $"{Game.FLAG_MEGABATTERY2}{Constants.FlagMegaBatterySuffix}");

        // Modify trigger area that reminds the player if they're leaving without the Mega-Battery.
        obj = Utils.GetMapvaniaObject("dungeon2/lor1089/lor1265")!;
        field = obj.properties.Fields.ToArray().First(f => f.name == "activationFlag");
        obj.properties.Fields.Remove(field);
        obj.properties.SetField(
            "activationFlag",
            field.value.StringValue.Replace(
                Game.FLAG_MEGABATTERY2, $"{Game.FLAG_MEGABATTERY2}{Constants.FlagMegaBatterySuffix}"));
    }
}
