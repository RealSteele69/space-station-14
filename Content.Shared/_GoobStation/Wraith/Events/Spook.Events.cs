// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.Actions;

namespace Content.Shared._GoobStation.Wraith.Events;

public sealed partial class FlipLightsEvent : InstantActionEvent;

public sealed partial class BurnLightsEvent : InstantActionEvent;
public sealed partial class OpenDoorsSpookEvent : InstantActionEvent;
public sealed partial class CreateSmokeSpookEvent : InstantActionEvent;
public sealed partial class CreateEctoplasmEvent : InstantActionEvent;
public sealed partial class SapApcEvent : InstantActionEvent;
public sealed partial class RandomSpookEvent : InstantActionEvent;
