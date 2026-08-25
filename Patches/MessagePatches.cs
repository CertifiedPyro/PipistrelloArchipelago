using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using UnityEngine;
using Sprite = Il2CppUtil.Sprite;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patches to handle queued messages.
/// </summary>
[HarmonyPatch]
internal static class MessagePatches
{
    private const int TextShowTimeMs = 3500;

    private static readonly HashSet<ObjectPlayer.State> InvalidStates =
    [
        ObjectPlayer.State.AcquiringItem,
        ObjectPlayer.State.AcquiringMegaBattery,
        ObjectPlayer.State.AuntieFinish,
        ObjectPlayer.State.AuntieTalk,
        ObjectPlayer.State.Cutscene
    ];

    private static InternalState _state = new();

    /// <summary>
    /// Determines if a queued message should be shown in a dialogue panel.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(ObjectPlayer), nameof(ObjectPlayer.Process))]
    private static void ObjectPlayer_Process_Prefix()
    {
        if (!Global.State.SaveFileLoaded || _state.MessageState != MessageState.None)
        {
            return;
        }

        if (Global.State.Messages.TryPeek(out var message)
            && CanContinueShowingMessage()
            && Global.Director.dialoguePanel == null)
        {
            _state.MessageState = MessageState.Building;

            message = $"[fast|{message}]";
            Global.Director.player.ExecuteCodeInThread($"say(\"{message}\")", nameof(MessagePatches));
        }
    }

    /// <summary>
    /// Handles the shown message.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(DialoguePanel), nameof(DialoguePanel.Process))]
    private static void DialoguePanel_Process_Prefix(DialoguePanel __instance)
    {
        if (_state.MessageState == MessageState.None)
        {
            return;
        }

        // Handle dialogue that is over.
        if (__instance.currentTextScroll >= __instance.textScrolls.Count)
        {
            Global.State.Messages.TryDequeue(out _);
            _state = new InternalState();
            return;
        }

        _state.MessageState = MessageState.Showing;
        _state.IgnoreClick = true;

        // Close dialogue panel early if necessary (including if dialogue is added midway through).
        var currentTextScroll = __instance.textScrolls[__instance.currentTextScroll];
        if (!CanContinueShowingMessage() || __instance.textScrolls.Count > 1)
        {
            currentTextScroll.ended = true;
            _state = new InternalState();
            return;
        }

        // Advance or close text once timer has passed.
        if (currentTextScroll.isWaitingClick && _state.ElapsedTextTimeSeconds > TextShowTimeMs / 1000f)
        {
            _state.ElapsedTextTimeSeconds = 0;
            _state.IgnoreClick = false;
            currentTextScroll.AcceptClick();
            _state.IgnoreClick = true;
            return;
        }

        // Advance timer when text section is fully shown.
        if (currentTextScroll.isWaitingClick)
        {
            _state.ElapsedTextTimeSeconds += Time.deltaTime;
        }
    }

    /// <summary>
    /// Prevents message from being skipped.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(TextScroll), nameof(TextScroll.AcceptClick))]
    private static bool TextScroll_AcceptClick_Prefix()
    {
        return !_state.IgnoreClick;
    }

    /// <summary>
    /// Hides the dialogue advance arrow.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(SpriteManager), nameof(SpriteManager.GetSprite))]
    private static void SpriteManager_GetSprite_Postfix(string sprId, ref Sprite __result)
    {
        if (_state.MessageState != MessageState.None && sprId == "ui/dialogueAdvanceArrow")
        {
            __result = SpriteManager.nullSprite;
        }
    }

    /// <summary>
    /// Allows interactables to work while a message is showing.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(ObjectPlayer), nameof(ObjectPlayer.HandleInputInteract))]
    private static void ObjectPlayer_HandleInputInteract_Postfix(ref bool __result)
    {
        var player = Global.Director.player;
        if (player.interactableObject != null && player.currentInput.jump)
        {
            __result = true;
            player.interactableObject.OnInteract();
        }
    }

    /// <summary>
    /// Converts game colors to Archipelago palette colors.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(TextRenderer), nameof(TextRenderer.BuildRecursive))]
    private static void TextRenderer_BuildRecursive_Prefix(TextRenderer.Section section, ref Color currentColor)
    {
        if (_state.MessageState != MessageState.Building)
        {
            return;
        }

        Archipelago.MultiClient.Net.Models.Color? newColor = null;
        if (currentColor.Equals(Il2CppPipistrello.Global.colorTextRed))
        {
            newColor = Archipelago.MultiClient.Net.Models.Color.Red;
        }
        else if (currentColor.Equals(Il2CppPipistrello.Global.colorTextGreen))
        {
            newColor = Archipelago.MultiClient.Net.Models.Color.Green;
        }
        /* Pure blue is too hard to read, so use the game's blue color instead. */
        // else if (currentColor.Equals(Il2CppPipistrello.Global.colorTextBlue))
        // {
        //     newColor = Archipelago.MultiClient.Net.Models.Color.Blue;
        // }
        else if (currentColor.Equals(Il2CppPipistrello.Global.colorTextCyan))
        {
            newColor = Archipelago.MultiClient.Net.Models.Color.Cyan;
        }
        else if (currentColor.Equals(Il2CppPipistrello.Global.colorTextPlayer))
        {
            newColor = Archipelago.MultiClient.Net.Models.Color.Magenta;
        }
        else if (currentColor.Equals(Il2CppPipistrello.Global.colorTextYellow))
        {
            newColor = Archipelago.MultiClient.Net.Models.Color.Yellow;
        }
        else if (currentColor.Equals(Il2CppPipistrello.Global.colorTextRefinement))
        {
            newColor = Archipelago.MultiClient.Net.Models.Color.SlateBlue;
        }
        else if (currentColor.Equals(Il2CppPipistrello.Global.colorTextLightPink))
        {
            newColor = Archipelago.MultiClient.Net.Models.Color.Salmon;
        }
        else if (currentColor.Equals(Il2CppPipistrello.Global.colorTextBlueprint))
        {
            newColor = Archipelago.MultiClient.Net.Models.Color.Plum;
        }

        if (newColor != null)
        {
            currentColor = new Color(newColor.Value.R / 255f, newColor.Value.G / 255f, newColor.Value.B / 255f);
        }
    }

    private static bool CanContinueShowingMessage()
    {
        return !InvalidStates.Contains(Global.Director.player.state)
               && Global.Director.uiDialog == null
               && !Global.Director.IsPlayerDead()
               && !Global.Director.transitionActive;
    }

    private class InternalState
    {
        internal MessageState MessageState;
        internal float ElapsedTextTimeSeconds;
        internal bool IgnoreClick;
    }

    private enum MessageState
    {
        None = 0,
        Building = 1,
        Showing = 2
    }
}
