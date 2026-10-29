using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._ClawCommand.GangConversion.Components;

/// <summary>
/// Marks an item (the gang spray tagger) that paints turf decals in the wielder's gang colour, claiming
/// the area for their gang. Tagging over a rival's tag erases it first.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GangTaggerComponent : Component
{
    /// <summary>
    /// Which gang this tagger paints for. Set when the tagger is handed out; -1 means it hasn't been claimed yet.
    /// </summary>
    [DataField]
    public int GangId = -1;

    /// <summary>
    /// The colour the tagger paints with. Matches the owning gang's colour.
    /// </summary>
    [DataField]
    public Color Color = Color.White;

    /// <summary>
    /// The decal prototype drawn by this tagger.
    /// </summary>
    [DataField]
    public string DecalId = "GangTagDecal";

    /// <summary>
    /// How far around the click point existing tags (of any gang) are considered "the same spot" for the
    /// purposes of overwriting a rival's tag or blocking a redundant re-tag of your own.
    /// </summary>
    [DataField]
    public float ClaimRange = 0.75f;

    [DataField]
    public SoundSpecifier? UseSound;
}
