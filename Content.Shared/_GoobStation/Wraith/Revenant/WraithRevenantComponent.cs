// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: source pointed RevenantAbilities at a Goobstation-specific abilities bundle entity. This
// fork's vanilla Revenant uses a completely different kit (Essence currency + a Store/RevenantAbilities
// shop category, see Resources/Prototypes/Entities/Mobs/NPCs/revenant.yml), so RevenantAbilities here
// points at WraithRevenantAbilities - a bundle this port defines that grants both the real vanilla
// Revenant kit (RevenantComponent/Store/ActionGrant ActionRevenantShop/Emag) AND this port's bonus
// Wraith-exclusive Revenant-flavored abilities (Crush/Push/Shockwave/TouchOfEvil). See
// Resources/Prototypes/_GoobStation/Wraith/Abilities/revenant.yml.

using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Wraith.Revenant;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class WraithRevenantComponent : Component
{
    [ViewVariables]
    public EntProtoId RevenantAbilities = "WraithRevenantAbilities";

    [ViewVariables, AutoNetworkedField]
    public DamageSpecifier? OldDamageSpecifier;

    [ViewVariables, AutoNetworkedField]
    public bool HadPassive;
}
