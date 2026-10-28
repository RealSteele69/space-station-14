// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.Vampire;

public sealed partial class VampireFeedActionEvent : EntityTargetActionEvent;

public sealed partial class VampireMasqueradeActionEvent : InstantActionEvent;

public sealed partial class VampireFortitudeActionEvent : InstantActionEvent;

public sealed partial class VampireCloakActionEvent : InstantActionEvent;

public sealed partial class VampireMesmerizeActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class VampireMesmerizeDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class VampireEmbraceActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class VampireEmbraceDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class VassalRecuperateActionEvent : InstantActionEvent;

public sealed partial class VassalSenseMasterActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class VampireStakeDoAfterEvent : SimpleDoAfterEvent;
