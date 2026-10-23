using Content.Shared.Speech.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._ClawCommand.Speech;

/// <summary>
///     Claw Command - Rodent cheese puns ("gouda", "brie", "cheesed to meet you").
///     Kept separate from <see cref="ReplacementAccentComponent"/> so that a temporary replacement accent
///     (which overwrites that component's accent) doesn't wipe it out.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CheeseAccentComponent : BaseAccentComponent;
