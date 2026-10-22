using Content.Shared.Bible.Components;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.CosmicCult.Components;

[RegisterComponent]
public sealed partial class CosmicPurifiableComponent : Component
{
    [DataField]
    public EntProtoId PurgeVFX = "CleanseEffectVFX";

    [DataField]
    public SoundSpecifier PurgeSound = new SoundPathSpecifier("/Audio/_DV/CosmicCult/cleanse_deconversion.ogg");

    [DataField]
    public TimeSpan CleanseTime = TimeSpan.FromSeconds(25); // Claw Command: 35 -> 25

    [DataField]
    public TimeSpan CleanseTimeChaplain = TimeSpan.FromSeconds(12); // Claw Command: 20 -> 12

    ///<summary>
    /// If true, only an entity with a BibleUserComponent can purge this entity
    ///</summary>
    [DataField]
    public bool BibleUserRequired;
}
