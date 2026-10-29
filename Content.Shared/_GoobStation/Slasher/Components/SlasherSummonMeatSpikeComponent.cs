// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Slasher.Components;

/// <summary>
/// Grants the Slasher the ability to instantly spawn a meat spike.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlasherSummonMeatSpikeComponent : Component
{
    [ViewVariables]
    public EntityUid? ActionEnt;

    [DataField]
    public EntProtoId ActionId = "ActionSlasherSummonMeatSpike";

    /// <summary>
    /// Prototype id of the spike to spawn.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId MeatSpikePrototype = "SlasherMeatSpike";

    /// <summary>
    /// Sound to play when the spike is summoned.
    /// </summary>
    [DataField]
    public SoundSpecifier SummonSound =
        new SoundPathSpecifier("/Audio/_GoobStation/Effects/Slasher/SlasherSummonMeatspike.ogg")
        {
            Params = AudioParams.Default.WithMaxDistance(4f),
        };
}
