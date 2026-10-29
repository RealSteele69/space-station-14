// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: source's evolution picker was a Content.Shared._White.RadialSelector list, which doesn't
// exist here. Replaced with the shared WraithRadialEntry/SimpleRadialMenu plumbing (see UI/WraithRadialUi.cs).

using Content.Shared._GoobStation.Wraith.UI;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Wraith.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class EvolveComponent : Component
{
    /// <summary>
    /// Used in UI. The evolutions that you can choose from.
    /// </summary>
    [DataField(required: true)]
    public List<WraithRadialEntry> AvailableEvolutions = new();

    /// <summary>
    /// The corpses required from AbsorbCorpse in order to evolve
    /// </summary>
    [DataField]
    public int CorpsesRequired = 3;

    [ViewVariables]
    public EntityUid? ActionEnt;

    [ViewVariables]
    public EntProtoId ActionId = "ActionEvolve";
}

/// <summary>
/// Raised when attempting to evolve.
/// </summary>
[ByRefEvent]
public record struct WraithEvolveAttemptEvent(int CorpsesRequired, bool Cancelled = false);
