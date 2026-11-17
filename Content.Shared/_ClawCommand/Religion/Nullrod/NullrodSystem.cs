// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Bible.Components;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Placeable;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._ClawCommand.Religion.Nullrod;

/// <summary>
/// Null rod basics: punishing untrained users, binding a rod to a chaplain, and transforming it on altars.
/// </summary>
public sealed partial class NullrodSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private TagSystem _tag = default!;

    /// <summary>
    /// Only the plain null rod carries this, so a rod can be transformed once.
    /// </summary>
    private static readonly ProtoId<TagPrototype> NullrodTag = "Nullrod";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NullrodComponent, AttemptMeleeEvent>(OnMeleeAttempt);
        SubscribeLocalEvent<NullrodComponent, ShotAttemptedEvent>(OnShootAttempt);
        SubscribeLocalEvent<NullrodComponent, GetVerbsEvent<ActivationVerb>>(OnGetBindVerb);

        // Before the altar's placeable surface takes the rod off our hands.
        SubscribeLocalEvent<AltarSourceComponent, InteractUsingEvent>(OnAltarInteractUsing, before: [typeof(PlaceableSurfaceSystem)]);
    }

    #region Untrained use

    private void OnMeleeAttempt(Entity<NullrodComponent> ent, ref AttemptMeleeEvent args)
    {
        if (!ent.Comp.UntrainedUseRestriction || HasComp<BibleUserComponent>(args.User))
            return;

        args.Cancelled = true;
        UntrainedDamageAndPopup(ent, args.User);
    }

    private void OnShootAttempt(Entity<NullrodComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!ent.Comp.UntrainedUseRestriction || HasComp<BibleUserComponent>(args.User))
            return;

        args.Cancel();
        UntrainedDamageAndPopup(ent, args.User);
    }

    private void UntrainedDamageAndPopup(Entity<NullrodComponent> ent, EntityUid user)
    {
        if (_net.IsClient || _timing.CurTime < ent.Comp.NextPopupTime)
            return;

        ent.Comp.NextPopupTime = _timing.CurTime + ent.Comp.PopupCooldown;
        Dirty(ent);

        if (!_damageable.TryChangeDamage(user, ent.Comp.DamageOnUntrainedUse, origin: ent))
            return;

        _popup.PopupEntity(Loc.GetString(ent.Comp.UntrainedUseString), user, user, PopupType.MediumCaution);
        _audio.PlayPvs(ent.Comp.UntrainedUseSound, user);
    }

    #endregion

    #region Binding

    private void OnGetBindVerb(Entity<NullrodComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<BibleUserComponent>(args.User))
            return;

        if (TryComp<NullrodBearerComponent>(args.User, out var bearer) && bearer.NullRod == ent.Owner)
            return;

        var user = args.User;
        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString("nullrod-recall-verb-bind"),
            Act = () =>
            {
                Bind(user, ent);
                _popup.PopupClient(Loc.GetString("nullrod-recall-verb-bind-done", ("nullrod", ent.Owner)), user, user);
            },
        });
    }

    /// <summary>
    /// Makes <paramref name="nullrod"/> the rod that <paramref name="user"/> recalls at altars.
    /// </summary>
    public void Bind(EntityUid user, EntityUid? nullrod)
    {
        var bearer = EnsureComp<NullrodBearerComponent>(user);
        bearer.NullRod = nullrod;
        Dirty(user, bearer);
    }

    #endregion

    #region Altar transformation

    private void OnAltarInteractUsing(Entity<AltarSourceComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled
            || HasComp<StorageComponent>(args.Target)
            || !_tag.HasTag(args.Used, NullrodTag))
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        Spawn(ent.Comp.EffectProto, Transform(ent).Coordinates);
        _audio.PlayPvs(ent.Comp.SoundPath, ent, AudioParams.Default.WithVolume(-4f));

        var newRod = Spawn(ent.Comp.RodProto, args.ClickLocation.SnapToGrid(EntityManager));

        // The old rod is gone, so whatever the chaplain had bound now points at the new one.
        if (TryComp<NullrodBearerComponent>(args.User, out var bearer) && bearer.NullRod == args.Used)
            Bind(args.User, HasComp<NullrodComponent>(newRod) ? newRod : null);

        QueueDel(args.Used);
    }

    #endregion
}
