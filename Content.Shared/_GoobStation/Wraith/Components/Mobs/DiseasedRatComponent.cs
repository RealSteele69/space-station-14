// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: source used PolymorphConfiguration.ComponentsToTransfer to carry components across the
// evolve-polymorph, a field that doesn't exist on this fork's PolymorphConfiguration. The server system
// instead does a manual Spawn + EntityManager.CopyComponents + mind transfer + delete, matching the pattern
// this fork's own WraithEvolveSystem/SummonVoidCreatureSystem already use, so TransferComponents is unused
// here and dropped from the prototype (CopyComponents already copies everything relevant).

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Wraith.Components.Mobs;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class DiseasedRatComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<DiseasedRatFormUnlockPrototype>> DiseasedRatForms = new();
}

[Prototype]
public sealed partial class DiseasedRatFormUnlockPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public int FilthRequired;

    [DataField(serverOnly: true)]
    public EntProtoId? Entity;
}
