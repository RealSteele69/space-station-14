// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: these power the Wraith-exclusive "RevenantAbilities" bundle (Content.Shared._GoobStation.Wraith.Revenant),
// a self-contained reskin a Harbinger-form Wraith grants itself after possessing a fresh corpse via
// MakeRevenant. It does NOT spawn, transform into, or otherwise touch this fork's actual vanilla
// Revenant antagonist/mob (Resources/Prototypes/Entities/Mobs/NPCs/revenant.yml) - see the report for
// the full explanation of why that assumption from the task brief doesn't hold once you read the
// source code; "Revenant" here is flavor-only.

using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._GoobStation.Wraith.Events;

public sealed partial class RevenantPushEvent : EntityTargetActionEvent;
public sealed partial class TouchOfEvilEvent : InstantActionEvent;
public sealed partial class RevenantShockwaveEvent : InstantActionEvent;
public sealed partial class RevenantCrushEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class RevenantCrushDoAfterEvent : SimpleDoAfterEvent;
