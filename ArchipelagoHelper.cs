using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Exceptions;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using Il2CppPipistrello;
using Il2CppUtil;
using MelonLoader;
using System.Collections.ObjectModel;

namespace PipistrelloArchipelago;

public static class ArchipelagoHelper
{
    private static readonly Dictionary<string, string> _itemToFlag = new()
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

    /// <summary>
    /// Handle connection to Archipelago server.
    /// </summary>
    /// <returns>true if the connection succeeded, false otherwise.</returns>
    public static async Task<bool> ConnectAsync()
    {
        if (SaveState.Session != null)
        {
            SaveState.Session.Locations.CheckedLocationsUpdated -= CheckedLocationsHandler;
            SaveState.Session.Items.ItemReceived -= ItemReceivedHandler;
            if (SaveState.Session.Socket.Connected)
            {
                await SaveState.Session.Socket.DisconnectAsync();
            }
        }

        SaveState.Reset();

        var host = ModSettings.Host.Value;
        var port = ModSettings.Port.Value;
        var session = ArchipelagoSessionFactory.CreateSession(host, port);
        SaveState.Session = session;
        session.Locations.CheckedLocationsUpdated += CheckedLocationsHandler;
        session.Items.ItemReceived += ItemReceivedHandler;

        LoginResult result;
        try
        {
            Melon<PipArchMod>.Logger.Msg($"Connecting to {host}:{port}");
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
            SaveState.SlotData = loginSuccess.SlotData;
            SaveState.ScoutedLocations = await session.Locations.ScoutLocationsAsync(
                [.. session.Locations.AllLocations]);
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
        }

        return false;
    }

