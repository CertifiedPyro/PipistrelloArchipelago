using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using UnityEngine;

namespace PipistrelloArchipelago.Patches;

[HarmonyPatch]
internal static class MapChangePatches
{
    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.LoadProject))]
    private static void Director_LoadProject_Postfix()
    {
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

        // Remove door to skyscraper mini-dungeon.
        room = map.rooms.ToArray().FirstOrDefault(r => r.id == "yug2741")!;
        objects = room.objects;
        var objectToRemove = objects.ToArray().FirstOrDefault(o => o.globalObjectId.objectId == "yug2747");
        if (objectToRemove != null)
        {
            objects.Remove(objectToRemove);
        }

        // Remove slime NPC in front of Faria dungeon.
        room = map.rooms.ToArray().FirstOrDefault(r => r.id == "yug108")!;
        objects = room.objects;
        objectToRemove = objects.ToArray().FirstOrDefault(o => o.globalObjectId.objectId == "yug3097");
        if (objectToRemove != null)
        {
            objects.Remove(objectToRemove);
        }
    }
}
