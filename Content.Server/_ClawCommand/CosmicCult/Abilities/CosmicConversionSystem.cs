// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Bible.Components;
using Content.Goobstation.Common.Conversion;
using Content.Goobstation.Common.Religion;
using Content.Server._ClawCommand.CosmicCult.Components;
using Content.Server.Popups;
using Content.Shared._ClawCommand.CosmicCult.Components;
using Content.Shared._ClawCommand.CosmicCult;
using Content.Shared.Damage;
using Content.Shared.Mind;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Timing;
using Content.Server.Atmos.Rotting;
using Content.Server.Administration.Systems;
using Content.Shared.Administration.Systems;
using Content.Shared.Damage.Systems;

namespace Content.Server._ClawCommand.CosmicCult.Abilities;

public sealed partial class CosmicConversionSystem : EntitySystem
{
    [Dependency] private CosmicCultRuleSystem _cultRule = default!;
    [Dependency] private CosmicGlyphSystem _cosmicGlyph = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedCosmicCultSystem _cosmicCult = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private RottingSystem _rotting = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicGlyphConversionComponent, TryActivateGlyphEvent>(OnConversionGlyph);
        SubscribeLocalEvent<CosmicGlyphConversionComponent, CheckGlyphConditionsEvent>(OnCheckGlyphConditions);
    }

    private void OnCheckGlyphConditions(Entity<CosmicGlyphConversionComponent> uid, ref CheckGlyphConditionsEvent args)
    {
        var possibleTargets = _cosmicGlyph.GetTargetsNearGlyph(uid,
            uid.Comp.ConversionRange,
            entity => _cosmicCult.EntityIsCultist(entity));

        if (possibleTargets.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-glyph-conditions-not-met"), uid, args.User);
            args.Cancel();
            return;
        }

        if (possibleTargets.Count > 1)
        {
            _popup.PopupEntity(Loc.GetString("cult-glyph-too-many-targets"), uid, args.User);
            args.Cancel();
            return;
        }
        foreach (var target in possibleTargets)
        {
            if (!_mind.TryGetMind(target, out _, out _))
            {
                _popup.PopupEntity(Loc.GetString("cult-glyph-target-mindless"), uid, args.User);
                args.Cancel();
                return;
            }
        }
    }

    private void OnConversionGlyph(Entity<CosmicGlyphConversionComponent> uid, ref TryActivateGlyphEvent args)
    {
        var ev = new CheckGlyphConditionsEvent(args.User, args.Cultists);
        RaiseLocalEvent(uid, ref ev);
        if (ev.Cancelled)
        {
            args.Cancel();
            return;
        }

        var possibleTargets = _cosmicGlyph.GetTargetsNearGlyph(uid, uid.Comp.ConversionRange, entity => _cosmicCult.EntityIsCultist(entity));

        foreach (var target in possibleTargets)
        {
            // Claw Command: the dead can't be converted, so killing someone isn't a shortcut to a new cultist.
            if (_mobState.IsDead(target))
            {
                _popup.PopupEntity(Loc.GetString("cult-glyph-target-dead"), uid, args.User);
                args.Cancel();
                continue;
            }
            if (_rotting.IsRotten(target)) // Prevents using space corpses.
            {
                _popup.PopupEntity(Loc.GetString("cult-glyph-target-rotting"), uid, args.User);
                args.Cancel();
                continue;
            }
            var convEv = new BeforeConversionEvent(target);
            RaiseLocalEvent(target, ref convEv, true);
            if (convEv.Blocked)
            {
                _popup.PopupEntity(Loc.GetString("cult-glyph-target-immune"), uid, args.User);
                args.Cancel();
            }
            else if (HasComp<BibleUserComponent>(target))
            {
                _popup.PopupEntity(Loc.GetString("cult-glyph-target-chaplain"), uid, args.User);
                args.Cancel();
            }
            else if (uid.Comp.NegateProtection == false && HasComp<MindShieldComponent>(target))
            {
                _popup.PopupEntity(Loc.GetString("cult-glyph-target-mindshield"), uid, args.User);
                args.Cancel();
            }
            else
            {
                _stun.TryUpdateStunDuration(target, TimeSpan.FromSeconds(4f));
                _damageable.TryChangeDamage(target.Owner, uid.Comp.ConversionHeal, true); // Claw Command: partial heal instead of a full rejuvenate
                _cultRule.CosmicConversion(uid, target);
            }
        }
    }
}
