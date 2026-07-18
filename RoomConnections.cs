using HarmonyLib;
using Il2Cpp;
using Il2CppPipistrello;
using MelonLoader;
using UnityEngine;

namespace PipistrelloArchipelago;

//[HarmonyPatch(typeof(Director), nameof(Director.LoadProject))]
static class RoomConnections
{
    static Director instance;

    const int TILE_W = 16;
    const int TILE_H = 16;
    const int W_NUM_TILES = 18;
    const int H_NUM_TILES = 10;
    const string CSV_SEP = ";";

    static readonly System.Reflection.MethodInfo gameFormatRevealedLocationMethod = typeof(Game).GetMethod("FormatRevealedLocation");
    static readonly Dictionary<string, Mapvania.Map> mapDict = [];

    public static void Postfix(Director __instance)
    {
        instance = __instance;

        mapDict.Clear();
        foreach (var map in __instance.currentProject.maps)
        {
            mapDict.Add(map.id, map);
        }

        var sortedRooms = __instance.currentProject.maps.ToArray()
            .SelectMany(map => map.rooms.ToArray())
            .Where(room => !ShouldSkipRoom(room))
            .Select(room => new RoomData(room))
            .OrderBy(r => r);

        WriteRoomCsv(__instance, sortedRooms);
        WriteConnectionsCsv(__instance, sortedRooms);
    }

    /// <summary>
    /// Write room information to a CSV file, for import into Google Sheets.
    /// </summary>
    static void WriteRoomCsv(Director __instance, IEnumerable<RoomData> roomDatas)
    {
        var csvLines = roomDatas
            .Select(r => new RoomCsvEntry(r).Csv)
            .ToList();
        csvLines.Insert(0, RoomCsvEntry.Header);

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dev_arch", "rooms.csv");
        WriteFileIfChanged(path, csvLines);
    }

