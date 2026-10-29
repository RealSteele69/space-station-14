// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.StatusEffect;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Wraith.Minions.Harbinger;

[RegisterComponent, NetworkedComponent]
public sealed partial class SpikerShuffleComponent : Component
{
    [DataField]
    public List<ProtoId<StatusEffectPrototype>> StatusEffectsToRemove = new();

    [ViewVariables]
    public EntProtoId StatusEffect = "StatusEffectShuffle";

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

    [DataField]
    public EntProtoId StatusAbilityDisable = "StatusEffectWeakenedWraith";

    #region Visualizer
    [DataField]
    public string Normal = "spiker";

    [DataField]
    public string Shuffling = "shuffling_spiker";
    #endregion
}
