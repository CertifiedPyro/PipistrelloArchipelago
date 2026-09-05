# Pipistrello and the Cursed Yoyo Archipelago Mod

## Required Software

* [Archipelago](https://github.com/ArchipelagoMW/Archipelago/releases)
* [MelonLoader](https://melonwiki.xyz/#/README?id=requirements)
  * Requires [Microsoft Visual C++ 2015-2019 Redistributable 64 Bit](https://aka.ms/vs/16/release/vc_redist.x64.exe)
  * Requires [dotnet 6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0#runtime-desktop-6.0.19)
* [MelonPreferencesManager](https://github.com/piepieonline/MelonPreferencesManager/releases)
* [Pipistrello Archipelago Mod](https://github.com/CertifiedPyro/PipistrelloArchipelago/releases)

## Installation

### Prerequisites
1. Run the MelonLoader installer, select "Pipistrello and the Cursed Yoyo", and click "Install".
2. If you're running Linux:
    1. Export the following environment variable: `WINEDLLOVERRIDES="version=n,b"`
    2. On Steam, you can set the launch options to: `WINEDLLOVERRIDES="version=n,b" %command%`
3. Launch the game to create the required mod folders.
4. Navigate to your game's installation installation folder.
    1. You can find this in Steam by right-clicking > Manage > Browse Local Files.
    2. This is usually  `C:\Program Files (x86)\Steam\steamapps\common\Pipistrello and the Cursed Yoyo`
5. Download the latest release of MelonPreferencesManager.
6. Extract the two dlls into the `Mods/` folder under your game's installation directory.

### Archipelago mod

1. Download `PipistrelloArchipelago.dll` from the latest release.
2. Place the dll file into the `Mods/` folder under the game's installation folder.
3. Launch the game. The mod is installed correclty if a **Connect** button appears on the main menu.

### Archipelago tools

1. Make sure the Archipelago software is installed.
2. Download `pipistrello.apworld` from the latest release.
3. Double-click on `pipistrello.apworld`. Archipelago should install the apworld automatically.
4. Open the Archipelago Launcher and run "Generate Template Options" to create the options template file.
    1. Alternatively, you can download `Pipistrello.and.the.Cursed.Yoyo.yaml` from the latest release.

## Generating a game

Follow [the official instructions](https://archipelago.gg/tutorial/Archipelago/setup_en#generating-a-game).

## Joining a multiworld game
1. Start the game after installing all necessary mods.
2. Press F5 to open MelonPreferencesManager and input your connection information.
    1. Note: the password field is **not protected** and is fully visible.
3. Press the **Connect** button in-game.
4. Once connected, you can press **Load Game**.
5. Start with a *new* save file. The game will stall for a few seconds before loading.
6. You should now be loaded directly into South Plaza!

## Hints and trackers

There is no visual tracker yet, so please use Universal Tracker for now. In Universal Tracker. locations are sorted by
area (in logical order).

# Gameplay info

## What does randomization do to this game?

The randomizer is still in early alpha, so only South Plaza and Faria (including the mini-dungeon and dungeon)
are randomized.
Items and rewards are randomized and replaced with location checks.

The goal is to reach North Plaza via the sewers. In addition to hitting the 2 levers, you must also:

- Obtain the Faria Mega-Battery
- Defeat the Slime Tycoon in SlimeCorp Excavation Site

You can also pick from several levels of logic difficulty.
Make sure you read the description of the difficulty setting in the options yaml before selecting a difficulty above
Normal.

## What items get shuffled?

By default, the following items are in the item pool:
- Abilities
- Badges
    - Badges are progressive. The first item will be the base badge, and the second item will be the refined badge.
- BP shards
- Charged moves
- Mega-Batteries
- Petal containers
- Special moves
- Special items (e.g. Staff ID for the Faria dungeon)
- Upgrades

Money bags are given as filler items.

## What locations get shuffled?

By default, the following locations are enabled:
- Badges
- BP shards
- Combat rewards (only required ones)
- Diamonds
- Musical notes rewards
- Petal containers
- Quest rewards
- Taxi phone interactions

Additionally, the following options can be enabled:

- Moneysanity - Adds standalone money bags and money bags from optional combat encounters as location checks
    - Note: Money bags from combats/quests/etc are always enabled as locations, regardless of this setting

The upgrade tree and badge refinements *are not* currently randomized.

## What does another world's item look like in Pipistrello and the Cursed Yoyo?

Items from other worlds show up as Archipelago sprites in the game world, with corresponding Archipelago icons for map
pins. If the location was originally a money bag, the sprite and map pin will be green instead.

When you collect another world's item, you will get a message showing the item name and recipient.

## When the player receives an item, what happens?

The item is instantly granted to you, and a message appears on the bottom of the screen showing the item name and sender.

# Credits
- **sshard**, **Chibisatan** and everyone in the Pocket Trap Speedrunners Discord for help with making rules and finding new tricks.
- **Scipio** for the original Archipelago sprites used in-game.
- **CrusherRL** for inspiration on how to bundle `Archipelago.MultiClient.Net.dll` with the mod.
- **Lordmau5** for inspiration on how to mask the password in Melon Preferences Manager.
