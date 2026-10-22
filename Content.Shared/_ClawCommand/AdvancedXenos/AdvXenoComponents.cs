// Claw Command - advanced xenomorph gamemode. The behaviour follows the original DM source;
// the implementation is new SS14 code.

using Content.Shared.Alert;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.AdvancedXenos;

/// <summary>
/// Has a hive node. Almost everything is gated on <c>locate(/obj/item/organ/internal/xenos/hivenode)</c>:
/// facehuggers ignore you, resin doors open for you, resin melts at your touch, you may nest hosts,
/// you hear the hivemind and you see who is carrying an embryo.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AdvXenoHiveComponent : Component;

/// <summary>
/// The plasma vessel. Abilities spend plasma; it regenerates slowly, faster on weeds or in plasma gas,
/// and plasma reagent in the bloodstream tops it up.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AdvXenoPlasmaComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Plasma = 100;

    [DataField, AutoNetworkedField]
    public int MaxPlasma = 500;

    /// <summary>Flat gain per life tick (plasmavessel/handle_organ_proc_special: adjust_plasma(1)).</summary>
    [DataField]
    public int Regen = 1;

    /// <summary>Extra gain per tick while standing on weeds or in plasma gas, when there is nothing to heal.</summary>
    [DataField]
    public int WeedsPlasmaRate = 5;

    /// <summary>Health regained per damage group per tick on weeds. Divided by three unless resting.</summary>
    [DataField]
    public float WeedsHealRate = 1f;

    /// <summary>Plasma per tick gained from each unit of plasma reagent in the bloodstream (originally 4).</summary>
    [DataField]
    public int ReagentPlasmaRate = 4;

    [DataField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(2);

    [DataField, AutoPausedField]
    public TimeSpan NextTick;

    [DataField]
    public ProtoId<AlertPrototype> Alert = "AdvXenoPlasma";
}

/// <summary>
/// A grown xenomorph. Drives the "alien drone (12)" naming and the one-queen rule.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AdvXenoCasteComponent : Component
{
    /// <summary>Lower-case caste name used in the generated name, e.g. "drone".</summary>
    [DataField(required: true)]
    public string Caste = "creature";

    /// <summary>Queens are named "alien princess" while another queen is active.</summary>
    [DataField]
    public bool Queen;

    [DataField]
    public SoundSpecifier? DeathSound = new SoundPathSpecifier("/Audio/_ClawCommand/AdvancedXenos/Voice/hiss6.ogg");

    [DataField]
    public LocId DeathMessage = "adv-xeno-death-message";
}

/// <summary>
/// A larva: grows over time (faster on weeds) and then picks a caste.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AdvXenoLarvaComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Growth;

    [DataField]
    public int MaxGrowth = 200;

    [DataField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(2);

    [DataField, AutoPausedField]
    public TimeSpan NextTick;

    /// <summary>The "time to choose a caste" message has been sent.</summary>
    [DataField]
    public bool GrownNotified;

    [DataField]
    public ProtoId<AlertPrototype> Alert = "AdvXenoGrowth";
}

[Serializable, NetSerializable]
public enum AdvXenoLarvaVisuals : byte
{
    Stage,
    Hidden,
}

/// <summary>
/// Carries an alien embryo. Stages 0-5, then the chestburst.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AdvXenoEmbryoComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Stage;

    [DataField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(2);

    [DataField, AutoPausedField]
    public TimeSpan NextTick;

    /// <summary>Set once the chestburst has started; the host is gibbed when it elapses.</summary>
    [DataField, AutoPausedField]
    public TimeSpan? BurstAt;

    [DataField]
    public EntProtoId Larva = "MobAdvXenoLarvaGhostRole";
}

public enum AdvXenoHuggerState : byte
{
    Active,
    Idle,
    Dead,
    Impregnated,
}

[Serializable, NetSerializable]
public enum AdvXenoHuggerVisuals : byte
{
    State,
}

/// <summary>Sprite shown for a facehugger; Thrown only exists while it is in the air.</summary>
[Serializable, NetSerializable]
public enum AdvXenoHuggerSprite : byte
{
    Active,
    Idle,
    Dead,
    Impregnated,
    Thrown,
}