    static void WriteConnectionsCsv(Director  __instance, IEnumerable<RoomData> roomDatas)
    {
        var connections = new List<ConnectionCsvEntry>();
        var expectedConnections = new List<ExpectedConnectionCsvEntry>();

        foreach (var roomData in roomDatas)
        {
            // Check bordering rooms.
            var roomConnections = new List<ConnectionCsvEntry>();
            var numConnections = 0;
            foreach (var borderingRoom in roomData.Room.borderingRooms)
            {
                if (!borderingRoom.hasAnyAccess)
                {
                    continue;
                }

                // Get bordering room's relation to original room.
                var otherData = new RoomData(borderingRoom.room);
                var mode = (roomData.Coords, otherData.Coords) switch
                {
                    var (r, o) when r.X > o.X + o.Width - 1 => "left",
                    var (r, o) when r.X + r.Width - 1 < o.X => "right",
                    var (r, o) when r.Y > o.Y + o.Height - 1 => "up",
                    var (r, o) when r.Y + r.Height - 1 < o.Y  => "down",
                };

                var connectionPosition = GetConnectionPosition(roomData, otherData, mode, borderingRoom.hasGroundAccess);
                roomConnections.Add(new ConnectionCsvEntry(roomData, otherData, mode, connectionPosition));
                numConnections += 1;
            }

            foreach (var mapObject in roomData.Room.objects)
            {
                // Check warp areas.
                if (mapObject.objectDefName == "warpArea")
                {
                    if (mapObject.properties.TryGetFieldBool("onlyArrive"))
                    {
                        continue;
                    }

                    var mode = mapObject.properties.TryGetFieldString("mode");
                    var destSplit = mapObject.properties.TryGetFieldString("destination").Split('/');

                    // Destination could be mapId/objectId, or just objectId if the warp area is within the same map.
                    // TODO: Director.DecodeWarpDestination()?
                    var globalObjectId = new Game.GlobalObjectId()
                    {
                        mapId = destSplit.Length == 2 ? destSplit[0] : roomData.Map.id,
                        objectId = destSplit.Length == 2 ? destSplit[1] : destSplit[0]
                    };
                    var destRoom = Mapvania.FindWarpDestination(__instance.currentProject, globalObjectId, roomData.Map, roomData.Room).Item2;
                    var otherData = new RoomData(destRoom);

                    var connectionPosition = mode switch
                    {
                        "up" => mapObject.position + new Vector2(0, 2 * TILE_H),
                        "down" => mapObject.position - new Vector2(0, 2 * TILE_H),
                    };
                    roomConnections.Add(new ConnectionCsvEntry(roomData, otherData, $"warp ({mode})", connectionPosition.ToVector3()));
                    numConnections += 1;
                }

                // Check manholes.
                // Only "city" map has "manhole" objects, and only "city_underground" map has "manholeLightCone" objects.
                if (mapObject.objectDefName == "manhole" || mapObject.objectDefName == "manholeLightCone")
                {
                    var otherMap = roomData.Map.id switch
                    {
                        "city" => mapDict["city_underground"],
                        "city_underground" => mapDict["city"]
                    };
                    var globalPosition = roomData.Room.position + mapObject.position;
                    var otherRoom = Mapvania.FindRoomAtGlobalPosition(otherMap, globalPosition);

                    var mode = mapObject.objectDefName switch
                    {
                        "manhole" => "manhole (down)",
                        "manholeLightCone" => "manhole (up)"
                    };
                    roomConnections.Add(new ConnectionCsvEntry(roomData, new RoomData(otherRoom), mode, mapObject.position.ToVector3()));
                    numConnections += 1;
                }
            }

            // Move player into room so pathfinding can work.
            var player = instance.player;
            if (instance.currentMapId != roomData.Map.id || instance.currentRoomId != roomData.Room.id)
            {
                instance.roomTransitionDestinationMapId = roomData.Map.id;
                instance.roomTransitionDestinationRoomId = roomData.Room.id;
                instance.LoadNextRoomForTransition();
                instance.CleanUpAfterTransition();
            }

            // Find safe tile to put player on.
            var possibleRule = true;
            for (var tileX = 0; tileX < roomData.Coords.Width * W_NUM_TILES && possibleRule; tileX++)
            {
                for (var tileY = 0; tileY < roomData.Coords.Height * H_NUM_TILES; tileY++)
                {
                    // Use tile center's coordinates.
                    var x = tileX * TILE_W + TILE_W / 2;
                    var y = tileY * TILE_H + TILE_H / 2;
                    var attribute = (ulong)Mapvania.GetTileAttribute(roomData.Room, new Vector2(x, y));
                    var isValidGround = (attribute & (ulong)Mapvania.TileAttribute.GroundMask) != 0;
                    var isValidNotGround = attribute == 0 || (attribute & (ulong)Mapvania.TileAttribute.HazardsCoverableByGroundMask) != 0;
                    if (isValidGround)
                    {
                        player.position = new Vector2(x, y);
                        possibleRule = false;
                        break;
                    }
                }
            }

            // Check if all connections are reachable with each other.
            if (!possibleRule)
            {
                var permutations = roomConnections
                    .SelectMany((x, i) => roomConnections.Where((y, j) => j != i), (x, y) => new List<ConnectionCsvEntry> { x, y })
                    .ToList();

                foreach (var permutation in permutations)
                {
                    var pathfindOptions = new Il2CppPipistrello.Object.PathfindOptions()
                    {
                        modeMask = Il2CppPipistrello.Object.PathfindMode.DiagonalMovement
                    };
                    var result = player.Pathfind(Mapvania.GetCellIndexAt(roomData.Room, roomConnections[0].Position), Mapvania.GetCellIndexAt(roomData.Room, roomConnections[^1].Position), pathfindOptions);
                    if (result == null)
                    {
                        possibleRule = true;
                        break;
                    }
                }
            }

            for (var i = 0; i < roomConnections.Count; i++)
            {
                var entry = roomConnections[i];
                entry.PossibleRule = possibleRule;
                roomConnections[i] = entry;
            }

            connections.AddRange(roomConnections);
            expectedConnections.Add(new ExpectedConnectionCsvEntry(roomData, numConnections));
        }

        // Write to connections.csv.
        var connectionsCsvLines = connections
            .OrderBy(c => c.RoomData)
            .ThenBy(c => c.OtherRoomData)
            .Select(c => c.Csv)
            .ToList();
        connectionsCsvLines.Insert(0, ConnectionCsvEntry.Header);
        var connectionsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dev_arch", "connections.csv");
        WriteFileIfChanged(connectionsPath, connectionsCsvLines);

        // Write to expected_connections.csv.
        var expectedConnectionsCsvLines = expectedConnections
            .Select(c => c.Csv)
            .ToList();
        expectedConnectionsCsvLines.Insert(0, ExpectedConnectionCsvEntry.Header);
        var expectedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dev_arch", "expected_connections.csv");
        WriteFileIfChanged(expectedPath, expectedConnectionsCsvLines);
    }

