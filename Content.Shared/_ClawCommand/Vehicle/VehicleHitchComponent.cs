// Claw Command - tow hitching. The behaviour follows the original train coupling source;
// the implementation reuses the engine's own pull/joint system instead of a bespoke mover.

using Robust.Shared.GameStates;

namespace Content.Shared._ClawCommand.Vehicle;

/// <summary>
/// Marks a tug or trailer that can be hitched into a train: right-clicking it while another
/// hitch-marked entity with a free <c>Puller</c> is nearby offers "Hitch to"; right-clicking it
/// while already hitched offers "Unhitch". The actual coupling is an ordinary pull joint between
/// the two entities' existing <c>PullerComponent</c>/<c>PullableComponent</c>, so a trailer that is
/// itself being towed can also tow the next trailer behind it, forming a chain.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VehicleHitchComponent : Component
{
    /// <summary>How close two hitch points need to be to couple.</summary>
    [DataField]
    public float Range = 1.5f;
}
