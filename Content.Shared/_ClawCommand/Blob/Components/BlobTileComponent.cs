using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Blob.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), Serializable]
public sealed partial class BlobTileComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.White;

    [ViewVariables]
    public Entity<BlobCoreComponent>? Core;

    [DataField]
    public bool ReturnCost = true;

    [DataField(required: true)]
    public BlobTileType BlobTileType = BlobTileType.Invalid;

    [DataField]
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

    [DataField]
    public DamageSpecifier FlashDamage = new()
    {
        DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
        {
            { "Heat", 24 },
        }
    };
}

[Serializable]
public enum BlobTileType : byte
{
    Invalid, // invalid default value 0
    Normal,
    Strong,
    Reflective,
    Resource,
    /*
    Storage,
    Turret,
    */
    Node,
    Factory,
    Core,
}