    static bool ShouldSkipRoom(Mapvania.Room room)
    {
        return room.properties.unreachable || room.properties.dev;
    }

    /// <summary>
    /// Gets the full room label, including location, coordinates, and room ID.
    /// E.g. North Plaza (Sewers) (X-3,Y-9) - yug1417
    /// </summary>
    static string GetRoomLabel(Mapvania.Room room)
    {
        var map = mapDict[room.mapId];
        var roomLabel = (string)gameFormatRevealedLocationMethod.Invoke(null, [map, room, new Il2CppSystem.Nullable<Vector2>()]);
        roomLabel = roomLabel.Replace("[nbsp]", " ");
        return $"{roomLabel} - {room.id}";
    }

    static Vector3 GetConnectionPosition(RoomData room, RoomData otherRoom, string direction, bool hasGroundAccess)
    {
        var tileXEnumerator = Enumerable.Range(0, room.Coords.Width * W_NUM_TILES);
        var xOffset = 0;
        if (direction == "left")
        {
            tileXEnumerator = Enumerable.Range(0, 1);
            xOffset = -TILE_W;
        }
        else if (direction == "right")
        {
            tileXEnumerator = Enumerable.Range(room.Coords.Width * W_NUM_TILES - 1, 1);
            xOffset = TILE_W;
        }

        var tileYEnumerator = Enumerable.Range(0, room.Coords.Height * H_NUM_TILES);
        var yOffset = 0;
        if (direction == "up")
        {
            tileYEnumerator = Enumerable.Range(0, 1);
            yOffset = -TILE_H;
        }
        else if (direction == "down")
        {
            tileYEnumerator = Enumerable.Range(room.Coords.Height * H_NUM_TILES - 1, 1);
            yOffset = TILE_H;
        }

        foreach (var tileX in tileXEnumerator)
        {
            foreach (var tileY in tileYEnumerator)
            {
                // Use tile center's coordinates.
                var x = tileX * TILE_W + TILE_W / 2;
                var y = tileY * TILE_H + TILE_H / 2;
                var attribute = (ulong)Mapvania.GetTileAttribute(room.Room, new Vector2(x, y));
                if ((attribute & (ulong)Mapvania.TileAttribute.ImpassableForPlayerMask) != 0)
                {
                    continue;
                }

                var isValidGround = (attribute & (ulong)Mapvania.TileAttribute.GroundMask) != 0;
                var isValidNotGround = attribute == 0 || (attribute & (ulong)Mapvania.TileAttribute.HazardsCoverableByGroundMask) != 0;
                if ((hasGroundAccess && isValidGround) || (!hasGroundAccess && isValidNotGround))
                {
                    var connectionInternalPosition = new Vector2(x, y);

                    var connectionGlobalPosition = room.Room.position + connectionInternalPosition;
                    var connectedRoomPosition = connectionGlobalPosition + new Vector2(xOffset, yOffset);
                    var connectedRoom = Mapvania.FindRoomAtGlobalPosition(room.Map, connectedRoomPosition.ToVector3());
                    if (connectedRoom != null && connectedRoom.globalRoomId.IsEqual(otherRoom.Room.globalRoomId))
                    {
                        return connectionInternalPosition.ToVector3();
                    }
                }
            }
        }

        MelonLogger.Msg($"No connection found for {room.RoomLabel} -> {otherRoom.RoomLabel} || {direction}, {hasGroundAccess}");
        return new Vector3(-1, -1, -1);
    }

