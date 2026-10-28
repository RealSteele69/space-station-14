// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Vampire;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;

namespace Content.Server._ClawCommand.Vampire;

public sealed partial class VampireSystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    /// <summary>
    /// How long the victim stays paralyzed by each Feed tick - just long enough to be refreshed by the next one.
    /// </summary>
    private static readonly TimeSpan FeedParalyzeDuration = TimeSpan.FromSeconds(2);

    private void InitializeFeed()
    {
        SubscribeLocalEvent<VampireComponent, VampireFeedActionEvent>(OnFeedAction);
    }

    private void OnFeedAction(Entity<VampireComponent> vampire, ref VampireFeedActionEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<VampireFeedingComponent>(vampire, out var feeding))
        {
            StopFeeding(vampire, (vampire.Owner, feeding));
            args.Handled = true;
            return;
        }

        var target = args.Target;

        if (!TryComp<PullerComponent>(vampire, out var puller)
            || puller.Pulling != target
            || puller.GrabStage < GrabStage.Soft)
        {
            _popup.PopupEntity(Loc.GetString("vampire-feed-fail-no-grab"), vampire, vampire);
            return;
        }

        if (!TryComp<BloodstreamComponent>(target, out var bloodstream) || _bloodstream.GetBloodLevel((target, bloodstream)) <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("vampire-feed-fail-no-blood"), vampire, vampire);
            return;
        }

        var action = EnsureComp<VampireFeedingComponent>(vampire);
        action.Target = target;
        action.Action = args.Action.Owner;

        _actions.SetToggled(args.Action.Owner, true);
        _popup.PopupEntity(Loc.GetString("vampire-feed-start", ("target", target)), vampire, vampire, PopupType.MediumCaution);

        args.Handled = true;
    }

    private void ProcessFeeding(Entity<VampireComponent> vampire, Entity<VampireFeedingComponent> feeding)
    {
        var target = feeding.Comp.Target;

        if (!TryComp<PullerComponent>(vampire, out var puller)
            || puller.Pulling != target
            || puller.GrabStage < GrabStage.Soft
            || !TryComp<BloodstreamComponent>(target, out var bloodstream)
            || _bloodstream.GetBloodLevel((target, bloodstream)) <= 0f)
        {
            StopFeeding(vampire, feeding);
            return;
        }

        vampire.Comp.FedOn.Add(target);

        var drain = vampire.Comp.FeedAmount;
        _bloodstream.TryModifyBloodLevel((target, bloodstream), -drain);

        var efficiency = HasComp<HumanoidProfileComponent>(target) ? 1f : 0.5f;
        AddBloodPoints(vampire, drain * efficiency);

        _stun.TryUpdateParalyzeDuration(target, FeedParalyzeDuration);
    }

    private void StopFeeding(Entity<VampireComponent> vampire, Entity<VampireFeedingComponent> feeding)
    {
        _actions.SetToggled(feeding.Comp.Action, false);
        RemComp<VampireFeedingComponent>(vampire);
        _popup.PopupEntity(Loc.GetString("vampire-feed-stop"), vampire, vampire);
    }
}
