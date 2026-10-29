// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.Wraith.Curses;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._GoobStation.Wraith.Curses;

public sealed partial class CurseHolderSystem : SharedCurseHolderSystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CurseHolderComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnGetStatusIcons(Entity<CurseHolderComponent> ent, ref GetStatusIconsEvent args)
    {
        if (ent.Comp.CurseStatusIcons.Count == 0)
            return;

        foreach (var curseIcon in ent.Comp.CurseStatusIcons)
        {
            var icon = _proto.Index(curseIcon);
            args.StatusIcons.Add(icon);
        }
    }
}
