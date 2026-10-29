// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: source let the Plaguebringer freely type a reagent amount into a Content.Shared._White.
// ListViewSelector list UI, which doesn't exist here. Simplified to a radial pick (reusing the same
// WraithRadialEntry/SimpleRadialMenu plumbing as Evolve/Spook/ChooseVoidCreature) over the same fixed
// Reagents dictionary from YAML, each use injecting a fixed Amount of the chosen reagent rather than a
// freely chosen quantity - same gameplay result (poison food/drink with a chosen toxin), simpler UI.

using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Wraith.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class DefileComponent : Component
{
    /// <summary>
    /// Dictionary of reagents and the quantity injected when that reagent is picked.
    /// Key: Reagent ID, Value: Quantity to inject.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Reagents = new();

    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype>? ReagentSelected;

    [DataField, AutoNetworkedField]
    public FixedPoint2 ReagentSelectedAmount;
}
