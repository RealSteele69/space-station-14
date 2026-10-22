using Content.Shared.Damage;

namespace Content.Shared._ClawCommand.CosmicCult.Components;

/// <summary>
///     Indicates a glyph entity as performing conversion effects
/// </summary>
[RegisterComponent]
public sealed partial class CosmicGlyphConversionComponent : Component
{
    /// <summary>
    ///     The search range for finding conversion targets.
    /// </summary>
    [DataField]
    public float ConversionRange = 0.5f;

    /// <summary>
    ///     Whether or not we ignore mindshields or chaplain status.
    /// </summary>
    [DataField]
    public bool NegateProtection;

    /// <summary>
    ///     Healing applied on conversion.
    /// </summary>
    [DataField]
    public DamageSpecifier ConversionHeal = new()
    {
        DamageDict = new()
        {
            { "Blunt", -25 },
            { "Slash", -25 },
            { "Piercing", -25 },
            { "Heat", -25 },
            { "Shock", -25 },
            { "Cold", -25 },
            { "Poison", -25 },
            { "Radiation", -25 },
            { "Asphyxiation", -25 }
        }
    };
}
