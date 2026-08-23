using System.Text;
using Archipelago.MultiClient.Net.Colors;
using Archipelago.MultiClient.Net.MessageLog.Messages;

namespace PipistrelloArchipelago.Handlers;

internal static class MessageLogHandler
{
    public static void Process(LogMessage message)
    {
        if (message is HintItemSendLogMessage { IsRelatedToActivePlayer: false })
        {
            return;
        }

        if (message is not (ChatLogMessage or GoalLogMessage or JoinLogMessage or LeaveLogMessage
            or ReleaseLogMessage or ServerChatLogMessage))
        {
            return;
        }

        var builder = new StringBuilder();
        foreach (var part in message.Parts)
        {
            var text = part.Text.Replace("[", "(").Replace("]", ")").Replace("\"", "'");
            var color = GetTextColor(part.PaletteColor);
            var messagePart = color == null ? text : $"[c:{color}|{text}]";
            builder.Append(messagePart);
        }

        var queuedMessage = $"[fast|{builder}]";
        Global.State.Messages.Enqueue(queuedMessage);
    }

    private static string GetTextColor(PaletteColor? color)
    {
        return color switch
        {
            PaletteColor.White => null,
            PaletteColor.Black => "gray",
            PaletteColor.Red => "red",
            PaletteColor.Green => "green",
            PaletteColor.Blue => "blue",
            PaletteColor.Cyan => "cyan",
            PaletteColor.Magenta => "player",
            PaletteColor.Yellow => "yellow",
            PaletteColor.SlateBlue => "refine",
            PaletteColor.Salmon => "lightPink",
            PaletteColor.Plum => "blueprint",
            _ => null
        };
    }
}