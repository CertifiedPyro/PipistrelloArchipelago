using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Exceptions;
using Archipelago.MultiClient.Net.Models;
using Il2CppPipistrello;
using Il2CppUtil;
using MelonLoader;
using PipistrelloArchipelago.Handlers;

namespace PipistrelloArchipelago;

public static class ArchipelagoHelper
{
    private static readonly Dictionary<string, string> ItemToFlag = new()
    {
        { "Offstring Throw", Game.FLAG_ABILITY_THROW },
        { "Walk-the-Dog", Game.FLAG_ABILITY_WALKTHEDOG },
        { "Wall-Dash", Game.FLAG_ABILITY_WALLDASH },
        { "UFO Throw", Game.FLAG_ABILITY_HELIX },
        { "Wall-Ride", Game.FLAG_ABILITY_WALLRAILING },
        { "Sleeper", Game.FLAG_ABILITY_CHARGED_SLEEPER },
        { "Flurry Attack", Game.FLAG_ABILITY_CHARGED_FLURRY },
        { "Cat's Cradle", Game.FLAG_ABILITY_CHARGED_SPREAD },
        { "Merry-Go-Round", Game.FLAG_ABILITY_CHARGED_SPIN },
        { "Parry", Game.FLAG_ABILITY_SPECIAL_PARRY },
        { "Around-the-World", Game.FLAG_ABILITY_SPECIAL_SPIN },
        { "Coin-Flip", Game.FLAG_ABILITY_SPECIAL_COINFLIP },
        { "Mega-Battery 1", Game.FLAG_MEGABATTERY1 },
        { "Mega-Battery 2", Game.FLAG_MEGABATTERY2 },
        { "Mega-Battery 3", Game.FLAG_MEGABATTERY3 },
        { "Mega-Battery 4", Game.FLAG_MEGABATTERY4 },
    };

    private static readonly Dictionary<string, Game.Upgrade> ItemToUpgrade = Game.upgrades
        .ToArray()
        .ToDictionary(u => Localization.Get($"upgrade_name_{u.id}", lang: "en_US"));

    private static readonly Dictionary<string, Game.Equip> ItemToEquip = Game.equips
        .ToArray()
        .ToDictionary(e => Localization.Get($"equip_name_{e.id}", lang: "en_US"));

    private static bool _isItemHandlerEnabled;

    /// <summary>
    /// Handles the connection to the Archipelago server.
    /// </summary>
    /// <returns>true if the connection succeeded, false otherwise.</returns>
    public static async Task<bool> ConnectAsync()
    {
        if (Global.State.Session != null)
        {
            Global.State.Session.Locations.CheckedLocationsUpdated -= LocationHandler.Process;
            if (Global.State.Session.Socket.Connected)
            {
                await Global.State.Session.Socket.DisconnectAsync();
            }
        }

        var host = ModSettings.Host.Value;
        var port = ModSettings.Port.Value;
        var session = ArchipelagoSessionFactory.CreateSession(host, port);
        session.Locations.CheckedLocationsUpdated += LocationHandler.Process;

        LoginResult result;
        try
        {
            await session.ConnectAsync();
            result = await session.LoginAsync(
                "Pipistrello and the Cursed Yoyo",
                ModSettings.SlotName.Value,
                Archipelago.MultiClient.Net.Enums.ItemsHandlingFlags.AllItems,
                password: ModSettings.Password.Value);
        }
        catch (Exception e)
        {
            result = new LoginFailure(e.GetBaseException().Message);
        }

        if (result is LoginSuccessful loginSuccess)
        {
            Global.State = new()
            {
                Session = session,
                SlotData = loginSuccess.SlotData,
                ScoutedLocations = await session.Locations.ScoutLocationsAsync(
                    [.. session.Locations.AllLocations])
            };
            if (!_isItemHandlerEnabled)
            {
                _ = StartReceivedItemsLoop();
            }

            return true;
        }
        else
        {
            Melon<PipArchMod>.Logger.Error($"Failed to connect: {host}:{port}");
            var loginFailure = (LoginFailure)result;
            foreach (var error in loginFailure.Errors)
            {
                Melon<PipArchMod>.Logger.Error(error);
            }

            return false;
        }
    }

