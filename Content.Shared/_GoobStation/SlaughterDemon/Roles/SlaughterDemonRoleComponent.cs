// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.Roles.Components;

namespace Content.Shared._GoobStation.SlaughterDemon.Roles;

/// <summary>
///  Added to mind role entities to tag that they are a slaughter demon.
/// </summary>
[RegisterComponent]
public sealed partial class SlaughterDemonRoleComponent : BaseMindRoleComponent;
