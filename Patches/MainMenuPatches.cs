using Archipelago.MultiClient.Net;
using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using MelonLoader;
using UnityEngine;

namespace PipistrelloArchipelago.Patches;

[HarmonyPatch]
public static class MainMenuPatches
{
    private static UILabel _connectionStatus = null;
    private static UIButton _loadGameButton = null;

    /// <summary>
    /// Add extra buttons/labels to main menu for Archipelago connections.
    /// </summary>
    [HarmonyPatch(typeof(Menu), nameof(Menu.MakeMainMenu))]
    [HarmonyPostfix]
    public static void MainMenuPatch(UIDialog __result)
    {
        // Force reconnection every time main menu is reached.
        ArchipelagoSession session = null;

        var elements = __result.rootElement.subElements;
        var loadGameButton = elements[0].Cast<UIButton>();

        // Add connect button at start of list.
        var connectButton = loadGameButton.MemberwiseClone().Cast<UIButton>();
        var connectButtonTextFn = () => "Connect";
        connectButton.SetText(connectButtonTextFn);
        elements.Insert(0, connectButton);

        // Add connection status label.
        var labelText = session == null
            ? "<Waiting for connection>\n---"
            : GetSuccessfulConnectionStatus();
        var labelTextFn = () => labelText;
        var label = new UILabel(labelTextFn)
        {
            labelHalign = TextRenderer.Halign.Center,
            forceMinWidth = new Il2CppSystem.Nullable<float>(130f),
            marginTop = -4f,
            marginBottom = -4f
        };
        _connectionStatus = label;

        // Wrap connection status label in a panel.
        var panel = new UIPanel()
        {
            childrenCenterH = true
        };
        panel.subElements.Add(label);
        elements.Insert(0, panel);

        // Enable/disable load game button.
        _loadGameButton = loadGameButton;
        if (session == null)
        {
            DisableLoadGameButton();
        }
        else
        {
            EnableLoadGameButton();
        }

        // Fix offset and assign every row to its correct index.
        __result.rootElement.offset = new Vector3(0, 24, 0);
        for (var i = 1; i < elements.Count; i++)
        {
            elements[i].cellY = i;
        }
    }

    /// <summary>
    /// Connect to Archipelago when "Connect" button is pressed.
    /// </summary>
    [HarmonyPatch(typeof(UIElement), nameof(UIElement.PerformPress))]
    [HarmonyPrefix]
    public static bool UIElementPressPatch(UIElement __instance)
    {
        // Only patch the "Connect" button.
        var connectButton = __instance.TryCast<UIButton>();
        if (connectButton == null || connectButton.textFn.Invoke() != "Connect")
        {
            return true;
        }

        var labelText = () => "Connecting...\n---";
        _connectionStatus.textFn = labelText;

        ArchipelagoHelper.ConnectAsync().ContinueWith(t => OnConnection(t)).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// Disable spinning 3D console.
    /// </summary>
    [HarmonyPatch(typeof(TitleScreen), nameof(TitleScreen.CanBeginAttractMode))]
    [HarmonyPostfix]
    public static void DisableSpinningConsolePatch(ref bool __result)
    {
        __result = false;
    }

    /// <summary>
    /// Modify save file menu to prevent loading non-Archipelago saves.
    /// </summary>
    [HarmonyPatch(typeof(Menu), nameof(Menu.MakeSavefileLoadMenu))]
    [HarmonyPostfix]
    public static void SavefileLoadMenuPatch(UIDialog __result)
    {
        // Get record corresponding with save file menu.
        Func<UIElement, bool> predicate = (e) => e.TryCast<UISaveFile>() != null;
        var saveFile = __result.FindElementInAllSubDialogs(predicate).Cast<UISaveFile>();
        // saveFile.record is null for some reason, so fetch from Director.
        var record = __result.director.savefileRecords[saveFile.savefileIndex];

        // Check that save file has Archipelago flag.
        if (record?.flags?.ContainsKey(Constants.FLAG_ARCHIPELAGO) == true)
        {
            return;
        }

        // Get the first Load Game button.
        predicate = (e) => e.TryCast<UIButton>() != null;
        var button = __result.FindElementInAllSubDialogs(predicate).Cast<UIButton>();

        // Replace "Load Game" button to avoid loading non-Archipelago saves.
        var textFn = () => "❌ Non-Archipelago save ❌";
        button.SetText(textFn);
        button.canPress = false;
    }


    /// <summary>
    /// Handles post-Archipelago connection UI changes.
    /// </summary>
    /// <param name="resultTask">A task representing whether the connection to Archipelago was successful.</param>
    private static void OnConnection(Task<bool> resultTask)
    {
        try
        {
            var connectText = "";
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