    /// <summary>
    /// The received item event loop.
    /// </summary>
    public static async Task StartReceivedItemsLoop()
    {
        _isItemHandlerEnabled = true;
        var director = Global.Director;
        try
        {
            while (true)
            {
                await Task.Delay(1000);

                if (!Global.State.SaveFileLoaded)
                {
                    continue;
                }

                var helper = Global.State.Session.Items;
                var lastIndex = director.GetFlag(Constants.FLAG_LAST_ITEM_INDEX);
                if (lastIndex > helper.AllItemsReceived.Count)
                {
                    Melon<PipArchMod>.Logger.Error("Received item index was not expected.");
                    Melon<PipArchMod>.Logger.Error($"Received index: {helper.Index} | Last index: {lastIndex}");

                    var text = $"[instant|[c:red|Network error - Please reconnect.]][w:2]";
                    Global.State.Messages.Enqueue(text);
                    return;
                }

                while (lastIndex < helper.AllItemsReceived.Count)
                {
                    var item = helper.AllItemsReceived[lastIndex];
                    var isLocalLocation = item.Player.Slot == Global.State.Session.ConnectionInfo.Slot &&
                                          Global.State.LocalCheckedLocations.ContainsKey(item.LocationId);
                    var result = HandleItem(item, queueMessage: !isLocalLocation);
                    if (result)
                    {
                        lastIndex++;
                        director.SetFlag(Constants.FLAG_LAST_ITEM_INDEX, lastIndex);
                        director.PrepareCheckpoint(false);
                    }
                    else
                    {
                        var text = $"[instant|[c:red|Error - Cannot handle item: {item.ItemDisplayName}.]][w:2]";
                        Global.State.Messages.Enqueue(text);
                        return;
                    }
                }

                // Dequeue all items.
                while (helper.DequeueItem() != null)
                {
                }
            }
        }
        catch (Exception ex)
        {
            Melon<PipArchMod>.Logger.Error($"Exception receiving item: {ex}");
        }
        finally
        {
            _isItemHandlerEnabled = false;
        }
    }

    /// <summary>
    /// Handle Archipelago items and locations after connection.
    /// This method must be called after a save file has been selected already.
    /// </summary>
    public static void HandleInitial()
    {
        try
        {
            // Set SaveFileLoaded at the start, so HandleCheckedLocations() will run as expected.
            Global.State.SaveFileLoaded = true;
            var director = Global.Director;

            // Handle remote-checked locations.
            // Archipelago sends every checked location on connection.
            Melon<PipArchMod>.Logger.Msg("Handling remote checked locations...");
            var locationsHelper = Global.State.Session.Locations;
            LocationHandler.Process(locationsHelper.AllLocationsChecked);

            // Handle missed local-checked locations.
            Melon<PipArchMod>.Logger.Msg("Handling local checked locations...");
            var checkedLocationsSet = new HashSet<long>(locationsHelper.AllLocationsChecked);
            var missedLocalLocations = new List<long>();
            foreach (var locationId in locationsHelper.AllMissingLocations)
            {
                // Check physical Archipelago items.
                var objectId = Utils.LocationIdToObjectId(locationId);
                var archObjectId = Utils.IdToArchItemId(objectId);
                var bpFlag = Game.FlagBpContainerAcquired(archObjectId);
                if (director.GetFlagBool(bpFlag))
                {
                    missedLocalLocations.Add(locationId);
                }

                // Check money bags
                var mapObject = Utils.GetMapvaniaObject(objectId);
                if (mapObject == null)
                {
                    continue;
                }

                var moneyBagDespawnFlag =
                    $"{Game.GLOBAL_FLAG_PREFIX}{mapObject.globalObjectId.AsStringNoRoom}{Game.FLAG_OBJECT_DESPAWN_SUFFIX}";
                if (director.GetFlag(moneyBagDespawnFlag) != 0)
                {
                    missedLocalLocations.Add(locationId);
                }
            }

            // Check missed taxi phones.
            foreach (var objectId in director.playerRecord.taxiPhonesUnlocked)
            {
                if (!Utils.IsObjectIdActiveLocation(objectId.AsString))
                {
                    continue;
                }

                var locationId = Utils.ObjectIdToLocationId(objectId.AsString);
                if (!checkedLocationsSet.Contains(locationId))
                {
                    missedLocalLocations.Add(locationId);
                }
            }

            if (missedLocalLocations.Count > 0)
            {
                Melon<PipArchMod>.Logger.Msg(
                    $"Found unsent location check ids: {string.Join(',', missedLocalLocations)}");
                try
                {
                    locationsHelper.CompleteLocationChecks([.. missedLocalLocations]);
                }
                catch (ArchipelagoSocketClosedException ex)
                {
                    Melon<PipArchMod>.Logger.Error($"Could not send location checks: {ex}");
                }
            }

            // Prepare checkpoint to ensure any missing items or locations will be saved.
            director.PrepareCheckpoint(false);
        }
        catch (Exception ex)
        {
            Melon<PipArchMod>.Logger.Error("Exception handling initial received items: " + ex);
        }
    }

