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
        // TODO: Handle countdown message
        if (message is ChatLogMessage)
        {
            if (!ModSettings.AllowChatMessages.Value)
            {
                return;
            }
        }
        else if (message is ServerChatLogMessage)
        {
            if (!ModSettings.AllowServerMessages.Value)
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

        Global.State.Messages.Enqueue(builder.ToString());
    }
}
