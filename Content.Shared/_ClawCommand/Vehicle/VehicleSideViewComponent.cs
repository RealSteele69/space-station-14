namespace Content.Shared._ClawCommand.Vehicle;

/// <summary>
/// Claw Command - For vehicles only drawn side-on: a left-facing state plus a mirrored one.
/// The client shows whichever side matches the way the vehicle points on screen, and keeps the last one
/// while it drives straight up or down. Wear (see <see cref="VehicleWearVisuals"/>) swaps in battered states.
/// The state shown is <see cref="State"/> + wear suffix + (<see cref="FlippedSuffix"/> when facing right).
/// </summary>
[RegisterComponent]
public sealed partial class VehicleSideViewComponent : Component
{
    /// <summary>Sprite layer map key to swap states on.</summary>
    [DataField]
    public string Layer = "base";

    /// <summary>The left-facing, undamaged state.</summary>
    [DataField(required: true)]
    public string State = string.Empty;

    [DataField]
    public string FlippedSuffix = "_f";

    [DataField]
    public Dictionary<VehicleWearLevel, string> WearSuffixes = new();

    /// <summary>Which way it was last drawn. Client-side.</summary>
    [ViewVariables]
    public bool FacingRight;

    [ViewVariables]
    public string? ShownState;
}
