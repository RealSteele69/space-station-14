using Robust.Shared.GameStates;

namespace Content.Shared._ClawCommand.Silicons.Synths.Battery;

/// <summary>
/// Marks a power cell organ as the one powering the <see cref="SynthBatteryComponent"/> body it sits in.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SynthPowerCellComponent : Component;