    /// <summary>
    /// Handles a received item from Archipelago.
    /// </summary>
    /// <param name="item">The received item.</param>
    /// <param name="queueMessage">Whether to always queue a message.</param>
    /// <returns>true if the item was handled successfully, false otherwise.</returns>
    private static bool HandleItem(ItemInfo item, bool queueMessage = false)
    {
        try
        {
            var itemName = item.ItemName;
            var director = Global.Director;
            var followingObjects = director.player.followingObjects.ToArray();
            var result = true;
            Melon<PipArchMod>.Logger.Msg($"Received item: {itemName}");

            if (ItemToFlag.ContainsKey(itemName))
            {
                director.SetFlagBool(ItemToFlag[itemName], true);
                Melon<PipArchMod>.Logger.Msg($"Set flag: {ItemToFlag[itemName]}");
            }
            else if (itemName == "Petal Container")
            {
                Game.SetPetalContainerAcquired(director, "filler_pc", 1, true);
                Game.SetPetalContainerAcquired(director, "filler_pc", 0, false);
                Melon<PipArchMod>.Logger.Msg("Added petal container");
            }
            else if (itemName == "BP Shard")
            {
                Game.SetBpContainerAcquired(director, "filler_bp", 1, true);
                Game.SetBpContainerAcquired(director, "filler_bp", 0, false);
                Melon<PipArchMod>.Logger.Msg("Added BP shard");
            }
            else if (itemName.Contains('$')
                     && int.TryParse(itemName[(itemName.IndexOf('$') + 1)..], out var money))
            {
                // CollectCoin properly handles debts.
                Global.Director.CollectCoin(money);
                Melon<PipArchMod>.Logger.Msg("Added $" + money);
            }
            else if (ItemToUpgrade.TryGetValue(itemName, out var upgrade))
            {
                Game.SetUpgradeAcquired(director, upgrade, true);
                Melon<PipArchMod>.Logger.Msg("Added upgrade");
            }
            else if (ItemToEquip.FirstOrDefault(pair => itemName.Contains(pair.Key)) is (not null, var equip))
            {
                var equipAcquired = Game.IsEquipAcquired(director.playerRecord, equip);
                if (!equipAcquired)
                {
                    Game.SetEquipAcquired(director, equip, true, true);
                    Melon<PipArchMod>.Logger.Msg("Added equip");
                }
                else
                {
                    Game.SetEquipRefined(director, equip, true);
                    Melon<PipArchMod>.Logger.Msg("Refined equip");
                }
            }
            else if (itemName.Contains("Staff ID"))
            {
                var staffIdObject = Utils.GetMapvaniaObject("yugo3_dev/yug4006/yug4042")!;
                // Check that the staff ID isn't already turned in for dungeon access or following the player.
                if (!director.GetFlagBool($"{Game.GLOBAL_FLAG_PREFIX}fariaLimeDungeonAccess")
                    && followingObjects.All(o => o.objectDefName != staffIdObject.objectDefName))
                {
                    // Use Staff ID from a dev map that we know won't be changed.
                    // TODO: Find better way to add following object that activates immediately.
                    director.playerRecord.followingObjectIds.Add(staffIdObject.globalObjectId);
                    Melon<PipArchMod>.Logger.Msg("Added Staff ID to following objects");
                }
            }
            else
            {
                result = false;
                Melon<PipArchMod>.Logger.Error($"Could not handle item: {itemName}");
            }

            if (result && queueMessage)
            {
                var itemDisplayName = item.ItemDisplayName.Replace(" ", "[nbsp]");
                var playerName = item.Player.Name.Replace(" ", "[nbsp]");

                var text = $"[instant|You received [c:blue|{itemDisplayName}] from [c:red|{playerName}]!][w:2]";
                Global.State.Messages.Enqueue(text);
            }

            return result;
        }
        catch (Exception ex)
        {
            Melon<PipArchMod>.Logger.Error($"Exception handling item: {ex}");
            return false;
        }
    }
}
