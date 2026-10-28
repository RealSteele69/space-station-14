using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Audio;

namespace Content.Server._ClawCommand.Seasonal.Halloween;

/// <summary>
/// Marks an EntityStorage (locker/closet) as spooky-season booby-trapped: the first time someone
/// opens it, <see cref="SpookyClosetTrapSystem"/> rolls one jump-scare effect from <see cref="Effects"/>
/// and spawns it on top of the closet, then removes this component.
/// </summary>
[RegisterComponent]
public sealed partial class SpookyClosetTrapComponent : Component
{
    /// <summary>
    /// Weighted table of jump-scare effects to roll from. Use a GroupSelector so the odds can be
    /// skewed (e.g. the harmless skeleton should come up far more often than the hostile spirit).
    /// </summary>
    [DataField]
    public EntityTableSelector? Effects;

    /// <summary>
    /// Sound played for everyone near the closet the instant it's triggered, before the effect
    /// entity's own sound (if any) has a chance to play.
    /// </summary>
    [DataField]
    public SoundSpecifier? JumpscareSound = new SoundCollectionSpecifier("HalloweenJumpscare");
}
