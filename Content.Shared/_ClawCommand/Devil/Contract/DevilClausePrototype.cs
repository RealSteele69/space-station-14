// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage.Prototypes;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Devil.Contract;

/// <summary>
/// A clause that can be written on a devil contract as "contractee: id" or "contractor: id".
/// Its ID is what is searched for on the paper.
/// </summary>
[Prototype("clause")]
public sealed partial class DevilClausePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Positive weight is a cost to the signer, negative weight is a benefit.
    /// A contract can only be signed while its total weight is zero or more.
    /// </summary>
    [DataField(required: true)]
    public int ClauseWeight;

    [DataField]
    public ComponentRegistry? AddedComponents;

    [DataField]
    public ComponentRegistry? RemovedComponents;

    /// <summary>
    /// Status effects applied permanently, for traits this fork models as status effects (clumsy, muted...).
    /// </summary>
    [DataField]
    public List<EntProtoId>? StatusEffects;

    [DataField]
    public ProtoId<DamageModifierSetPrototype>? DamageModifierSet;

    [DataField]
    public BaseDevilContractEvent? Event;

    [DataField]
    public List<EntProtoId>? Implants;

    [DataField]
    public List<EntProtoId>? SpawnedItems;

    [DataField]
    public ProtoId<PolymorphPrototype>? Polymorph;
}
