using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._ClawCommand.Vehicle;

/// <summary>
/// Claw Command - lets a tug and its trailers couple into a train. Hitching just points the existing
/// pull system at the two entities' own <see cref="PullerComponent"/>/<see cref="PullableComponent"/>,
/// so the usual pull joint drags the trailer along behind whatever tows it; because a towed trailer can
/// itself tow the next one, this chains into a full train with no extra movement code.
/// </summary>
public sealed partial class VehicleHitchSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleHitchComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<VehicleHitchComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess)
            return;

        // Already hitched to something: offer to unhitch from it.
        if (TryComp<PullableComponent>(ent, out var pullable) && pullable.Puller is { } puller)
        {
            var user = args.User;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("vehicle-unhitch-verb", ("target", puller)),
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/unbuckle.svg.192dpi.png")),
                Priority = 5,
                Act = () => Unhitch(ent, pullable, user),
            });
            return;
        }

        if (!TryComp<PullableComponent>(ent, out var ownPullable))
            return;

        // Nearby hitch points that are free to tow something and won't create a loop.
        foreach (var candidate in _lookup.GetEntitiesInRange<VehicleHitchComponent>(Transform(ent).Coordinates, ent.Comp.Range))
        {
            var candidateUid = candidate.Owner;
            if (candidateUid == ent.Owner)
                continue;

            if (!TryComp<PullerComponent>(candidateUid, out var candidatePuller) || candidatePuller.Pulling != null)
                continue;

            if (IsInChainBehind(ent, candidateUid))
                continue;

            var user = args.User;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("vehicle-hitch-verb", ("target", candidateUid)),
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/buckle.svg.192dpi.png")),
                Priority = 5,
                Act = () => Hitch(candidateUid, (ent.Owner, ownPullable), user),
            });
        }
    }

    /// <summary>True if walking <paramref name="candidate"/>'s own tow chain ever reaches <paramref name="ent"/> (would loop).</summary>
    private bool IsInChainBehind(EntityUid ent, EntityUid candidate)
    {
        var current = candidate;
        var guard = 0;
        while (TryComp<PullableComponent>(current, out var pullable) && pullable.Puller is { } next && guard++ < 64)
        {
            if (next == ent)
                return true;
            current = next;
        }

        return false;
    }

    private void Hitch(EntityUid towUid, Entity<PullableComponent> trailer, EntityUid user)
    {
        if (!_pulling.TryStartPull(towUid, trailer.Owner, pullableComp: trailer.Comp))
        {
            _popup.PopupEntity(Loc.GetString("vehicle-hitch-fail"), trailer, user, PopupType.SmallCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("vehicle-hitch-done", ("target", towUid)), trailer, user);
    }

    private void Unhitch(EntityUid ent, PullableComponent pullable, EntityUid user)
    {
        var tow = pullable.Puller;
        if (tow == null || !_pulling.TryStopPull(ent, pullable, user))
            return;

        _popup.PopupEntity(Loc.GetString("vehicle-unhitch-done", ("target", tow.Value)), ent, user);
    }
}
