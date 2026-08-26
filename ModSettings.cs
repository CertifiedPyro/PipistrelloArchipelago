using MelonLoader;
using MelonLoader.Preferences;
using Tomlet;
using Tomlet.Models;

namespace PipistrelloArchipelago;

internal static class ModSettings
{
    public static MelonPreferences_Entry<string> Host;
    public static MelonPreferences_Entry<int> Port;
    public static MelonPreferences_Entry<string> SlotName;
    public static MelonPreferences_Entry<string> Password;
    public static MelonPreferences_Entry<bool> DeathLink;

    public static MelonPreferences_Entry<ItemMessagesSetting> MessagesItemReceivedAllowed;
    public static MelonPreferences_Entry<bool> MessagesChatAllowed;
    public static MelonPreferences_Entry<bool> MessagesServerAllowed;

    internal static void Initialize()
    {
        // Initialize MelonLoader settings.
        var category = MelonPreferences.CreateCategory("Archipelago");
        Host = category.CreateEntry("Host", "archipelago.gg");
        Port = category.CreateEntry("Port", 0);
        SlotName = category.CreateEntry("Slot Name", "");
        Password = category.CreateEntry("Password", "", description: "WARNING: Password is not hidden");

        const string deathLinkDescription =
            """
            Once connected, toggles death link if it was originally enabled.
            Note: You cannot disable death link in race mode.
            """;
        DeathLink = category.CreateEntry(
            "Death Link",
            false,
            description: deathLinkDescription,
            validator: new DeathLinkValidator());

        // Newer versions of Tomlet cannot serialize enums with [Flags] because it calls Enum.GetName(type, value).
        TomletMain.RegisterMapper<ItemMessagesSetting>(value => new TomlString(value.ToString()), null);

        const string messagesItemReceivedAllowedDescription =
            $"""
             Sets which received items are displayed in-game as messages.
             Note: Toggling "{nameof(ItemMessagesSetting.Useful)}" on forces "{nameof(ItemMessagesSetting.Progression)}" on.
             """;
        MessagesItemReceivedAllowed = category.CreateEntry(
            "Item Receive Messages",
            ItemMessagesSetting.Progression
            | ItemMessagesSetting.Useful
            | ItemMessagesSetting.Trap
            | ItemMessagesSetting.Filler,
            description: messagesItemReceivedAllowedDescription,
            validator: new AllowItemReceiveMessagesValidator());
        MessagesChatAllowed = category.CreateEntry("Chat Messages", true);
        MessagesServerAllowed = category.CreateEntry("Server Messages", true);
        category.CreateEntry(
            "--- End of settings ---", true,
            description: "This exists to pad out the preferences manager in windowed mode. You can ignore this.");
    }
}

[Flags]
internal enum ItemMessagesSetting
{
    None = 0,
    Progression = 1,
    Useful = 2,
    Trap = 4,
    Filler = 8
}

internal class DeathLinkValidator : ValueValidator
{
    public override bool IsValid(object value)
    {
        return value.Equals(EnsureValid(value));
    }

    public override object EnsureValid(object value)
    {
        if (Global.State.DeathLinkService != null)
        {
            // If race mode is toggled on, death link must stay on if enabled.
            if (Global.State.RaceMode)
            {
                return true;
            }

            // Death link can be toggled on/off if it was originally enabled.
            return value;
        }

        // Otherwise, setting must stay false.
        return false;
    }
}

internal class AllowItemReceiveMessagesValidator : ValueValidator
{
    public override bool IsValid(object value)
    {
        return value is ItemMessagesSetting;
    }

    public override object EnsureValid(object value)
    {
        var newValue = (ItemMessagesSetting)value;
        if (newValue.HasFlag(ItemMessagesSetting.Useful))
        {
            newValue |= ItemMessagesSetting.Progression;
        }

        return newValue;
    }
}
