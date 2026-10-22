using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.BloodCult.Runes;

/// <summary>
/// One entry in the ritual dagger's rune menu.
/// </summary>
[Prototype("bloodCultRune")]
public sealed partial class RuneSelectorPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The rune entity that gets drawn.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public TimeSpan DrawTime = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Only drawable once the sacrifice target is dead, and only where the veil is thin.
    /// </summary>
    [DataField]
    public bool RequireTargetDead;

    [DataField]
    public int RequiredTotalCultists = 1;

    /// <summary>
    /// Damage the drawer takes for scribing the rune in their own blood.
    /// </summary>
    [DataField]
    public DamageSpecifier DrawDamage = new() { DamageDict = new() { ["Slash"] = 15 } };

    /// <summary>
    /// Order in the menu.
    /// </summary>
    [DataField]
    public int Order;
}
