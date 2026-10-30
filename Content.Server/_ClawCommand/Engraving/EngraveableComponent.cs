namespace Content.Server._ClawCommand.Engraving;

/// <summary>
/// Allows an item's description to be extended with a one-time engraving.
/// </summary>
[RegisterComponent, Access(typeof(EngraveableSystem))]
public sealed partial class EngraveableComponent : Component
{
    /// <summary>
    /// The engraved message, empty while nothing has been engraved.
    /// </summary>
    [DataField]
    public string EngravedMessage = string.Empty;

    /// <summary>
    /// The inspect text to use when there is no engraving
    /// </summary>
    [DataField]
    public LocId NoEngravingText = "engraving-no-message";

    /// <summary>
    /// The message to use when successfully engraving the item
    /// </summary>
    [DataField]
    public LocId EngraveSuccessMessage = "engraving-succeed";

    /// <summary>
    /// The inspect text to use when there is an engraving. The message will be shown separately afterwards.
    /// </summary>
    [DataField]
    public LocId HasEngravingText = "engraving-has-message";

    /// <summary>
    /// Maximum length of an engraving.
    /// </summary>
    [DataField]
    public int MaxLength = 100;
}
