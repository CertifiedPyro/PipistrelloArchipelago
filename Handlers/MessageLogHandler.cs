using System.Text;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using MelonLoader;

namespace PipistrelloArchipelago.Handlers;

internal static class MessageLogHandler
{
    public static void Process(LogMessage message)
    {
        if (message is not ChatLogMessage)
        {
            return;
        }
        
        var builder = new StringBuilder();
        foreach (var part in message.Parts)
        {
            var color = part.PaletteColor?.ToString();
            var text = part.Text.Replace("[", "(").Replace("]", ")");
            MelonLogger.Msg($"Message part: {color} | {text}");
            
            var messagePart = color == null ? text : $"[c:{color}|{text}]";
            builder.Append(messagePart);
        }

        var queuedMessage = builder.ToString();
        Global.State.Messages.Enqueue(queuedMessage);
    }
}