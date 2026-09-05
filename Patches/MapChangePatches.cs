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
        "dungeon1/ren29878/lor570", // Code that teleports player to the Safe House
        "dungeon2/lor1089/lor1282", // Code that teleports player to the Safe House
        "dungeon2/lor1089/lor1265", // Trigger area that reminds the player if they're leaving without the Mega-Battery
        "dungeon3/lor2/lor521", // Code that teleports player to the Safe House
        "dungeon3/lor2/lor520", // Trigger area that reminds the player if they're leaving without the Mega-Battery
        "dungeon4/lor155/lor1361" // Code that teleports player to the Safe House
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
        var room = map.rooms.ToArray().FirstOrDefault(r => r.id == "ren223")!;
        var objects = room.objects;
        if (objects.ToArray().FirstOrDefault(o => o.globalObjectId.objectId == "archBarrier1") == null)
        {
            room.objects.Add(
                new Mapvania.Object
                {
                    objectDefId = "lor15",
                    objectDefName = "barrier",
                    globalObjectId = new Game.GlobalObjectId
                    {
                        mapId = "city",
                        roomId = "ren223",
                        objectId = "archBarrier1"
                    },
                    position = new Vector2(0, 22 * 16),
                    width = 16,
                    height = 7 * 16,
                    properties = JsonValue.Parse("""{"activationFlag": true}"""),
                    usesFlags = true
                });
        }

        room = map.rooms.ToArray().FirstOrDefault(r => r.id == "ren4152")!;
        objects = room.objects;
        if (objects.ToArray().FirstOrDefault(o => o.globalObjectId.objectId == "archBarrier2") == null)
        {
            room.objects.Add(
                new Mapvania.Object
                {
                    objectDefId = "lor15",
                    objectDefName = "barrier",
                    globalObjectId = new Game.GlobalObjectId
                    {
                        mapId = "city",
                        roomId = "ren4152",
                        objectId = "archBarrier2"
                    },
                    position = new Vector2(0, 2 * 16),
                    width = 16,
                    height = 7 * 16,
                    properties = JsonValue.Parse("""{"activationFlag": true}"""),
                    usesFlags = true
                });
        }

        room = map.rooms.ToArray().FirstOrDefault(r => r.id == "ren4064")!;
        objects = room.objects;
        if (objects.ToArray().FirstOrDefault(o => o.globalObjectId.objectId == "archBarrier3") == null)
        {
            room.objects.Add(
                new Mapvania.Object
                {
                    objectDefId = "lor15",
                    objectDefName = "barrier",
                    globalObjectId = new Game.GlobalObjectId
                    {
                        mapId = "city",
                        roomId = "ren4064",
                        objectId = "archBarrier3"
                    },
                    position = new Vector2(0, 4 * 16),
                    width = 16,
                    height = 2 * 16,
                    properties = JsonValue.Parse("""{"activationFlag": true}"""),
                    usesFlags = true
                });
        }

        // Block off Cancelled Subway Station
        room = map.rooms.ToArray().FirstOrDefault(r => r.id == "lor1128")!;
        objects = room.objects;
        if (objects.ToArray().FirstOrDefault(o => o.globalObjectId.objectId == "archBarrier4") == null)
        {
            room.objects.Add(
                new Mapvania.Object
                {
                    objectDefId = "lor15",
                    objectDefName = "barrier",
                    globalObjectId = new Game.GlobalObjectId
                    {
                        mapId = "city",
                        roomId = "lor1128",
                        objectId = "archBarrier4"
                    },
                    position = new Vector2(14 * 16, 8 * 16),
                    width = 2 * 16,
                    height = 16,
                    properties = JsonValue.Parse("""{"activationFlag": true}"""),
                    usesFlags = true
                });
        }

        // Block off water access to Fadalins Neighborhood
        room = map.rooms.ToArray().FirstOrDefault(r => r.id == "lor1097")!;
        objects = room.objects;
        if (objects.ToArray().FirstOrDefault(o => o.globalObjectId.objectId == "archBarrier5") == null)
        {
            room.objects.Add(
                new Mapvania.Object
                {
                    objectDefId = "lor15",
                    objectDefName = "barrier",
                    globalObjectId = new Game.GlobalObjectId
                    {
                        mapId = "city",
                        roomId = "lor1128",
                        objectId = "archBarrier5"
                    },
                    position = new Vector2(4 * 16, 0),
                    width = 25 * 16,
                    height = 16,
                    properties = JsonValue.Parse("""{"activationFlag": true}"""),
                    usesFlags = true
                });
        }

        // Add barrier for defeating the Faria boss and getting the Faria Mega-Battery.
        map = Global.Director.currentProject.maps.ToArray().FirstOrDefault(m => m.id == "city_underground")!;
        room = map.rooms.ToArray().FirstOrDefault(r => r.id == "ren984")!;
        objects = room.objects;
        if (objects.ToArray().FirstOrDefault(o => o.globalObjectId.objectId == "archFariaBarrier") == null)
        {
            room.objects.Add(
                new Mapvania.Object
                {
                    objectDefId = "lor15",
                    objectDefName = "barrier",
                    globalObjectId = new Game.GlobalObjectId
                    {
                        mapId = "city_underground",
                        roomId = "ren984",
                        objectId = "archFariaBarrier"
                    },
                    position = new Vector2(17 * 16, 16),
                    width = 2 * 16,
                    height = 16,
                    properties = JsonValue.Parse("""{"activationFlag": "!g:bossDefeated2 || !g:megaBattery2"}"""),
                    usesFlags = true
                });
        }

        // Add sign explaining the new barrier requirements.
        if (objects.ToArray().FirstOrDefault(o => o.globalObjectId.objectId == "archBarrierSign") == null)
        {
            const string code = """
                                var flagFariaBoss = \"false\"
                                if (flag(\"g:bossDefeated2\").isOn()) { flagFariaBoss = \"true\" }
                                var flagMegaBattery2 = \"false\"
                                if (flag(\"g:megaBattery2\").isOn()) { flagMegaBattery2 = \"true\" }
                                this.say(\"Additional barrier requirements:\n\"
                                    + \" - Faria boss defeated: \" + flagFariaBoss + \"\n\"
                                    + \" - Faria Mega-Battery obtained: \" +  flagMegaBattery2)
                                """;
            room.objects.Add(
                new Mapvania.Object
                {
                    objectDefId = "hen93",
                    objectDefName = "sign",
                    globalObjectId = new Game.GlobalObjectId
                    {
                        mapId = "city_underground",
                        roomId = "ren984",
                        objectId = "archBarrierSign"
                    },
                    position = new Vector2(20 * 16, 4 * 16),
                    width = 16,
                    height = 16,
                    properties = JsonValue.Parse($$"""{"code": "{{code}}", "hideShadow": true}"""),
                    usesFlags = true
                });
        }
    }
}
