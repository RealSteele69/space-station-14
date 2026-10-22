// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Dataset;
using Content.Shared.Polymorph;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Devil;

[RegisterComponent]
public sealed partial class DevilComponent : Component
{
    [DataField]
    public List<EntProtoId> BaseDevilActions = new()
    {
        "ActionCreateContract",
        "ActionShadowJaunt",
        "ActionDevilGrip",
    };

    [DataField]
    public List<EntityUid>? ActionEntities;

    /// <summary>
    /// The amount of souls or successful contracts the entity has.
    /// </summary>
    [DataField]
    public int Souls;

    [DataField]
    public ProtoId<DevilBranchPrototype> DevilBranchPrototype = "BaseDevilBranch";

    /// <summary>
    /// The true name of the devil.
    /// This is auto-generated from a list in the system.
    /// </summary>
    [DataField]
    public string TrueName = string.Empty;

    /// <summary>
    /// The current power level of the devil.
    /// </summary>
    [DataField]
    public DevilPowerLevel PowerLevel = DevilPowerLevel.None;

    /// <summary>
    /// Sound effect played when summoning a contract.
    /// </summary>
    [DataField]
    public SoundPathSpecifier FwooshPath = new("/Audio/Magic/fireball.ogg");

    /// <summary>
    /// When the true-name stun was last triggered
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan LastTriggeredTime;

    /// <summary>
    /// Minimum time between true-name triggers
    /// </summary>
    [DataField]
    public TimeSpan CooldownDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public ProtoId<DatasetPrototype> FirstNameTrue = "names_devil_first";

    [DataField]
    public ProtoId<DatasetPrototype> LastNameTrue = "names_devil_last";

    /// <summary>
    /// How much damage taken when a true name is spoken. Doubled if spoken by the chaplain.
    /// </summary>
    [DataField]
    public DamageSpecifier DamageOnTrueName = new() { DamageDict = { ["Holy"] = 15 } };

    /// <summary>
    /// Holy action damage multiplier if done by the chaplain. Also effects stuns.
    /// </summary>
    [DataField]
    public float BibleUserDamageMultiplier = 2f;

    /// <summary>
    /// How long the Devil is stunned when their true name is spoken. Doubled if spoken by the chaplain.
    /// </summary>
    [DataField]
    public TimeSpan ParalyzeDurationOnTrueName = TimeSpan.FromSeconds(4);

    /// <summary>
    /// The devil's grip action, so its cooldown can be started once the grip is used.
    /// </summary>
    [ViewVariables]
    public EntityUid? DevilGrip;

    [DataField]
    public TimeSpan PossessionDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public EntProtoId GripPrototype = "DevilGrip";

    [DataField]
    public EntProtoId ContractPrototype = "PaperDevilContract";

    [DataField]
    public EntProtoId RevivalContractPrototype = "PaperDevilContractRevival";

    [DataField]
    public EntProtoId PentagramEffectProto = "Pentagram";

    [DataField]
    public EntProtoId FireEffectProto = "FireEffect";

    [DataField]
    public EntProtoId JauntAnimationProto = "PolymorphShadowJauntAnimation";

    [DataField]
    public ProtoId<PolymorphPrototype> JauntEntityProto = "ShadowJaunt";

    [DataField]
    public ProtoId<DamageModifierSetPrototype> DevilDamageModifierSet = "DevilDealPositive";
}
