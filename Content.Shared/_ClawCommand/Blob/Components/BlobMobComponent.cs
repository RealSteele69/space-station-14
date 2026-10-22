using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Blob.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class BlobMobComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly), DataField("healthOfPulse")]
    public DamageSpecifier HealthOfPulse = new()
    {
        DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
        {
            { "Blunt", -2 },
            { "Slash", -2 },
            { "Piercing", -2 },
            { "Heat", -2 },
            { "Cold", -2 },
            { "Shock", -2 },
        }
    };
}
