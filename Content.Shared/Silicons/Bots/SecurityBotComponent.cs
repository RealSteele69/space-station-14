using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Silicons.Bots;

/// <summary>
/// An autonomous security bot that patrols for wanted/hostile crew (per their networked
/// criminal status icon), stuns them with an intrinsic disabler, and zip-ties them once downed.
/// Emagging it drops all checks and makes it treat everyone as wanted.
/// </summary>
[RegisterComponent]
[Access(typeof(SecurityBotSystem))]
public sealed partial class SecurityBotComponent : Component
{
    /// <summary>
    /// Arrest anyone with no ID card on them, regardless of criminal status.
    /// </summary>
    [DataField]
    public bool CheckId;

    /// <summary>
    /// Arrest anyone carrying contraband they aren't authorized to have.
    /// </summary>
    [DataField]
    public bool CheckWeapons;

    /// <summary>
    /// Arrest anyone whose criminal record status icon is Wanted or Hostile.
    /// </summary>
    [DataField]
    public bool CheckRecords = true;

    /// <summary>
    /// Whether to zip-tie downed targets. If false the bot only stuns and moves on
    /// (a "warning" mode), leaving the restraining to crew.
    /// </summary>
    [DataField]
    public bool HandcuffTargets = true;

    /// <summary>
    /// Whether to announce arrests over the security channel.
    /// </summary>
    [DataField]
    public bool DeclareArrests = true;

    /// <summary>
    /// Entity prototype spawned momentarily to perform a cuffing - never actually given to
    /// anyone, just a vessel for the reusable (RemoveOnUse: false) Handcuff component.
    /// </summary>
    [DataField]
    public EntProtoId CuffToolPrototype = "SecurityBotCuffTool";

    [DataField]
    public SoundSpecifier CuffSound = new SoundPathSpecifier("/Audio/Items/Handcuffs/ziptie_end.ogg");

    /// <summary>
    /// The last entity this bot decided was wanted and chased/announced. Used to avoid
    /// re-announcing the same perp every tick while hunting them, and to avoid instantly
    /// re-engaging someone who just escaped.
    /// </summary>
    [DataField]
    public EntityUid? LastPerp;

    /// <summary>
    /// How long after losing a perp before the bot will target them again.
    /// </summary>
    [DataField]
    public TimeSpan ReacquireCooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan LastPerpLostAt;
}
