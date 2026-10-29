// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.SlaughterDemon.Other;

/// <summary>
/// Deals damage to the slaughter demon, and expels you from its stomach
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DemonsKissComponent : Component
{
    /// <summary>
    ///  Damage to deal to the demon.
    /// Hardcoded cuz its un-yamlabble as a status effect that can only be achieved through drinking.
    /// </summary>
    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>()
        {
            { "Blunt", 25},
            { "Slash",  25},
        }
    };

    /// <summary>
    ///  Whether to eject the entity, or not
    /// </summary>
    [DataField]
    public bool Eject = true;
}
