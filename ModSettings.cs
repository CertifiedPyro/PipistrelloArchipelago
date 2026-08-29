using MelonLoader;
using MelonLoader.Preferences;
using MelonPrefManager.UI.InteractiveValues;
using Tomlet;
using Tomlet.Models;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;
using UniverseLib.UI.Models;

namespace PipistrelloArchipelago;

internal static class ModSettings
{
    public static MelonPreferences_Entry<string> Host;
    public static MelonPreferences_Entry<int> Port;
    public static MelonPreferences_Entry<string> SlotName;
    public static MelonPreferences_Entry<MaskedString> Password;
    public static MelonPreferences_Entry<bool> DeathLink;

    public static MelonPreferences_Entry<ItemMessagesSetting> MessagesItemReceivedAllowed;
    public static MelonPreferences_Entry<bool> MessagesChatAllowed;
    public static MelonPreferences_Entry<bool> MessagesServerAllowed;

    internal static void Initialize()
    {
        // Initialize MelonLoader settings.
        InteractiveValue.RegisterIValueType<InteractiveMaskedString>();

        var category = MelonPreferences.CreateCategory("Archipelago");
        Host = category.CreateEntry("Host", "archipelago.gg");
        Port = category.CreateEntry("Port", 0);
        SlotName = category.CreateEntry("Slot Name", "");
        Password = category.CreateEntry("Password", new MaskedString(""));

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
            description: "Ignore this: It just exists to pad out the preferences manager in windowed mode.");
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
    public override bool IsValid(object value) => value.Equals(EnsureValid(value));

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
    public override bool IsValid(object value) => value is ItemMessagesSetting;

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

/// <summary>
/// An <see cref="InteractiveValue" /> that's mostly copied from <see cref="InteractiveString" />,
/// with some changes to support <see cref="MaskedString" />.
/// </summary>
public class InteractiveMaskedString(object value, Type valueType) : InteractiveValue(value, valueType)
{
    private InputFieldRef _valueInput;
    private GameObject _hiddenObj;
    private Text _placeholderText;

    public override bool SupportsType(Type type) => type == typeof(MaskedString);

    public override void RefreshUIForValue()
    {
        if (!_hiddenObj.gameObject.activeSelf)
        {
            _hiddenObj.gameObject.SetActive(true);
        }

        var maskedString = (MaskedString)Value;
        if (!string.IsNullOrEmpty(maskedString.ToString()))
        {
            var toString = maskedString.ToString();
            if (toString.Length > 15000)
            {
                toString = toString[..15000];
            }

            _valueInput.Text = toString;
            _placeholderText.text = toString;
        }
        else
        {
            var s = Value == null ? "null" : "empty";
            _valueInput.Text = "";
            _placeholderText.text = s;
        }
    }

    public override void ConstructUI(GameObject parent)
    {
        base.ConstructUI(parent);

        _hiddenObj = UIFactory.CreateLabel(mainContent, "HiddenLabel", "").gameObject;
        _hiddenObj.SetActive(false);
        var hiddenText = _hiddenObj.GetComponent<Text>();
        hiddenText.color = Color.clear;
        hiddenText.fontSize = 14;
        hiddenText.raycastTarget = false;
        hiddenText.supportRichText = false;
        var hiddenFitter = _hiddenObj.AddComponent<ContentSizeFitter>();
        hiddenFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        UIFactory.SetLayoutElement(_hiddenObj, minHeight: 25, flexibleHeight: 500, minWidth: 250, flexibleWidth: 9000);
        UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(_hiddenObj, true, true, true, true);

        _valueInput = UIFactory.CreateInputField(_hiddenObj, "StringInputField", "...");
        // This is the important line that makes the password masked.
        _valueInput.Component.contentType = InputField.ContentType.Password;
        UIFactory.SetLayoutElement(_valueInput.Component.gameObject, 120, 25, 5000, 5000);

        _valueInput.Component.lineType = InputField.LineType.MultiLineNewline;

        _placeholderText = _valueInput.Component.placeholder.GetComponent<Text>();

        _placeholderText.supportRichText = false;
        _valueInput.Component.textComponent.supportRichText = false;

        _valueInput.OnValueChanged += val =>
        {
            hiddenText.text = val ?? "";
            LayoutRebuilder.ForceRebuildLayoutImmediate(Owner.ContentRect);
            SetValueFromInput();
        };

        RefreshUIForValue();
    }

    private void SetValueFromInput()
    {
        Value = new MaskedString(_valueInput.Text);
        Owner.SetValueFromIValue();
    }
}

public readonly struct MaskedString(string value)
{
    private string Value { get; } = value;
    public override string ToString() => Value;
}
