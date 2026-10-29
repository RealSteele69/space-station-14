// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note (simplification): dropped FrozenCooldowns/IncorporealStartTime (cooldown-freeze while
// incorporeal) and the surveillance-camera exit check - both are presentation-only polish on top of
// the core "become invisible and walk through walls, but only while unseen" mechanic, and neither is
// load-bearing. See SlasherIncorporealSystem.cs for the full adaptation notes.

using Content.Shared.Alert;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Slasher.Components;

/// <summary>
/// Grants the Slasher the ability to toggle incorporeal form.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class SlasherIncorporealComponent : Component
{
    [ViewVariables]
    public EntityUid? IncorporealizeActionEnt;

    [ViewVariables]
    public EntityUid? CorporealizeActionEnt;

    [DataField]
    public EntProtoId IncorporealizeActionId = "ActionSlasherIncorporealize";

    [DataField]
    public EntProtoId CorporealizeActionId = "ActionSlasherCorporealize";

    /// <summary>
    /// Current state of the slasher. True when incorporeal.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsIncorporeal;

    /// <summary>
    /// Range (in tiles) to check for observers with line of sight that prevent incorporealizing
    /// or corporealizing.
    /// </summary>
    [DataField]
    public float ObserverCheckRange = 10f;

    /// <summary>
    /// How long the do-after to enter incorporeal form takes.
    /// </summary>
    [DataField]
    public TimeSpan IncorporealizeDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Collision mask/layer applied while incorporeal, so the slasher can pass through walls.
    /// </summary>
    [DataField]
    public int CollisionMask;

    [DataField]
    public int CollisionLayer;

    [ViewVariables]
    public int StoredMask;

    [ViewVariables]
    public int StoredLayer;

    /// <summary>
    /// Components added while incorporeal that get removed again on exit. Lets kits add their own
    /// immunities without this system needing to know about them.
    /// </summary>
    [ViewVariables]
    public HashSet<string> AddedIncorporealComponents = new();

    [DataField]
    public SoundSpecifier JauntAppear = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    [DataField]
    public SoundSpecifier JauntDisappear = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");
}
