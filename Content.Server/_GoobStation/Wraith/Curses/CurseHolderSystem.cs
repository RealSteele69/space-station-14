// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: source also cleared curses on a chaplain BibleSmiteUsed event. This fork has no
// Content.Server.Bible system at all (no bible/chaplain smite mechanic exists), so that hook is dropped;
// everything else (the curse tick/apply loop) lives in the shared SharedCurseHolderSystem base.

namespace Content.Server._GoobStation.Wraith.Curses;

public sealed partial class CurseHolderSystem : Content.Shared._GoobStation.Wraith.Curses.SharedCurseHolderSystem;
