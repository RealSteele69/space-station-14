using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Blob.Components;

/// <summary>
/// Blob speech. Anything said aloud comes out as blob gibberish, while the Blobmind channel (:l) carries the plain words.
/// With <see cref="RadioOnly"/> nothing is said aloud at all and every message goes to the Blobmind (the observer camera).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BlobSpeakComponent : Component
{
    [DataField]
    public ProtoId<RadioChannelPrototype> Channel = "Blobmind";

    /// <summary>
    /// ReplacementAccent applied to speech said out loud.
    /// </summary>
    [DataField]
    public string Accent = "blob";

    [DataField]
    public bool RadioOnly;

    /// <summary>
    /// Hide entity name
    /// </summary>
    [DataField]
    public bool OverrideName;

    [DataField]
    public LocId Name = "speak-vv-blob";

    // What startup had to add, so shutdown (zombies being cured) only removes our own bits.
    [ViewVariables]
    public bool AddedReceiver;

    [ViewVariables]
    public bool AddedActiveRadio;

    [ViewVariables]
    public bool AddedChannel;
}
