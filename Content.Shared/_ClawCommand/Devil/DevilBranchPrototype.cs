// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Devil;

/// <summary>
/// Which actions a devil unlocks at each power level.
/// </summary>
[Prototype("devilBranchPrototype")]
public sealed partial class DevilBranchPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Dictionary<DevilPowerLevel, List<EntProtoId>> PowerActions = new();
}

public enum DevilPowerLevel : byte
{
    None,
    Weak,
    Moderate,
    Powerful,
}
