using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using MelonLoader;
using UnityEngine;

namespace PipistrelloArchipelago.Handlers;

[HarmonyPatch]
internal static class DisplayMessageHandler
{
    private const int TextShowTimeMs = 3000;

    private static long? _textStartTimeMs;
    private static bool _showingMessage;
    private static bool _messageShown;
    private static bool _ignoreClick;
    private static bool _buildingDialoguePanel;

    [HarmonyPatch(typeof(ObjectPlayer), nameof(ObjectPlayer.Process))]
    [HarmonyPrefix]
    public static void Start()
    {
        if (_showingMessage)
        {
            if (_messageShown && Global.Director.dialoguePanel == null && Global.State.Messages.TryDequeue(out _))
            {
                _textStartTimeMs = null;
                _showingMessage = false;
                _messageShown = false;
                _ignoreClick = false;
            }

            return;
        }

        // Only queue message if there isn't dialogue showing already.
        if (Global.Director.dialoguePanel == null && Global.State.Messages.TryPeek(out var message))
        {
            _showingMessage = true;
            Global.Director.player.ExecuteCodeInThread($"say(\"{message}\")", nameof(DisplayMessageHandler));
        }
    }

    [HarmonyPatch(typeof(DialoguePanel), nameof(DialoguePanel.Process))]
    [HarmonyPrefix]
    public static void Prefix(DialoguePanel __instance)
    {
        if (!_showingMessage)
        {
            return;
        }

        _messageShown = true;
        _ignoreClick = true;
        if (__instance.currentTextScroll >= __instance.textScrolls.Count)
        {
            return;
        }

        var currentTextScroll = __instance.textScrolls[__instance.currentTextScroll];
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (_textStartTimeMs != null && currentTime - _textStartTimeMs > TextShowTimeMs)
        {
            _textStartTimeMs = null;
            _ignoreClick = false;
            currentTextScroll.AcceptClick();
            _ignoreClick = true;
            return;
        }

        if (_textStartTimeMs == null)
        {
            _textStartTimeMs = currentTime;
        }
    }

    [HarmonyPatch(typeof(DialoguePanel), nameof(DialoguePanel.InjectText))]
    [HarmonyPrefix]
    public static void StartChoicesPrefix()
    {
        MelonLogger.Msg("Set _inDialoguePanel to true");
        _buildingDialoguePanel = true;
    }

    [HarmonyPatch(typeof(DialoguePanel), nameof(DialoguePanel.InjectText))]
    [HarmonyPostfix]
    public static void EndChoicesPrefix()
    {
        MelonLogger.Msg("Set _inDialoguePanel to false");
        _buildingDialoguePanel = false;
    }

    [HarmonyPatch(typeof(TextRenderer), nameof(TextRenderer.BuildRecursive))]
    [HarmonyPrefix]
    public static void BuildRecursivePatch(TextRenderer.Section section, ref Color currentColor)
    {
        // MelonLogger.Msg($"TextRenderer.BuildRecursive: {section.GetText()} {currentColor}");
        if (!_showingMessage || !_buildingDialoguePanel)
        {
            return;
        }

        MelonLogger.Msg($"current color: {currentColor}");
        Archipelago.MultiClient.Net.Models.Color? newColor = null;
        if (currentColor.Equals(Il2CppPipistrello.Global.colorTextRed))
        {
            newColor = Archipelago.MultiClient.Net.Models.Color.Red;
        }
        else if (currentColor.Equals(Il2CppPipistrello.Global.colorTextGreen))
        {
            newColor = Archipelago.MultiClient.Net.Models.Color.Green;
        }
        else if (currentColor.Equals(Il2CppPipistrello.Global.colorTextBlue))
        {
            newColor = Archipelago.MultiClient.Net.Models.Color.Blue;
        }
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
            MelonLogger.Msg($"new color: {currentColor}");
        }
    }

    [HarmonyPatch(typeof(TextScroll), nameof(TextScroll.AcceptClick))]
    [HarmonyPrefix]
    public static bool AcceptClickPatch()
    {
        return !_ignoreClick;
    }
}