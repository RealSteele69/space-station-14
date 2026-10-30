using Robust.Shared.GameStates;

namespace Content.Shared._ClawCommand.TapeRecorder.Components;

/// <summary>
/// Added to tape recorders that are updating, winding or rewinding the tape.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ActiveTapeRecorderComponent : Component;
