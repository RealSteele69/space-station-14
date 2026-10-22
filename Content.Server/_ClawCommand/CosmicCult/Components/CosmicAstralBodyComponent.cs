using Content.Server._ClawCommand.CosmicCult.Abilities;

namespace Content.Server._ClawCommand.CosmicCult.Components;

[RegisterComponent, Access(typeof(CosmicReturnSystem))]
public sealed partial class CosmicAstralBodyComponent : Component
{
    [DataField]
    public EntityUid OriginalBody;
}
