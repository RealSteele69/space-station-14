// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: see EvolveComponent.cs - replaced Content.Shared._White.RadialSelector with the shared
// WraithRadialEntry list.

using Content.Shared._GoobStation.Wraith.UI;
using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SpookComponent : Component
{
    [DataField(required: true)]
    public List<WraithRadialEntry> Actions = new();
}
