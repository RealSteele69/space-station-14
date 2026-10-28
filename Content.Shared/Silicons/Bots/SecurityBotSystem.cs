using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Contraband;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Security.Components;
using Content.Shared.Silicon.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.Silicons.Bots;

/// <summary>
/// Handles the judgement, stunning and cuffing logic shared by security bots.
/// Actual movement/targeting/combat is driven by HTN prototypes and NPC operators on the server;
/// this system just answers "is this a valid/wanted target" and performs the cuffing itself so
/// that both the targeting consideration and the server-only operators can share one source of truth.
/// </summary>
public sealed partial class SecurityBotSystem : EntitySystem
{
    [Dependency] private SharedCuffableSystem _cuffable = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SecurityBotComponent, GotEmaggedEvent>(OnEmagged);
    }

    private void OnEmagged(Entity<SecurityBotComponent> bot, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(bot, EmagType.Interaction))
            return;

        _popup.PopupEntity(Loc.GetString("security-bot-emagged"), bot, PopupType.LargeCaution);
        args.Handled = true;
    }

    /// <summary>
    /// Baseline eligibility: alive, not another bot/borg, not already fully restrained.
    /// Doesn't consider whether they're actually wanted.
    /// </summary>
    public bool IsValidTarget(Entity<SecurityBotComponent?> bot, EntityUid target)
    {
        if (target == bot.Owner)
            return false;

        if (HasComp<SiliconComponent>(target))
            return false;

        if (_mobState.IsDead(target))
            return false;

        if (TryComp<CuffableComponent>(target, out var cuffable) && _cuffable.IsCuffed((target, cuffable)))
            return false;

        return true;
    }

    /// <summary>
    /// The core judgement call: should this bot treat <paramref name="target"/> as wanted?
    /// </summary>
    public bool IsWanted(Entity<SecurityBotComponent?> bot, EntityUid target)
    {
        if (!Resolve(bot, ref bot.Comp, false))
            return false;

        if (!IsValidTarget(bot, target))
            return false;

        // Gone rogue: no more judgement, everyone still standing is a target.
        if (HasComp<EmaggedComponent>(bot.Owner))
            return true;

        if (bot.Comp.CheckRecords
            && TryComp<CriminalRecordComponent>(target, out var record)
            && (record.StatusIcon == "SecurityIconWanted" || record.StatusIcon == "SecurityIconHostile"))
        {
            return true;
        }

        if (bot.Comp.CheckId && !_idCard.TryFindIdCard(target, out _))
            return true;

        if (bot.Comp.CheckWeapons && IsCarryingContraband(target))
            return true;

        return false;
    }

    private bool IsCarryingContraband(EntityUid target)
    {
        foreach (var held in _hands.EnumerateHeld(target))
        {
            if (HasComp<ContrabandComponent>(held))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to zip-tie an incapacitated target using a throwaway tool entity that carries
    /// the reusable (RemoveOnUse: false) side of HandcuffComponent, so the bot never runs out
    /// of cuffs and never needs hands of its own.
    /// </summary>
    public bool TryCuff(Entity<SecurityBotComponent?> bot, EntityUid target)
    {
        if (!Resolve(bot, ref bot.Comp, false))
            return false;

        if (!TryComp<CuffableComponent>(target, out var cuffable))
            return false;

        if (_cuffable.IsCuffed((target, cuffable)))
            return true; // already done

        if (!_interaction.InRangeUnobstructed(bot.Owner, target))
            return false;

        var tool = Spawn(bot.Comp.CuffToolPrototype, Transform(target).Coordinates);

        var success = false;
        if (TryComp<HandcuffComponent>(tool, out var handcuffComp))
            success = _cuffable.TryAddNewCuffs(target, bot.Owner, tool, cuffable, handcuffComp);

        QueueDel(tool);

        if (success)
            _audio.PlayPvs(bot.Comp.CuffSound, bot);

        return success;
    }

    public bool ShouldReacquire(Entity<SecurityBotComponent?> bot, EntityUid target)
    {
        if (!Resolve(bot, ref bot.Comp, false))
            return true;

        if (bot.Comp.LastPerp != target)
            return true;

        return _timing.CurTime > bot.Comp.LastPerpLostAt + bot.Comp.ReacquireCooldown;
    }

    public void MarkPerpSeen(Entity<SecurityBotComponent?> bot, EntityUid target)
    {
        if (!Resolve(bot, ref bot.Comp, false))
            return;

        bot.Comp.LastPerp = target;
    }

    public void MarkPerpLost(Entity<SecurityBotComponent?> bot)
    {
        if (!Resolve(bot, ref bot.Comp, false))
            return;

        if (bot.Comp.LastPerp != null)
            bot.Comp.LastPerpLostAt = _timing.CurTime;
    }

    public bool ShouldHandcuff(Entity<SecurityBotComponent?> bot)
    {
        return Resolve(bot, ref bot.Comp, false) && bot.Comp.HandcuffTargets;
    }

    public bool ShouldDeclareArrests(Entity<SecurityBotComponent?> bot)
    {
        return Resolve(bot, ref bot.Comp, false) && bot.Comp.DeclareArrests;
    }
}