    /// <summary>
    /// Handle checked locations.
    /// </summary>
    /// <param name="newCheckedLocations">The list of location ids checked.</param>
    public static void CheckedLocationsHandler(ReadOnlyCollection<long> newCheckedLocations)
    {
        // Ignore if save file isn't selected yet.
        var director = GlobalState.Director;
        if (director.selectedSavefileIndex == -1)
        {
            return;
        }

        foreach (var locationId in newCheckedLocations)
        {
            // Flag the item as acquired, so it doesn't show up again.
            var locationName = SaveState.Session.Locations.GetLocationNameFromId(locationId);
            var objectId = GlobalState.LocationNameToGlobalObjectId[locationName];
            var archObjectId = Utils.IdToArchItemId(objectId);
            var flag = Game.FlagBpContainerAcquired(archObjectId);
            if (!director.GetFlagBool(flag))
            {
                Melon<PipArchMod>.Logger.Msg($"Setting flag {flag} for location {locationName}");
                director.SetFlagBool(flag, true);
            }

            // Remove map pin.
            var mapPins = director.playerRecord.mapPins;
            for (var i = 0; i < mapPins.Count; i++)
            {
                var mapPinObjectId = mapPins[i].objectId.AsString;
                if (mapPinObjectId == archObjectId)
                {
                    mapPins.RemoveAt(i);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// The received item event listener.
    /// </summary>
    public static void ItemReceivedHandler(ReceivedItemsHelper helper)
    {
        try
        {
            var director = GlobalState.Director;
            if (director.selectedSavefileIndex != -1)
            {
                // If a save file is selected, handle item normally.
                var lastIndex = director.GetFlag(Constants.FLAG_LAST_ITEM_INDEX);
                if (helper.Index == lastIndex + 1)
                {
                    var result = HandleItem(helper.PeekItem());
                    if (result)
                    {
                        director.SetFlag(Constants.FLAG_LAST_ITEM_INDEX, lastIndex + 1);
                        helper.DequeueItem();
                    }
                }
                else
                {
                    Melon<PipArchMod>.Logger.Error("Received item index was not expected.");
                    Melon<PipArchMod>.Logger.Error($"Received index: {helper.Index} | Last index: {lastIndex}");

                    var text = $"[instant|[c:red|Network error - Please reconnect.]][w:2]";
                    SaveState.Messages.Enqueue(text);
                }
            }
        }
        catch (Exception ex)
        {
            Melon<PipArchMod>.Logger.Error($"Exception receiving item: {ex}");
        }
    }

    /// <summary>
    /// Handle the received items packet that's received after connection.
    /// This method must be called after a save file has been selected already.
    /// </summary>
    public static void InitialHandler()
    {
        try
        {
            // Handle initial received items.
            // Archipelago sends every received item on connection.
            // Note: this assumes that player must reconnect every time from main menu.
            Melon<PipArchMod>.Logger.Msg("Handling initial received items...");
            var director = GlobalState.Director;
            var itemsHelper = SaveState.Session.Items;
            var index = itemsHelper.Index;
            var lastIndex = director.GetFlag(Constants.FLAG_LAST_ITEM_INDEX);
            Melon<PipArchMod>.Logger.Msg($"Current index: {index} | Stored index: {lastIndex}");
            var i = 0;
            while (itemsHelper.Any() && ++i <= index)
            {
                if (i <= lastIndex)
                {
                    itemsHelper.DequeueItem();
                    continue;
                }

                var result = HandleItem(itemsHelper.PeekItem());
                if (result)
                {
                    director.SetFlag(Constants.FLAG_LAST_ITEM_INDEX, i);
                    itemsHelper.DequeueItem();
                }
                else
                {
                    break;
                }
            }

            // Handle remote-checked locations.
            // Archipelago sends every checked location on connection.
            Melon<PipArchMod>.Logger.Msg("Handling remote checked locations...");
            var locationsHelper = SaveState.Session.Locations;
            CheckedLocationsHandler(locationsHelper.AllLocationsChecked);

            // Handle missed local-checked locations.
            Melon<PipArchMod>.Logger.Msg("Handling local checked locations...");
            var checkedLocationsSet = new HashSet<long>(locationsHelper.AllLocationsChecked);
            var missedLocalLocations = new List<long>();
            foreach (var locationId in locationsHelper.AllMissingLocations)
            {
                // Check physical Archipelago items.
                var objectId = Utils.LocationIdToGlobalObjectId(locationId);
                var archObjectId = Utils.IdToArchItemId(objectId);
                var flag = Game.FlagBpContainerAcquired(archObjectId);
                if (director.GetFlagBool(flag) 
                    && !checkedLocationsSet.Contains(locationId))
                {
                    missedLocalLocations.Add(locationId);
                }
            }

            // Check missed taxi phones.
            foreach (var objectId in director.playerRecord.taxiPhonesUnlocked)
            {
                var locationId = Utils.GlobalObjectIdToLocationId(objectId.AsString);
                if (!checkedLocationsSet.Contains(locationId))
                {
                    missedLocalLocations.Add(locationId);
                }
            }

            if (missedLocalLocations.Count > 0)
            {
                Melon<PipArchMod>.Logger.Msg(
                    $"Found unsent location checks: {string.Join(',', missedLocalLocations)}");
                try
                {
                    locationsHelper.CompleteLocationChecks([.. missedLocalLocations]);
                }
                catch (ArchipelagoSocketClosedException ex)
                {
                    Melon<PipArchMod>.Logger.Error($"Could not send location checks: {ex}");
                }
            }
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
    /// <returns>true if the item was handled successfully, false otherwise.</returns>
    private static bool HandleItem(ItemInfo item)
    {
        try
        {
            var director = GlobalState.Director;
            var itemName = item.ItemName;
            var result = true;
            Melon<PipArchMod>.Logger.Msg($"Received item: {itemName}");

            var itemToUpgrade = Game.upgrades
                .ToArray()
                .ToDictionary(u => Localization.Get($"upgrade_name_{u.id}", lang: "en_US"));
            var itemToEquip = Game.equips
                .ToArray()
                .ToDictionary(e => Localization.Get($"equip_name_{e.id}", lang: "en_US"));

            if (_itemToFlag.ContainsKey(itemName))
            {
                director.SetFlagBool(_itemToFlag[itemName], true);
                Melon<PipArchMod>.Logger.Msg($"Set flag: {_itemToFlag[itemName]}");
            }
            else if (itemToUpgrade.ContainsKey(itemName))
            {
                var upgrade = itemToUpgrade[itemName];
                Game.SetUpgradeAcquired(director, upgrade, true);
                Melon<PipArchMod>.Logger.Msg("Added upgrade");
            }
            else if (itemToEquip.Any((pair) => itemName.Contains(pair.Key)))
            {
                Game.equips.ToArray().ToDictionary(e => Localization.Get(e.id, lang: "en_US"));
                var equip = itemToEquip.FirstOrDefault(
                    (pair) => itemName.Contains(pair.Key)).Value;
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
            else if (itemName.Contains("Petal Container"))
            {
                Game.SetPetalContainerAcquired(director, "filler_pc", 1, true);
                Game.SetPetalContainerAcquired(director, "filler_pc", 0, false);
                Melon<PipArchMod>.Logger.Msg("Added petal container");
            }
            else if (itemName.Contains("BP Shard"))
            {
                Game.SetBpContainerAcquired(director, "filler_bp", 1, true);
                Game.SetBpContainerAcquired(director, "filler_bp", 0, false);
                Melon<PipArchMod>.Logger.Msg("Added BP shard");
            }
            else if (itemName.Contains('$') 
                && int.TryParse(itemName[(itemName.IndexOf('$') + 1)..], out var money))
            {
                // CollectCoin properly handles debts.
                GlobalState.Director.CollectCoin(money);
                Melon<PipArchMod>.Logger.Msg("Added $" + money);
            }
            else
            {
                result = false;
                Melon<PipArchMod>.Logger.Error($"Could not handle item: {itemName}");
            }

            if (result && item.Player.Slot != SaveState.Session.ConnectionInfo.Slot)
            {
                var itemDisplayName = item.ItemDisplayName.Replace(" ", "[nbsp]");
                var playerName = item.Player.Name.Replace(" ", "[nbsp]");

                var text = $"[instant|You received [c:blue|{itemDisplayName}] from [c:red|{playerName}]!][w:2]";
                SaveState.Messages.Enqueue(text);
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
