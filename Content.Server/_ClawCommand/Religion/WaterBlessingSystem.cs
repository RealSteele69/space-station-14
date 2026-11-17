// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Religion;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Bible.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Religion;

/// <summary>
/// Chaplains can use their bible (or anything with <see cref="WaterBlesserComponent"/>) on a container to bless the water inside into holy water.
/// </summary>
public sealed partial class WaterBlessingSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly ProtoId<ReagentPrototype> Water = "Water";
    private static readonly ProtoId<ReagentPrototype> HolyWater = "Holywater";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WaterBlesserComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<WaterBlesserComponent, BlessWaterDoAfterEvent>(OnBlessDoAfter);
    }

    private void OnAfterInteract(Entity<WaterBlesserComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled
            || !args.CanReach
            || args.Target is not { } target
            || !HasComp<BibleUserComponent>(args.User)
            || GetWater(target) <= 0)
            return;

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.BlessTime, new BlessWaterDoAfterEvent(), ent, target, ent)
        {
            BreakOnMove = true,
            BreakOnDropItem = true,
            NeedHand = true,
            BlockDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("bless-water-start",
                ("user", Identity.Entity(args.User, EntityManager)), ("target", target)),
            args.User);
    }

    private void OnBlessDoAfter(Entity<WaterBlesserComponent> ent, ref BlessWaterDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        if (!TryGetSolution(target, out var soln))
            return;

        var amount = soln.Value.Comp.Solution.GetTotalPrototypeQuantity(Water);
        if (amount <= 0)
            return;

        _solution.RemoveReagent(soln.Value, Water, amount);
        _solution.TryAddReagent(soln.Value, HolyWater, amount, out _);

        _audio.PlayPvs(ent.Comp.BlessSound, target, AudioParams.Default.WithVolume(-2f));
        Spawn(ent.Comp.BlessEffect, Transform(target).Coordinates);
        _popup.PopupEntity(Loc.GetString("bless-water-done", ("target", target)), args.User, args.User);
    }

    private float GetWater(EntityUid target)
    {
        return TryGetSolution(target, out var soln)
            ? soln.Value.Comp.Solution.GetTotalPrototypeQuantity(Water).Float()
            : 0f;
    }

    private bool TryGetSolution(EntityUid target, [NotNullWhen(true)] out Entity<SolutionComponent>? soln)
    {
        return _solution.TryGetRefillableSolution(target, out soln, out _)
               || _solution.TryGetDrainableSolution(target, out soln, out _);
    }
}
