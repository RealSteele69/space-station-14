using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.BloodCult.Components;

/// <summary>
/// A cult construct or shade. Counts as a cult member for runes, doors and the cult's headcount.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BloodCultConstructComponent : Component
{
    /// <summary>
    /// Actions granted to the construct when it spawns.
    /// </summary>
    [DataField]
    public List<EntProtoId> Actions = new();

    /// <summary>
    /// How long the "forming" animation plays after spawning.
    /// </summary>
    [DataField]
    public TimeSpan TransformDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public ProtoId<FactionIconPrototype> StatusIcon = "BloodCultMember";

    /// <summary>
    /// Left behind when the construct is destroyed.
    /// </summary>
    [DataField]
    public EntProtoId SpawnOnDeath = "Ectoplasm";

    /// <summary>
    /// Holy constructs serve the crew, so they don't count as cultists.
    /// </summary>
    [DataField]
    public bool Holy;

    [ViewVariables]
    public TimeSpan? TransformEnd;

    [ViewVariables]
    public List<EntityUid> ActionEntities = new();
}