/// <summary>
/// The facehugger.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AdvXenoFacehuggerComponent : Component
{
    [DataField, AutoNetworkedField]
    public AdvXenoHuggerState State = AdvXenoHuggerState.Active;

    /// <summary>The proboscis has been removed: it still latches on but cannot implant.</summary>
    [DataField, AutoNetworkedField]
    public bool Sterile;

    /// <summary>Brute dealt when it leaps at someone, even if a helmet stops it.</summary>
    [DataField]
    public float Strength = 5f;

    /// <summary>After latching, it cannot latch again until this passes (MAX_IMPREGNATION_TIME).</summary>
    [DataField, AutoPausedField]
    public TimeSpan AttachCooldownEnd;

    [DataField, AutoPausedField]
    public TimeSpan? WakeAt;

    [DataField, AutoPausedField]
    public TimeSpan? ImpregnateAt;

    [DataField]
    public EntityUid? ImpregnateTarget;

    [DataField, AutoPausedField]
    public TimeSpan NextCrawl;

    /// <summary>Someone grabbed it with bare hands; the server latches it onto them next tick.</summary>
    [DataField]
    public EntityUid? PendingAttach;
}

public enum AdvXenoEggStatus : byte
{
    Growing,
    Grown,
    Bursting,
    Burst,
}

[Serializable, NetSerializable]
public enum AdvXenoEggVisuals : byte
{
    Status,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AdvXenoEggComponent : Component
{
    [DataField, AutoNetworkedField]
    public AdvXenoEggStatus Status = AdvXenoEggStatus.Growing;

    /// <summary>MIN_GROWTH_TIME / MAX_GROWTH_TIME (180-300 seconds).</summary>
    [DataField]
    public TimeSpan MinGrowTime = TimeSpan.FromSeconds(180);

    [DataField]
    public TimeSpan MaxGrowTime = TimeSpan.FromSeconds(300);

    [DataField, AutoPausedField]
    public TimeSpan? GrowAt;

    [DataField, AutoPausedField]
    public TimeSpan? BurstDoneAt;

    [DataField]
    public bool KillOnBurst;

    [DataField, AutoPausedField]
    public TimeSpan NextScan;

    /// <summary>A grown egg opens when a possible host comes this close.</summary>
    [DataField]
    public float TriggerRange = 3f;

    /// <summary>Damage at which the egg breaks open and the child dies (integrity 140, failure 40).</summary>
    [DataField]
    public float BreakDamage = 100f;

    [DataField]
    public EntProtoId Facehugger = "AdvXenoFacehugger";
}

/// <summary>Alien weeds. Heal xenos and refill plasma; spread from a node.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AdvXenoWeedsComponent : Component
{
    [DataField]
    public EntityUid? Node;
}

/// <summary>The "purple sac" that spreads weeds within <see cref="Range"/> tiles.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class AdvXenoWeedNodeComponent : Component
{
    /// <summary>NODERANGE.</summary>
    [DataField]
    public int Range = 3;

    [DataField]
    public EntProtoId Weeds = "AdvXenoWeeds";

    [DataField]
    public TimeSpan SpreadInterval = TimeSpan.FromSeconds(2);

    [DataField, AutoPausedField]
    public TimeSpan NextSpread;
}

/// <summary>Resin wall/membrane: a hive member stroking it melts it away.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AdvXenoResinComponent : Component;

/// <summary>Resin door: only opens for hive members.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AdvXenoResinDoorComponent : Component;

/// <summary>
/// Alien nest: only a hive member can secure someone in it, and a captive needs a long struggle to get out.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AdvXenoNestComponent : Component
{
    /// <summary>NEST_RESIST_TIME (1200 deciseconds).</summary>
    [DataField]
    public TimeSpan ResistTime = TimeSpan.FromSeconds(120);
}

/// <summary>Corrosive acid eating through <see cref="Target"/>.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class AdvXenoAcidComponent : Component
{
    [DataField]
    public EntityUid? Target;

    [DataField]
    public int Ticks;

    /// <summary>4 for objects, 8 for walls.</summary>
    [DataField]
    public int Strength = 4;

    [DataField, AutoPausedField]
    public TimeSpan NextTick;
}

/// <summary>Can never be dissolved by corrosive acid (INTEGRITY_ACIDPROOF / unmeltable materials).</summary>
[RegisterComponent]
public sealed partial class AdvXenoAcidProofComponent : Component;

/// <summary>Holds devoured prey (gluttonous = 2). Prey is kept alive until regurgitated or the xeno dies.</summary>
[RegisterComponent]
public sealed partial class AdvXenoStomachComponent : Component
{
    public const string ContainerId = "advxeno_stomach";

