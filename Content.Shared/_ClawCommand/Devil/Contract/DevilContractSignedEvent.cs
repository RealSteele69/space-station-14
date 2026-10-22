// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._ClawCommand.Devil.Contract;

/// <summary>
/// Raised on the contractee once they sign a devil contract.
/// </summary>
[ByRefEvent]
public readonly record struct DevilContractSignedEvent(EntityUid Contract);
