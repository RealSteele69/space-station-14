// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: dropped SlasherIncorporealObserverCheckEvent/SlasherIncorporealCameraCheckEvent -
// the observer check is a direct method call to SlasherFearSystem.IsObservedByPlayers instead of an
// event, and the camera-blocks-corporealize check was dropped as presentation-only polish (see
// SlasherIncorporealSystem.cs).

using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._GoobStation.Slasher;

public sealed class SlasherAscendedEvent(EntityUid slasher) : EntityEventArgs
{
    public readonly EntityUid Slasher = slasher;
}

public sealed partial class SlasherToggleFearMusicAlertEvent : BaseAlertEvent;

[ByRefEvent]
public sealed partial class SlasherRegenerateEvent : InstantActionEvent;

/// <summary>
/// Raised on the slasher when Regenerate brings them back from death.
/// </summary>
[ByRefEvent]
public sealed class SlasherRevivedFromDeathEvent : EntityEventArgs;

[ByRefEvent]
public sealed partial class SlasherMassacreEvent : InstantActionEvent;

[ByRefEvent]
public sealed partial class SlasherPossessionEvent : EntityTargetActionEvent;

/// <summary>
/// Soul steal targeted action event.
/// </summary>
[ByRefEvent]
public sealed partial class SlasherSoulStealEvent : EntityTargetActionEvent;

[ByRefEvent]
public sealed partial class SlasherStaggerAreaEvent : InstantActionEvent;

[ByRefEvent]
public sealed partial class SlasherSummonMacheteEvent : InstantActionEvent;

[ByRefEvent]
public sealed partial class SlasherSummonMeatSpikeEvent : InstantActionEvent;

[ByRefEvent]
public sealed partial class SlasherRelentlessGrabEvent : InstantActionEvent;

/// <summary>
/// DoAfter event raised when Soul Steal completes.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SlasherSoulStealDoAfterEvent : SimpleDoAfterEvent;

#region Incorporealize / corporealize events

[Serializable, NetSerializable]
public sealed partial class SlasherIncorporealizeDoAfterEvent : SimpleDoAfterEvent;

[ByRefEvent]
public sealed partial class SlasherIncorporealizeEvent : InstantActionEvent;

[ByRefEvent]
public sealed partial class SlasherCorporealizeEvent : InstantActionEvent;

[ByRefEvent]
public sealed class SlasherIncorporealEnteredEvent : EntityEventArgs;

#endregion
