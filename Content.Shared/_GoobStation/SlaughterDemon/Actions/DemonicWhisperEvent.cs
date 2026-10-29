// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// New file written for ClawCommand's SlaughterDemon port.
//
// Goobstation's "Demonic Whisper" action is built on a generic ActionTelepathy/TelepathyAction framework
// (dialogueTitle + a text-entry dialog box, popupWhisperSelf/popupWhisperFlavor) that doesn't exist in this
// fork at all - there is no telepathy/dialog-box action infrastructure here outside the unrelated Psionics
// system. Reimplementing that whole framework just for one flavor ability on SlaughterDemon was judged out
// of scope for a focused port, so this is simplified to a single themed EntityTargetAction: no player-typed
// message, just a flat "a demonic presence claws at your mind" popup to the target and a confirmation popup
// to the demon. See DemonicWhisperSystem (Content.Server._GoobStation.SlaughterDemon.Speech) for the handler.

using Content.Shared.Actions;

namespace Content.Shared._GoobStation.SlaughterDemon.Actions;

public sealed partial class DemonicWhisperEvent : EntityTargetActionEvent;
