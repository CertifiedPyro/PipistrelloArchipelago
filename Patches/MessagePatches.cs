using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using UnityEngine;

namespace PipistrelloArchipelago.Patches;

[HarmonyPatch]
internal static class MessagePatches
{
    private const int TextShowTimeMs = 3000;

    private static MessageState _messageState;
    private static float _elapsedTextTimeSeconds;
    private static bool _ignoreClick;

    [HarmonyPatch(typeof(ObjectPlayer), nameof(ObjectPlayer.Process))]
    [HarmonyPrefix]
    public static void Start()
    {
        if (!Global.State.SaveFileLoaded)
        {
            return;
        }

        if (_messageState != MessageState.None)
        {
            if (_messageState == MessageState.Finished &&
                Global.Director.dialoguePanel == null &&
                Global.State.Messages.TryDequeue(out _))
            {
                _messageState = MessageState.None;
                _elapsedTextTimeSeconds = 0;
                _ignoreClick = false;
            }

            return;
        }

        if (Global.State.Messages.TryPeek(out var message) &&
            CanContinueShowingMessage() &&
            Global.Director.dialoguePanel == null)
        {
            _messageState = MessageState.Building;
            Global.Director.player.ExecuteCodeInThread($"say(\"{message}\")", nameof(MessagePatches));
        }
    }

    [HarmonyPatch(typeof(DialoguePanel), nameof(DialoguePanel.Process))]
    [HarmonyPrefix]
    public static void DialoguePanelPatch(DialoguePanel __instance)
    {
        if (_messageState == MessageState.None)
        {
            return;
        }

        if (__instance.currentTextScroll >= __instance.textScrolls.Count)
        {
            _messageState = MessageState.Finished;
            return;
        }

        _messageState = MessageState.Showing;
        _ignoreClick = true;

        var currentTextScroll = __instance.textScrolls[__instance.currentTextScroll];
        if (!CanContinueShowingMessage())
        {
            currentTextScroll.ended = true;
            _messageState = MessageState.None;
            _elapsedTextTimeSeconds = 0;
            _ignoreClick = false;
            return;
        }

        if (currentTextScroll.isWaitingClick && _elapsedTextTimeSeconds > TextShowTimeMs / 1000f)
        {
            _elapsedTextTimeSeconds = 0;
            _ignoreClick = false;
            currentTextScroll.AcceptClick();
            _ignoreClick = true;
            return;
        }

        _elapsedTextTimeSeconds += Time.deltaTime;
    }

    [HarmonyPatch(typeof(TextScroll), nameof(TextScroll.AcceptClick))]
    [HarmonyPrefix]
    public static bool AcceptClickPatch()
    {
        return !_ignoreClick;
    }

    [HarmonyPatch(typeof(TextRenderer), nameof(TextRenderer.BuildRecursive))]
    [HarmonyPrefix]
    public static void BuildRecursivePatch(TextRenderer.Section section, ref Color currentColor)
    {
        if (_messageState != MessageState.Building)
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
        return Global.Director.uiDialog == null && !Global.Director.IsPlayerDead();
    }
}

internal enum MessageState
{
    None = 0,
    Building = 1,
    Showing = 2,
    Finished = 3
}