    static void WriteFileIfChanged(string path, IEnumerable<string> lines)
    {
        if (File.Exists(path))
        {
            var existingLines = File.ReadLines(path);
            if (existingLines.SequenceEqual(lines))
            {
                return;
            }

            var differences = existingLines.Zip(lines, (val1, val2) => new { val1, val2 })
                .Select((pair, index) => new { Index = index, pair.val1, pair.val2 })
                .Where(item => item.val1 != item.val2);
            foreach (var diff in differences)
            {
                MelonLogger.Msg($"Diff - Idx: {diff.Index} || Existing: {diff.val1} || New: {diff.val2}");
            }
        }

        File.WriteAllLines(path, lines);
        MelonLogger.Msg($"Wrote to {path} due to changes");
    }

    readonly struct RoomData(Mapvania.Room room) : IComparable<RoomData>
    {
        public readonly Mapvania.Room Room => room;
        public readonly Mapvania.Map Map => mapDict[room.mapId];
        public readonly RoomCoordinates Coords => new(room);
        public readonly string LocationName => Game.FormatLocationName(Map, room);
        public readonly string RoomLabel => GetRoomLabel(room);
        public int CompareTo(RoomData other)
        {
            return (LocationName, Coords.Y, Coords.X)
                .CompareTo((other.LocationName, other.Coords.Y, other.Coords.X));
        }

    }

    readonly struct RoomCoordinates(Mapvania.Room room)
    {
        public readonly int X => room.x / (TILE_W * W_NUM_TILES);
        public readonly int Y => room.y / (TILE_H * H_NUM_TILES);
        public readonly int Width => room.wTiles / W_NUM_TILES;
        public readonly int Height => room.hTiles / H_NUM_TILES;
    }

    readonly struct RoomCsvEntry(RoomData roomData)
    {
        public readonly RoomData RoomData => roomData;

        public readonly string Csv => string.Join(CSV_SEP, [
                RoomData.RoomLabel, 
                RoomData.LocationName, 
                RoomData.Room.id, 
                RoomData.Coords.X, 
                RoomData.Coords.Y, 
                RoomData.Coords.Width, 
                RoomData.Coords.Height]);

        public static string Header => string.Join(CSV_SEP, ["Room Label", "Location", "Room ID", "X", "Y", "W", "H"]);
    }

    struct ConnectionCsvEntry(RoomData roomData, RoomData otherRoomData, string mode, Vector3 position)
    {
        public readonly RoomData RoomData => roomData;
        public readonly RoomData OtherRoomData => otherRoomData;
        public readonly string Mode => mode;
        /// <summary>
        /// Position within the room, at the center of the tile.
        /// </summary>
        public readonly Vector3 Position => position;

        public bool PossibleRule;

        public readonly string Csv => string.Join(CSV_SEP, [
            roomData.RoomLabel,
            mode,
            OtherRoomData.RoomLabel,
            PossibleRule]);

        public static string Header => string.Join(CSV_SEP, ["Room Label 1", "Exit direction", "Room Label 2", "Possible Rules?"]);
    }

    readonly struct ExpectedConnectionCsvEntry(RoomData roomData, int numConnections)
    {
        public readonly RoomData RoomData => roomData;
        public readonly int NumConnections => numConnections;

        public readonly string Csv => string.Join(CSV_SEP, [roomData.RoomLabel, numConnections]);

        public static string Header => string.Join(CSV_SEP, ["Room Label", "Expected"]);
    }
}

