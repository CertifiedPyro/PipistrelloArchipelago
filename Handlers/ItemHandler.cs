using Archipelago.MultiClient.Net.Colors;
using Archipelago.MultiClient.Net.Models;
using Il2CppPipistrello;
using Il2CppUtil;
using MelonLoader;

namespace PipistrelloArchipelago.Handlers;

internal static class ItemHandler
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
        { "Mega-Battery 4", Game.FLAG_MEGABATTERY4 }
    };

    private static readonly Dictionary<string, Game.Upgrade> ItemToUpgrade = Game.upgrades
        .ToArray()
        .ToDictionary(u => Localization.Get($"upgrade_name_{u.id}", "en_US"));

    private static readonly Dictionary<string, Game.Equip> ItemToEquip = Game.equips
        .ToArray()
        .ToDictionary(e => Localization.Get($"equip_name_{e.id}", "en_US"));

    private static CancellationTokenSource _cancellationTokenSource;

    /// <summary>
    /// Starts the received item event loop.
    /// </summary>
    public static async Task Start()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        var director = Global.Director;
        try
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                await Task.Delay(1000);

                if (!Global.State.SaveFileLoaded)
                {
                    continue;
                }

                var helper = Global.State.Session.Items;
                var lastIndex = director.GetFlag(Constants.FlagLastItemIndex);
                if (lastIndex > helper.AllItemsReceived.Count)
                {
                    Global.State.Messages.Enqueue("[c:red|Unexpected item index. Please quit and reconnect.]");
                    Melon<PipArchMod>.Logger.Error("Received item index was not expected.");
                    Melon<PipArchMod>.Logger.Error($"Received index: {helper.Index} | Last index: {lastIndex}");
                    return;
                }

                while (lastIndex < helper.AllItemsReceived.Count)
                {
                    var item = helper.AllItemsReceived[lastIndex];
                    var isItemFromLocalLocation = Utils.IsLocalItem(item)
                                                  && Global.State.LocalCheckedLocations.ContainsKey(item.LocationId);
                    var result = HandleItem(item, !isItemFromLocalLocation);
                    if (!result)
                    {
                        return;
                    }

                    director.SetFlag(Constants.FlagLastItemIndex, ++lastIndex);
                    director.PrepareCheckpoint(false);
                }

                // Dequeue all items.
                while (helper.DequeueItem() != null)
                {
                }
            }
        }
        catch (Exception e)
        {
            Melon<PipArchMod>.Logger.Error($"Exception receiving item: {e}");
        }
        finally
        {
            Melon<PipArchMod>.Logger.Msg($"Stopping {nameof(ItemHandler)}...");
            _cancellationTokenSource = null;
        }
    }

    public static void End()
    {
        _cancellationTokenSource?.Cancel();
    }

    /// <summary>
    /// Handles a received Archipelago item.
    /// </summary>
    /// <param name="item">The received item.</param>
    /// <param name="queueMessage">Whether to queue a message.</param>
    /// <returns>true if the item was handled successfully, false otherwise.</returns>
    private static bool HandleItem(ItemInfo item, bool queueMessage = false)
    {
        try
        {
            var itemName = item.ItemName;
            var director = Global.Director;
            var followingObjectIds = director.playerRecord.followingObjectIds.ToArray();
            var result = true;
            Melon<PipArchMod>.Logger.Msg($"Received item: {itemName}");

            if (ItemToFlag.TryGetValue(itemName, out var itemFlag))
            {
                director.SetFlagBool(itemFlag, true);
                Melon<PipArchMod>.Logger.Msg($"Set flag: {ItemToFlag[itemName]}");
            }
            else if (ItemToUpgrade.TryGetValue(itemName, out var upgrade))
            {
                Game.SetUpgradeAcquired(director, upgrade, true);
                Melon<PipArchMod>.Logger.Msg("Added upgrade");
            }
            else if (ItemToEquip.FirstOrDefault(pair => itemName.Contains(pair.Key)) is (not null, var equip))
            {
                if (Game.IsEquipAcquired(director.playerRecord, equip))
                {
                    Game.SetEquipRefined(director, equip, true);
                    Melon<PipArchMod>.Logger.Msg("Refined equip");
                }
                else
                {
                    Game.SetEquipAcquired(director, equip, true, true);
                    Melon<PipArchMod>.Logger.Msg("Added equip");
                }
            }
            else if (itemName.Contains('$')
                     && int.TryParse(itemName[(itemName.IndexOf('$') + 1)..], out var money))
            {
                // CollectCoin properly handles debts.
                Global.Director.CollectCoin(money);
                Melon<PipArchMod>.Logger.Msg("Added $" + money);
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
            else if (itemName == "Staff ID")
            {
                // Use Staff ID from a dev map that we know won't be changed.
                var staffIdObject = Utils.GetMapvaniaObject("yugo3_dev/yug4006/yug4042")!;
                // Check that the staff ID isn't already turned in for dungeon access or following the player.
                if (!director.GetFlagBool($"{Game.GLOBAL_FLAG_PREFIX}fariaLimeDungeonAccess")
                    && followingObjectIds.All(o => o.AsString != staffIdObject.globalObjectId.AsString))
                {
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
                var itemColor = Utils.GetTextColor(ColorUtils.GetColor(item).ToString());
                var playerName = item.Player.Name.Replace(" ", "[nbsp]");
                var playerColor = Utils.IsLocalItem(item)
                    ? Utils.GetTextColor(ColorUtils.ActivePlayerColor.ToString())
                    : Utils.GetTextColor(ColorUtils.NonActivePlayerColor.ToString());

                var text = Utils.IsLocalItem(item)
                    ? $"You found your [c:{itemColor}|{itemName}]!"
                    : $"You received [c:{itemColor}|{itemDisplayName}] from [c:{playerColor}|{playerName}]!";
                Global.State.Messages.Enqueue(text);
            }
            else if (!result)
            {
                var text = $"[c:red|Unexpected item: {item.ItemDisplayName}. Please quit and reconnect.]";
                Global.State.Messages.Enqueue(text);
            }

            return result;
        }
        catch (Exception e)
        {
            Melon<PipArchMod>.Logger.Error($"Exception handling item: {e}");
            return false;
        }
    }
}
