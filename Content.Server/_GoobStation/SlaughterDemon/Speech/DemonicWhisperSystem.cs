// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// New file written for ClawCommand's SlaughterDemon port. See DemonicWhisperEvent for why this is a
// simplified stand-in for Goobstation's TelepathyAction-based "Demonic Whisper" ability.

using Content.Shared._GoobStation.SlaughterDemon.Actions;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;

namespace Content.Server._GoobStation.SlaughterDemon.Speech;

public sealed partial class DemonicWhisperSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DemonicWhisperEvent>(OnDemonicWhisper);
    }

    private void OnDemonicWhisper(DemonicWhisperEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;

        _popup.PopupEntity(Loc.GetString("demonic-whisper-flavor"), target, target, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("demonic-whisper-self", ("name", Identity.Name(target, EntityManager))), args.Performer, args.Performer);

        args.Handled = true;
    }
}
