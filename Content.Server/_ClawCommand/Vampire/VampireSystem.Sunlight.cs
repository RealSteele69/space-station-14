// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Managers;
using Content.Shared._ClawCommand.Vampire;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Vampire;

/// <summary>
/// Sol: a station-wide solar flare that comes round on a timer. Between flares vampires are safe
/// anywhere; during one they burn unless hidden - a locker only softens it, their own coffin stops it.
/// The end of every flare also ages each vampire by one rank.
/// </summary>
public sealed partial class VampireSystem
{
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly ProtoId<DamageTypePrototype> SolDamageType = "Heat";

    private static readonly TimeSpan SolFirstNight = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SolNightGrowth = TimeSpan.FromSeconds(100);
    private static readonly TimeSpan SolDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SolBurnInterval = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan SolWarnEarly = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan SolWarnFinal = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan SolWarnImminent = TimeSpan.FromSeconds(5);

    private static readonly SoundSpecifier SolWarnSound = new SoundPathSpecifier("/Audio/Misc/notice1.ogg");
    private static readonly SoundSpecifier SolStartSound = new SoundPathSpecifier("/Audio/Effects/alert.ogg");
    private static readonly SoundSpecifier SolEndSound = new SoundPathSpecifier("/Audio/_ClawCommand/Seasonal/Halloween/ghosty_wind.ogg");

    /// <summary>When the current night or flare ends. Zero until the first vampire exists.</summary>
    private TimeSpan _solNextChange;
    private TimeSpan _solNight = SolFirstNight;
    private TimeSpan _solNextBurn;
    private bool _solActive;
    private int _solWarnStage;

    /// <summary>True while a solar flare is bombarding the station.</summary>
    public bool IsSolActive => _solActive;

    private void InitializeSunlight()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ResetSol());
    }

    private void ResetSol()
    {
        _solNextChange = TimeSpan.Zero;
        _solNight = SolFirstNight;
        _solActive = false;
        _solWarnStage = 0;
    }

    private void UpdateSol(TimeSpan now)
    {
        if (!EntityQueryEnumerator<VampireComponent>().MoveNext(out _, out _))
        {
            if (_solNextChange != TimeSpan.Zero)
                ResetSol();
            return;
        }

        if (_solNextChange == TimeSpan.Zero)
            _solNextChange = now + _solNight;

        if (!_solActive)
        {
            var remaining = _solNextChange - now;
            if (_solWarnStage < 1 && remaining <= SolWarnEarly)
            {
                _solWarnStage = 1;
                WarnSol("vampire-sol-warn-early", null, SolWarnSound);
            }
            if (_solWarnStage < 2 && remaining <= SolWarnFinal)
            {
                _solWarnStage = 2;
                WarnSol("vampire-sol-warn-final", "vassal-sol-warn-final", SolWarnSound);
            }
            if (_solWarnStage < 3 && remaining <= SolWarnImminent)
            {
                _solWarnStage = 3;
                WarnSol("vampire-sol-warn-imminent", null, SolStartSound);
            }

            if (remaining > TimeSpan.Zero)
                return;

            _solActive = true;
            _solNextChange = now + SolDuration;
            _solNextBurn = now;

            var query = EntityQueryEnumerator<VampireComponent>();
            while (query.MoveNext(out _, out var vampire))
            {
                vampire.WarnedSolOpen = false;
                vampire.WarnedSolLocker = false;
            }

            WarnSol("vampire-sol-start", "vassal-sol-start", null);
            return;
        }

        if (now >= _solNextBurn)
        {
            _solNextBurn += SolBurnInterval;
            PunishVampires();
        }

        if (now < _solNextChange)
            return;

        _solActive = false;
        _solWarnStage = 0;
        _solNight += SolNightGrowth; // every day makes the next night a little longer
        _solNextChange = now + _solNight;
        WarnSol("vampire-sol-end", null, SolEndSound);
        RankUpAll();
    }

    private void PunishVampires()
    {
        var query = EntityQueryEnumerator<VampireComponent>();
        while (query.MoveNext(out var uid, out var vampire))
        {
            if (vampire.FinalDeath)
                continue;

            var ent = (uid, vampire);

            // Their own coffin is perfect cover.
            if (IsRestingInOwnCoffin(ent))
                continue;

            float burn;
            if (_container.IsEntityInContainer(uid))
            {
                // Any other cover only takes the edge off.
                if (!vampire.WarnedSolLocker)
                {
                    vampire.WarnedSolLocker = true;
                    _popup.PopupEntity(Loc.GetString("vampire-sol-locker"), uid, uid, PopupType.MediumCaution);
                }
                burn = 0.5f + vampire.Level / 2f;
            }
            else
            {
                if (!vampire.WarnedSolOpen)
                {
                    vampire.WarnedSolOpen = true;
                    _popup.PopupEntity(Loc.GetString(vampire.Level > 0 ? "vampire-sol-burn" : "vampire-sol-burn-fledgling"),
                        uid, uid, PopupType.LargeCaution);
                }
                burn = 2f + vampire.Level;

                if (vampire.Level > 0)
                    _flammable.AdjustFireStacks(uid, 0.2f + vampire.Level / 10f, ignite: true);
            }

            var damage = new DamageSpecifier(_prototype.Index(SolDamageType), FixedPoint2.New(burn));
            _damageable.TryChangeDamage(uid, damage, ignoreResistances: true, interruptsDoAfters: false);
        }
    }

    /// <summary>
    /// Tells every vampire (and optionally every vassal) about the flare, in chat so it isn't missed.
    /// </summary>
    private void WarnSol(string vampireLoc, string? vassalLoc, SoundSpecifier? sound)
    {
        var vampires = EntityQueryEnumerator<VampireComponent>();
        while (vampires.MoveNext(out var uid, out var vampire))
        {
            if (!vampire.FinalDeath)
                Notify(uid, Loc.GetString(vampireLoc), sound);
        }

        if (vassalLoc == null)
            return;

        var vassals = EntityQueryEnumerator<VassalComponent>();
        while (vassals.MoveNext(out var uid, out _))
            Notify(uid, Loc.GetString(vassalLoc), null);
    }

    private void Notify(EntityUid uid, string message, SoundSpecifier? sound)
    {
        _popup.PopupEntity(message, uid, uid, PopupType.LargeCaution);

        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        _chat.DispatchServerMessage(actor.PlayerSession, message);
        if (sound != null)
            _audio.PlayGlobal(sound, actor.PlayerSession);
    }
}