    [DataField]
    public TimeSpan DevourTime = TimeSpan.FromSeconds(3);
}

/// <summary>In-flight hunter leap: checks for the grab when it lands.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class AdvXenoLeapingComponent : Component
{
    [DataField]
    public EntityUid Target;

    [DataField, AutoPausedField]
    public TimeSpan ResolveAt;
}

/// <summary>Larva "Hide": draws under tables and items.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AdvXenoHiderComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Hidden;
}

/// <summary>NPC biomorph special attacks (charge, leap, tunnel, berserk, burst).</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class AdvXenoBiomorphComponent : Component
{
    [DataField]
    public AdvXenoBiomorphSpecial Special = AdvXenoBiomorphSpecial.None;

    [DataField]
    public float SpecialMinRange = 1f;

    [DataField]
    public float SpecialMaxRange = 6f;

    [DataField]
    public TimeSpan SpecialCooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan Warmup = TimeSpan.FromSeconds(1);

    [DataField, AutoPausedField]
    public TimeSpan NextSpecial;

    /// <summary>Active special: when the windup ends and the move happens.</summary>
    [DataField, AutoPausedField]
    public TimeSpan? ExecuteAt;

    [DataField]
    public EntityUid? SpecialTarget;

    [DataField]
    public bool Charging;

    /// <summary>Stun on whoever gets run over by a charge.</summary>
    [DataField]
    public TimeSpan ChargeStun = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan ChargeKnockdown = TimeSpan.FromSeconds(2);

    [DataField]
    public float ChargeObjectDamage = 60f;

    [DataField]
    public SoundSpecifier? ChargeSound;

    /// <summary>Sprite state shown while charging (breaker_charging / monarch_charge).</summary>
    [DataField]
    public string? ChargeState;

    /// <summary>The living sprite state to return to after a charge.</summary>
    [DataField]
    public string? AliveState;

    [DataField]
    public SoundSpecifier? ChargeHitSound;

    /// <summary>Melee hits send medium-or-smaller victims flying.</summary>
    [DataField]
    public bool HeavyClaws;

    [DataField]
    public SoundSpecifier? HeavyClawSound;

    /// <summary>Melee damage multiplier against stunned victims (sprinter).</summary>
    [DataField]
    public float IncapacitatedDamageMultiplier = 1f;

    [DataField]
    public SoundSpecifier? DeathSound;

    [DataField]
    public LocId DeathMessage = "adv-xeno-biomorph-death";

    // Burrower tunnel state
    [DataField]
    public List<Vector2i> TunnelPath = new();

    [DataField, AutoPausedField]
    public TimeSpan NextTunnelStep;

    [DataField]
    public bool Overshooting;

    // Berserker
    [DataField, AutoPausedField]
    public TimeSpan? BerserkUntil;

    // Burster
    [DataField]
    public bool Exploded;

    [DataField, AutoPausedField]
    public TimeSpan? ExplodeAt;
}

public enum AdvXenoBiomorphSpecial : byte
{
    None,
    Charge,
    Leap,
    Tunnel,
    Berserk,
    Burst,
}

[Serializable, NetSerializable]
public enum AdvXenoBiomorphVisuals : byte
{
    Charging,
    Submerged,
    Berserk,
}

/// <summary>Mind role for advanced xenomorph antagonists (objectives filter on it).</summary>
[RegisterComponent]
public sealed partial class AdvXenomorphRoleComponent : Content.Shared.Roles.Components.BaseMindRoleComponent;
