using Content.Shared.Construction.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.BloodCult.Components;

/// <summary>
/// Cult gear. Anyone outside the cult who tries to use, wear, swing, throw or block with it
/// gets knocked down and drops it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BloodCultItemComponent : Component
{
    /// <summary>
    /// Allow non-cultists to use this item?
    /// </summary>
    [DataField]
    public bool AllowUseToEveryone;

    [DataField]
    public TimeSpan KnockdownDuration = TimeSpan.FromSeconds(2);
}

/// <summary>
/// A runed door. Only cult members can open, close or pry it, and it throws everyone else back.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BloodCultRunedDoorComponent : Component
{
    [DataField]
    public float RepulseStrength = 6f;

    [DataField]
    public float RepulseDistance = 2f;

    [DataField]
    public TimeSpan RepulseKnockdown = TimeSpan.FromSeconds(3);
}

/// <summary>
/// A stack of runed metal. Using it in hand opens a radial of cult structures to build.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BloodCultConstructionComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<ConstructionPrototype>> Entries = new();
}
