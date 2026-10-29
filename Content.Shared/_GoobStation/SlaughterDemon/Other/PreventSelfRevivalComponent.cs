// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// New component written for ClawCommand's SlaughterDemon port.
//
// Goobstation checks this component (Content.Goobstation.Shared somewhere upstream of here) from several
// unrelated self-resuscitation systems (defibrillators, revival implants, etc). Wiring an equivalent gate
// into this fork's own SharedDefibrillatorSystem/revival implants would mean editing core vanilla files,
// which is outside this port's additive-only scope (only threats.yml/antagonist.yml/events.yml/locale may
// be touched). So this is kept as a minimal marker, added/removed by SlaughterDemonSystem exactly like the
// source does, but it does NOT currently block anything on its own in this fork - it's a no-op hook for a
// future revival-system integration. Noted as a deliberately narrowed behavior in the port report.
namespace Content.Shared._GoobStation.SlaughterDemon.Other;

/// <summary>
/// Marks an entity that was devoured by a slaughter demon as having been consumed; removed again if the
/// demon that ate it is gibbed. Currently does not block revival on its own (see file header).
/// </summary>
[RegisterComponent]
public sealed partial class PreventSelfRevivalComponent : Component;
