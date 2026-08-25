using System.Text;
using Archipelago.MultiClient.Net.Colors;
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
            or HintItemSendLogMessage
            or GoalLogMessage
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

            // Convert palette color to a color the game understands.
            // This will get converted back to the palette color later.
            var color = GetTextColor(part.PaletteColor);
            var messagePart = color == null ? text : $"[c:{color}|{text}]";

            builder.Append(messagePart);
        }

        Global.State.Messages.Enqueue(builder.ToString());
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
