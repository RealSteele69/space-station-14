using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.Blob.Components;

/// <summary>
/// The blob's brain. Holds the point store, the chemical and all the balance numbers.
/// Claw Command: every number here is tuned well below the original so the crew can actually win.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlobCoreComponent : Component
{
    #region Live Data

    [ViewVariables]
    public EntityUid? Observer = default!;

    [ViewVariables]
    public HashSet<EntityUid> BlobTiles = [];

    [ViewVariables]
    public List<EntityUid> Actions = [];

    [ViewVariables]
    public TimeSpan NextAction = TimeSpan.Zero;

    [ViewVariables]
    public BlobChemType CurrentChem = BlobChemType.ReactiveSpines;

    #endregion

    #region Balance

    /// <summary>
    /// Only used for the health alert, keep in sync with the core's Destructible threshold.
    /// </summary>
    [DataField]
    public FixedPoint2 CoreBlobTotalHealth = 200;

    [DataField]
    public float StartingMoney = 150f; // enough for one resource blob and a little growth

    /// <summary>
    /// Extra seconds between manual attacks.
    /// </summary>
    [DataField]
    public float AttackRate = 0.6f;

    /// <summary>
    /// Extra seconds between manual growth.
    /// </summary>
    [DataField]
    public float GrowRate = 0.3f;

    [DataField]
    public bool CanSplit = true;

    /// <summary>
    /// Multiplier on the chemical's damage for blobbernaut punches.
    /// </summary>
    [DataField]
    public float BlobbernautDamageMultiplier = 0.6f;

    #endregion

    #region Damage Specifiers

    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public BlobChemDamage ChemDamageDict { get; set; } = new()
    {
        {
            BlobChemType.BlazingOil, new DamageSpecifier()
            {
                DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                {
                    { "Heat", 10 },
                    { "Structural", 100 },
                }
            }
        },
        {
            BlobChemType.ReactiveSpines, new DamageSpecifier()
            {
                DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                {
                    { "Blunt", 5 },
                    { "Slash", 5 },
                    { "Piercing", 5 },
                    { "Structural", 100 },
                }
            }
        },
        {
            BlobChemType.ExplosiveLattice, new DamageSpecifier()
            {
                DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                {
                    { "Heat", 4 },
                    { "Structural", 100 },
                }
            }
        },
        {
            BlobChemType.ElectromagneticWeb, new DamageSpecifier()
            {
                DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                {
                    { "Structural", 100 },
                    { "Shock", 12 },
                },
            }
        },
        {
            BlobChemType.RegenerativeMateria, new DamageSpecifier()
            {
                DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                {
                    { "Structural", 80 },
                    { "Poison", 10 },
                }
            }
        },
        {
            BlobChemType.ComatoseFiber, new DamageSpecifier()
            {
                DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                {
                    { "Structural", 100 },
                    { "Asphyxiation", 14 },
                }
            }
        },
        {
            BlobChemType.ChainCoating, new DamageSpecifier()
            {
                DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                {
                    { "Structural", 100 },
                    { "Blunt", 8 },
                    { "Slash", 8 },
                }
            }
        },
        {
            BlobChemType.SinewyTendons, new DamageSpecifier()
            {
                DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                {
                    { "Structural", 100 },
                    { "Blunt", -5 },
                    { "Slash", -5 },
                    { "Piercing", -5 },
                    { "Poison", -5 },
                    { "Heat", -5 },
                    { "Cold", -5 },
                    { "Asphyxiation", -5 },
                }
            }
        },
        {
            BlobChemType.CorrosiveSlime, new DamageSpecifier()
            {
                DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                {
                    { "Structural", 200 },
                    { "Caustic", 9 },
                    { "Cellular", 1 }
                }
            }
        },
        {
            BlobChemType.CryogenicPoison, new DamageSpecifier()
            {
                DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                {
                    { "Structural", 70 },
                    { "Cold", 11 },
                    { "Slash", 3 }
                }
            }
        },
    };

    #endregion

    #region Blob Chems

    [ViewVariables]
    public readonly BlobChemColors ChemColors = new()
    {
        {BlobChemType.ReactiveSpines, Color.FromHex("#637b19")},
        {BlobChemType.BlazingOil, Color.FromHex("#937000")},
        {BlobChemType.RegenerativeMateria, Color.FromHex("#441e59")},
        {BlobChemType.ExplosiveLattice, Color.FromHex("#6e2a00")},
        {BlobChemType.ElectromagneticWeb, Color.FromHex("#0d7777")},
        {BlobChemType.ComatoseFiber, Color.FromHex("#191978")},
        {BlobChemType.ChainCoating, Color.FromHex("#3b3b3b")},
        {BlobChemType.SinewyTendons, Color.FromHex("#690f53")},
        {BlobChemType.CorrosiveSlime, Color.FromHex("#9cae6b")},
        {BlobChemType.CryogenicPoison, Color.FromHex("#5282ae")},
    };

    [DataField]
    public BlobChemType DefaultChem = BlobChemType.ReactiveSpines;

    #endregion

    #region Blob Costs

    [DataField]
    public int ResourceBlobsTotal;

    [DataField]
    public FixedPoint2 AttackCost = 5;

    /// <summary>
    /// Multiplier on the normal tile cost when growing over open space (plating gets laid underneath).
    /// </summary>
    [DataField]
    public float SpaceGrowthCostMultiplier = 4f;

    [DataField]
    public BlobTileCosts BlobTileCosts = new()
    {
        {BlobTileType.Core, 0},
        {BlobTileType.Invalid, 0},
        {BlobTileType.Resource, 70},
        {BlobTileType.Factory, 100},
        {BlobTileType.Node, 65},
        {BlobTileType.Reflective, 20},
        {BlobTileType.Strong, 20},
        {BlobTileType.Normal, 8},
    };

    [DataField]
    public Dictionary<BlobTileType, Dictionary<BlobChemType, FixedPoint2>> BlobTileCostsByChem = new()
    {
        [ BlobTileType.Strong ] = new()
        {
            { BlobChemType.ChainCoating, 15 },
            { BlobChemType.SinewyTendons, 30 },
        },
        [ BlobTileType.Reflective ] = new()
        {
            { BlobChemType.ChainCoating, 15 },
            { BlobChemType.SinewyTendons, 25 },
        },
        [ BlobTileType.Factory ] = new()
        {
            { BlobChemType.ChainCoating, 120 },
            { BlobChemType.SinewyTendons, 80 },
        },
        [ BlobTileType.Resource ] = new()
        {
            { BlobChemType.ChainCoating, 95 },
            { BlobChemType.SinewyTendons, 55 },
        },
        [ BlobTileType.Node ] = new()
        {
            { BlobChemType.CorrosiveSlime, 120 },
        }
    };

    [DataField]
    public FixedPoint2 BlobbernautCost = 90;

    [DataField]
    public FixedPoint2 SplitCoreCost = 500;

    [DataField]
    public FixedPoint2 SwapCoreCost = 250;

    [DataField]
    public FixedPoint2 SwapChemCost = 90;

    #endregion

    #region Blob Ranges

    /// <summary>
    /// Minimum distance between two nodes.
    /// </summary>
    [DataField]
    public float NodeRadiusLimit = 6f;

    /// <summary>
    /// How far from a node or the core new tiles can grow.
    /// </summary>
    [DataField]
    public float TilesRadiusLimit = 7f;

    #endregion

    #region Prototypes

    [DataField]
    public BlobTileProto TilePrototypes = new()
    {
        {BlobTileType.Resource, "ResourceBlobTile"},
        {BlobTileType.Factory, "FactoryBlobTile"},
        {BlobTileType.Node, "NodeBlobTile"},
        {BlobTileType.Reflective, "ReflectiveBlobTile"},
        {BlobTileType.Strong, "StrongBlobTile"},
        {BlobTileType.Normal, "NormalBlobTile"},
        {BlobTileType.Invalid, "NormalBlobTile"},
        {BlobTileType.Core, "CoreBlobTile"},
    };

    [DataField(required: true)]
    public List<EntProtoId> ActionPrototypes = [];

    [DataField]
    public ProtoId<ExplosionPrototype> BlobExplosive = "Blob";

    [DataField]
    public EntProtoId<BlobObserverComponent> ObserverBlobPrototype = "MobObserverBlob";

    [DataField]
    public EntProtoId MindRoleBlobPrototypeId = "MindRoleBlob";

    #endregion

    #region Sounds

    [DataField]
    public SoundSpecifier GreetSoundNotification = new SoundPathSpecifier("/Audio/Effects/clang.ogg");

    [DataField]
    public SoundSpecifier AttackSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");

    #endregion
}

[Serializable, NetSerializable]
public enum BlobChemType : byte
{
    BlazingOil,
    ReactiveSpines,
    RegenerativeMateria,
    ExplosiveLattice,
    ElectromagneticWeb,
    ComatoseFiber,
    SinewyTendons,
    ChainCoating,
    CorrosiveSlime,
    CryogenicPoison
}
