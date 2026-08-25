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
        if (message is HintItemSendLogMessage { IsRelatedToActivePlayer: false })
        {
            return;
        }

        if (message is not (ChatLogMessage
            or GoalLogMessage
            or HintItemSendLogMessage
            or ReleaseLogMessage
            or ServerChatLogMessage))
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
