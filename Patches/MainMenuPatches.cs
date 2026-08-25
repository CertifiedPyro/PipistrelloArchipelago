using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using MelonLoader;
using UnityEngine;

namespace PipistrelloArchipelago.Patches;

[HarmonyPatch]
internal static class MainMenuPatches
{
    private static UILabel _connectionStatus;
    private static UIButton _loadGameButton;

    /// <summary>
    /// Patch to handle quitting to title screen.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Director), nameof(Director.QuitToTitleScreen))]
    private static void Director_QuitToTitleScreen_Prefix()
    {
        _ = ArchipelagoHelper.DisconnectAsync();
    }

    /// <summary>
    /// Adds buttons and labels to main menu for connecting to Archipelago.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Menu), nameof(Menu.MakeMainMenu))]
    private static void Menu_MakeMainMenu_Postfix(UIDialog __result)
    {
        Global.State.SaveFileLoaded = false;

        var elements = __result.rootElement.subElements;
        var loadGameButton = elements[0].Cast<UIButton>();

        // Add connect button at start of list.
        var connectButton = loadGameButton.MemberwiseClone().Cast<UIButton>();
        var connectButtonTextFn = () => "Connect";
        connectButton.SetText(connectButtonTextFn);
        elements.Insert(0, connectButton);

        // Add connection status label.
        var labelTextFn = () => "<Waiting for connection>\n---";
        var label = new UILabel(labelTextFn)
        {
            labelHalign = TextRenderer.Halign.Center,
            forceMinWidth = new Il2CppSystem.Nullable<float>(130f),
            marginTop = -4f,
            marginBottom = -4f
        };
        _connectionStatus = label;

        // Wrap connection status label in a panel.
        var panel = new UIPanel { childrenCenterH = true };
        panel.subElements.Add(label);
        elements.Insert(0, panel);

        _loadGameButton = loadGameButton;
        DisableLoadGameButton();

        // Fix offset and assign every row to its correct index.
        __result.rootElement.offset = new Vector3(0, 24, 0);
        for (var i = 1; i < elements.Count; i++)
        {
            elements[i].cellY = i;
        }
    }

    /// <summary>
    /// Connects to Archipelago when "Connect" button is pressed.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(UIElement), nameof(UIElement.PerformPress))]
    private static bool UIElement_PerformPress_Prefix(UIElement __instance)
    {
        var connectButton = __instance.TryCast<UIButton>();
        if (connectButton == null || connectButton.textFn.Invoke() != "Connect")
        {
            return true;
        }

        var labelText = () => "Connecting...\n---";
        _connectionStatus.textFn = labelText;

        DisableLoadGameButton();

        ArchipelagoHelper.ConnectAsync().ContinueWith(OnConnection).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// Disables the spinning 3D console on title screen.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(TitleScreen), nameof(TitleScreen.CanBeginAttractMode))]
    private static bool TitleScreen_CanBeginAttractMode_Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }

    /// <summary>
    /// Modifies the load save file menu to prevent loading non-Archipelago saves.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Menu), nameof(Menu.MakeSavefileLoadMenu))]
    private static void Menu_MakeSavefileLoadMenu_Postfix(UIDialog __result)
    {
        // Get record corresponding with save file menu.
        Func<UIElement, bool> predicate = e => e.TryCast<UISaveFile>() != null;
        var saveFile = __result.FindElementInAllSubDialogs(predicate).Cast<UISaveFile>();
        // saveFile.record is null for some reason, so fetch from Director.
        var record = Global.Director.savefileRecords[saveFile.savefileIndex];

        // Check that save file has Archipelago flag.
        if (record?.flags?.ContainsKey(Constants.FlagArchipelago) == true)
        {
            return;
        }

        // Get the first button, which should be "Load Game".
        predicate = e => e.TryCast<UIButton>() != null;
        var button = __result.FindElementInAllSubDialogs(predicate).Cast<UIButton>();

        // Replace "Load Game" button to avoid loading non-Archipelago saves.
        var textFn = () => "❌ Non-Archipelago save ❌";
        button.SetText(textFn);
        button.canPress = false;
    }

    /// <summary>
    /// Modifies the new save file menu to remove unnecessary "New Game" buttons.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Menu), nameof(Menu.MakeSavefileNewMenu))]
    private static void Menu_MakeSavefileNewMenu_Postfix(UIDialog __result)
    {
        Func<UIElement, bool> predicate = e =>
            e.TryCast<UIButton>() is { } button
            && button.textFn.Invoke() != Localization.Get("ui_newGame")
            && button.textFn.Invoke() != Localization.Get("ui_back");
        var result = __result.rootElement.subElements.RemoveAll(predicate);
        MelonLogger.Msg($"Removed {result} buttons...");
    }


    /// <summary>
    /// Handles post-connection UI changes.
    /// </summary>
    /// <param name="resultTask">A task representing whether the connection to Archipelago was successful.</param>
    private static void OnConnection(Task<bool> resultTask)
    {
        try
        {
            string connectText;
            if (resultTask.Result)
            {
                EnableLoadGameButton();
                connectText = GetSuccessfulConnectionStatus();
            }
            else
            {
                DisableLoadGameButton();
                connectText = GetFailedConnectionStatus();
            }

            Melon<PipArchMod>.Logger.Msg(connectText);
            var textFn = () => connectText;
            _connectionStatus.textFn = textFn;
        }
        catch (Exception ex)
        {
            Melon<PipArchMod>.Logger.Error($"Exception handling post-connection: {ex}");
        }
    }

    private static void EnableLoadGameButton()
    {
        var loadGameTextFn = () => "Load Game";
        _loadGameButton.SetText(loadGameTextFn);
        _loadGameButton.canPress = true;
    }

    private static void DisableLoadGameButton()
    {
        var loadGameTextFn = () => "---";
        _loadGameButton.SetText(loadGameTextFn);
        _loadGameButton.canPress = false;
    }

    private static string GetSuccessfulConnectionStatus()
    {
        var slot = Global.State.Session.ConnectionInfo.Slot;
        var slotName = Global.State.Session.Players.GetPlayerName(slot);
        return $"Connected: {ModSettings.Host.Value}:{ModSettings.Port.Value}\nSlot: {slotName}";
    }

    private static string GetFailedConnectionStatus()
    {
        return $"Failed to connect: {ModSettings.Host.Value}:{ModSettings.Port.Value}\n---";
    }
}
