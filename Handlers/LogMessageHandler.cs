using System.Text;
using Archipelago.MultiClient.Net.MessageLog.Messages;

namespace PipistrelloArchipelago.Handlers;

/// <summary>
/// Handler for receiving messages from Archipelago.
/// </summary>
internal static class LogMessageHandler
{
    /// <summary>
    /// Processes a received message.
    /// </summary>
    public static void Process(LogMessage message)
    {
        if (message is ChatLogMessage)
        {
            if (!ModSettings.MessagesChatAllowed.Value)
            {
                return;
            }
        }
        else if (message is ServerChatLogMessage)
        {
            if (!ModSettings.MessagesServerAllowed.Value)
            {
                return;
            }
        }
        else if (message is HintItemSendLogMessage hintItemSendLogMessage)
        {
            if (hintItemSendLogMessage is { IsRelatedToActivePlayer: false })
            {
                return;
            }
        }
        else if (message is CountdownLogMessage countdownLogMessage)
        {
            // Set a limit for queueing countdown messages, since they take priority over regular messages.
            if (countdownLogMessage.RemainingSeconds > 120)
            {
                return;
            }
        }
        else if (message is not (GoalLogMessage or ReleaseLogMessage))
        {
            return;
        }

        var builder = new StringBuilder();
        foreach (var part in message.Parts)
        {
            // Sanitize input.
            var text = part.Text.Replace("[", "(").Replace("]", ")").Replace("\"", "'");

            var color = Utils.GetTextColor(part.PaletteColor.ToString());
            var messagePart = color == null ? text : $"[c:{color}|{text}]";

            builder.Append(messagePart);
        }

        var queuedMessage = builder.ToString();
        if (message is CountdownLogMessage)
        {
            Global.State.CountdownMessages.Enqueue(queuedMessage);
        }
        else
        {
            Global.State.Messages.Enqueue(queuedMessage);
        }
    }
}
