using Content.Server.Chat.Systems;
using Content.Server.Zombies;
using Content.Shared._ClawCommand.CCVar;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Zombies;
using Robust.Shared.Configuration;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.Zombies;

/// <summary>
/// Gives infected entities a small chance to develop a <see cref="CriticalZombieInfectionComponent"/>,
/// then runs its symptoms and turns them when the timer runs out.
/// </summary>
/// <remarks>
/// Initial infected are skipped, they already pick their own moment with the zombify self action.
/// </remarks>
public sealed partial class CriticalZombieInfectionSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ZombieSystem _zombie = default!;

    private static readonly TimeSpan RollInterval = TimeSpan.FromSeconds(1);

    private float _chance;
    private float _window;
    private TimeSpan _nextRoll;

    private readonly List<EntityUid> _toUpdate = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CriticalZombieInfectionComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CriticalZombieInfectionComponent, ExaminedEvent>(OnExamined);

        Subs.CVar(_cfg, ClawCCVars.ZombieCriticalInfectionChance, value => _chance = value, true);
        Subs.CVar(_cfg, ClawCCVars.ZombieCriticalInfectionWindow, value => _window = value, true);
    }

    private void OnMapInit(Entity<CriticalZombieInfectionComponent> ent, ref MapInitEvent args)
    {
        var now = _timing.CurTime;
        var duration = _random.NextDouble(ent.Comp.MinDuration.TotalSeconds, ent.Comp.MaxDuration.TotalSeconds);

        ent.Comp.StartTime = now;
        ent.Comp.TurnTime = now + TimeSpan.FromSeconds(duration);
        ent.Comp.NextSymptom = now + GetSymptomDelay(ent.Comp, 0f);
    }

    private void OnExamined(Entity<CriticalZombieInfectionComponent> ent, ref ExaminedEvent args)
    {
        // Pale from any distance, the twitching only up close.
        var message = args.IsInDetailsRange ? ent.Comp.ExamineCloseMessage : ent.Comp.ExamineFarMessage;
        args.PushMarkup(Loc.GetString(message, ("target", Identity.Entity(ent, EntityManager))));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        if (now >= _nextRoll)
        {
            _nextRoll = now + RollInterval;
            RollCriticalInfections();
        }

        _toUpdate.Clear();
        var query = EntityQueryEnumerator<CriticalZombieInfectionComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            _toUpdate.Add(uid);
        }

        // Zombifying and curing add and remove a pile of components, so don't do it mid-enumeration.
        foreach (var uid in _toUpdate)
        {
            if (!TryComp<CriticalZombieInfectionComponent>(uid, out var comp))
                continue;

            UpdateInfection((uid, comp), now);
        }
    }

    private void RollCriticalInfections()
    {
        if (_chance <= 0f || _window <= 0f)
            return;

        // Spread the chance over the window, so it comes out to _chance per _window per entity.
        var prob = 1f - MathF.Pow(1f - Math.Clamp(_chance, 0f, 1f), (float) RollInterval.TotalSeconds / _window);

        _toUpdate.Clear();
        var query = EntityQueryEnumerator<PendingZombieComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState))
        {
            if (_mobState.IsDead(uid, mobState)
                || HasComp<CriticalZombieInfectionComponent>(uid)
                || HasComp<IncurableZombieComponent>(uid)
                || HasComp<ZombieComponent>(uid))
                continue;

            if (_random.Prob(prob))
                _toUpdate.Add(uid);
        }

        foreach (var uid in _toUpdate)
        {
            AddComp<CriticalZombieInfectionComponent>(uid);
        }
    }

    private void UpdateInfection(Entity<CriticalZombieInfectionComponent> ent, TimeSpan now)
    {
        var (uid, comp) = ent;

        // No PendingZombie means it was cured (Ambuzol), or they already turned some other way.
        if (!HasComp<PendingZombieComponent>(uid))
        {
            if (!HasComp<ZombieComponent>(uid))
                _popup.PopupEntity(Loc.GetString(comp.CuredMessage), uid, uid, PopupType.Medium);

            RemComp(uid, comp);
            return;
        }

        if (now >= comp.TurnTime)
        {
            RemComp(uid, comp);
            _zombie.ZombifyEntity(uid);
            return;
        }

        if (!comp.FinalWarningShown && now >= comp.TurnTime - comp.FinalWarning)
        {
            comp.FinalWarningShown = true;
            _popup.PopupEntity(Loc.GetString(comp.FinalMessage), uid, uid, PopupType.LargeCaution);
            _jitter.DoJitter(uid, comp.FinalWarning, true, comp.MaxShakeAmplitude, 8f);
        }

        if (now < comp.NextSymptom)
            return;

        var progress = GetProgress(comp, now);
        comp.NextSymptom = now + GetSymptomDelay(comp, progress);
        DoSymptom(ent, progress);
    }

    private void DoSymptom(Entity<CriticalZombieInfectionComponent> ent, float progress)
    {
        var (uid, comp) = ent;

        var (messages, popupType) = progress switch
        {
            < 1f / 3f => (comp.EarlyMessages, PopupType.SmallCaution),
            < 2f / 3f => (comp.MiddleMessages, PopupType.MediumCaution),
            _ => (comp.LateMessages, PopupType.MediumCaution),
        };

        if (messages.Count > 0)
            _popup.PopupEntity(Loc.GetString(_random.Pick(messages)), uid, uid, popupType);

        if (_random.Prob(MathHelper.Lerp(comp.MinCoughChance, comp.MaxCoughChance, progress)))
            _chat.TryEmoteWithChat(uid, comp.CoughEmote.Id);

        if (_random.Prob(MathHelper.Lerp(comp.MinShakeChance, comp.MaxShakeChance, progress)))
        {
            var duration = MathHelper.Lerp((float) comp.MinShakeDuration.TotalSeconds, (float) comp.MaxShakeDuration.TotalSeconds, progress);
            var amplitude = MathHelper.Lerp(comp.MinShakeAmplitude, comp.MaxShakeAmplitude, progress);
            _jitter.DoJitter(uid, TimeSpan.FromSeconds(duration), true, amplitude, MathHelper.Lerp(4f, 8f, progress));
        }
    }

    private static float GetProgress(CriticalZombieInfectionComponent comp, TimeSpan now)
    {
        var total = (comp.TurnTime - comp.StartTime).TotalSeconds;
        if (total <= 0)
            return 1f;

        return Math.Clamp((float) ((now - comp.StartTime).TotalSeconds / total), 0f, 1f);
    }

    private TimeSpan GetSymptomDelay(CriticalZombieInfectionComponent comp, float progress)
    {
        var seconds = MathHelper.Lerp((float) comp.MaxSymptomInterval.TotalSeconds, (float) comp.MinSymptomInterval.TotalSeconds, progress);
        return TimeSpan.FromSeconds(seconds * _random.NextFloat(0.75f, 1.25f));
    }
}
