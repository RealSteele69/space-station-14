// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Devil.Contract;

namespace Content.Shared._ClawCommand.Devil;

/// <summary>
/// Raised on a devil when their power level changes.
/// </summary>
/// <param name="User">The Devil whos power level is changing</param>
/// <param name="NewLevel">The new level they are reaching.</param>
[ByRefEvent]
public record struct PowerLevelChangedEvent(EntityUid User, DevilPowerLevel NewLevel);

/// <summary>
/// Raised on a devil when the amount of souls in their storage changes.
/// </summary>
/// <param name="User">The Devil gaining souls.</param>
/// <param name="Victim">The entity losing its soul.</param>
/// <param name="Amount">How many souls they are gaining.</param>
[ByRefEvent]
public record struct SoulAmountChangedEvent(EntityUid User, EntityUid Victim, int Amount);

// Contract Events

[ImplicitDataDefinitionForInheritors, DataDefinition]
public abstract partial class BaseDevilContractEvent : EntityEventArgs
{
    /// <summary>
    /// The contract using this event.
    /// </summary>
    public DevilContractComponent? Contract;

    /// <summary>
    /// The target affected by this contract.
    /// </summary>
    public EntityUid Target;
}

/// <summary>
/// The target's soul goes to the devil who wrote the contract.
/// </summary>
public sealed partial class DevilContractSoulOwnershipEvent : BaseDevilContractEvent;

/// <summary>
/// Applies a random clause to the target.
/// </summary>
public sealed partial class DevilContractChanceEvent : BaseDevilContractEvent;
