namespace Content.Server._ClawCommand.CosmicCult.Components;

/// <summary>
/// Barotrauma immunity granted to empowered cosmic cultists (tier 3 monument, or absorbing a malign rift).
/// Answers <see cref="Content.Shared.Atmos.RefreshPressureImmunityEvent"/> while present.
/// Kept separate from other permanent pressure immunities so deconversion only strips what the cult gave.
/// </summary>
[RegisterComponent]
public sealed partial class CosmicPressureImmunityComponent : Component;
