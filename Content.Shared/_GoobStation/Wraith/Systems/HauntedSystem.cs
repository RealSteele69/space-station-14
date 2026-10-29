// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.Wraith.Components;
using Content.Shared.Examine;
using Robust.Shared.Timing;

namespace Content.Shared._GoobStation.Wraith.Systems;

public sealed partial class HauntedSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HauntedComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<HauntedComponent, MapInitEvent>(OnMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HauntedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.DeletionTime)
                continue;

            RemCompDeferred<HauntedComponent>(uid);
        }
    }

    private void OnExamined(Entity<HauntedComponent> ent, ref ExaminedEvent args)
    {
        if (HasComp<WraithComponent>(args.Examiner))
        {
            var remaining = ent.Comp.DeletionTime - _timing.CurTime;
            args.PushMarkup($"[color=mediumpurple]{Loc.GetString("wraith-already-haunted", ("target", ent.Owner))}[/color]");
            args.PushMarkup(Loc.GetString("wraith-haunted-expiration", ("minutes", remaining.Minutes), ("seconds", remaining.Seconds)));
        }
    }

    private void OnMapInit(Entity<HauntedComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.DeletionTime = _timing.CurTime + ent.Comp.Lifetime;
        Dirty(ent);
    }
}
