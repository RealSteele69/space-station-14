// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Wraith.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SummonRotHulkComponent : Component
{
    /// <summary>
    /// Unique tag that works only for entities from the Trash Spawner
    /// </summary>
    [DataField]
    public ProtoId<TagPrototype> TrashTag = "RotHulkTrash";

    [DataField]
    public EntProtoId RotHulkProto = "MobRotHulk";

    [DataField]
    public EntProtoId BuffRotHulkProto = "MobRotHulkBuff";

    /// <summary>
    /// The minimum amount of trash needed to spawn a hulk
    /// </summary>
    [DataField]
    public int MinTrash = 10;

    /// <summary>
    /// The maximum amount of trash needed to spawn a hulk
    /// </summary>
    [DataField]
    public int MaxTrash = 40;

    /// <summary>
    /// The amount of trash needed to spawn a buffed hulk
    /// </summary>
    [DataField]
    public int BuffThreshold = 30;

    /// <summary>
    /// The search radius of this ability
    /// </summary>
    [DataField]
    public float SearchRadius = 3f;
}
