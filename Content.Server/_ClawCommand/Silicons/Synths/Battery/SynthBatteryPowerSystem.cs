using System.Diagnostics.CodeAnalysis;
using Content.Shared._ClawCommand.Silicons.Synths.Battery;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.Silicons.Synths.Battery;

/// <summary>
/// Drains a synth's power cell over time, warns when it runs low, handles EMPs, and lets a cyborg
/// recharging station charge the cell while the synth is inside it.
/// </summary>
public sealed partial class SynthBatteryPowerSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _receiver = default!;
    [Dependency] private SynthBatteryAlertSystem _alerts = default!;
    [Dependency] private SynthBatteryEffectsSystem _effects = default!;
    [Dependency] private SynthBatterySystem _synthBattery = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SynthPowerCellComponent, ChargeChangedEvent>(OnCellChargeChanged);
        SubscribeLocalEvent<SynthPowerCellComponent, RefreshChargeRateEvent>(OnCellRefreshChargeRate);
        SubscribeLocalEvent<SynthBatteryComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<SynthBatteryComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<SynthBatteryComponent, EntGotRemovedFromContainerMessage>(OnRemoved);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SynthBatteryComponent>();
        while (query.MoveNext(out var uid, out var synth))
        {
            if (_timing.CurTime < synth.NextUpdate)
                continue;

            synth.NextUpdate = _timing.CurTime + synth.UpdateRate;

            if (_mobState.IsDead(uid))
                continue;

            UpdatePower((uid, synth), (float) synth.UpdateRate.TotalSeconds);
        }
    }

    private void UpdatePower(Entity<SynthBatteryComponent> ent, float delta)
    {
        if (!_synthBattery.TryGetBattery(ent.Owner, out var battery))
        {
            _alerts.UpdateBatteryAlert(ent);
            SetUnpowered(ent, true);
            return;
        }

        var cell = battery.Value.AsNullable();

        // The station's power or EMP state can change under us, so keep the rate fresh while inside one.
        var inCharger = TryGetCharger(ent.Owner, out _);
        if (inCharger || ent.Comp.InCharger)
        {
            ent.Comp.InCharger = inCharger;
            _battery.RefreshChargeRate(cell);
        }

        if (_battery.GetCharge(cell) <= 0f)
        {
            _alerts.UpdateBatteryAlert(ent);
            SetUnpowered(ent, true);
            return;
        }

        SetUnpowered(ent, false);

        if (ent.Comp.DrawRate <= 0f)
        {
            _alerts.UpdateBatteryAlert(ent);
            return;
        }

        var oldPercent = _battery.GetChargeLevel(cell) * 100f;
        if (_battery.ChangeCharge(cell, -ent.Comp.DrawRate * delta) < 0f)
            UpdateLowPowerWarning(ent, oldPercent, _battery.GetChargeLevel(cell) * 100f);
    }

    private void UpdateLowPowerWarning(Entity<SynthBatteryComponent> ent, float oldPercent, float newPercent)
    {
        foreach (var threshold in ent.Comp.WarningPercentages)
        {
            if (oldPercent <= threshold || newPercent > threshold)
                continue;

            if (ent.Comp.BatteryLowText is { } text)
                _popup.PopupEntity(Loc.GetString(text), ent, ent, PopupType.LargeCaution);

            PlaySound(ent, ent.Comp.BatteryLowSound);
            return;
        }
    }

    private void SetUnpowered(Entity<SynthBatteryComponent> ent, bool unpowered)
    {
        if (ent.Comp.Unpowered == unpowered)
            return;

        _effects.SetUnpowered(ent, unpowered);

        if (!unpowered)
            return;

        if (ent.Comp.BatteryDeadText is { } text)
            _popup.PopupEntity(Loc.GetString(text), ent, ent, PopupType.LargeCaution);

        PlaySound(ent, ent.Comp.BatteryDeadSound);
    }

    private void PlaySound(Entity<SynthBatteryComponent> ent, SoundSpecifier? sound)
    {
        if (sound != null)
            _audio.PlayPvs(sound, ent);
    }

    /// <summary>
    /// Finds the recharging station the synth is sitting in, if it's one that would charge it right now.
    /// The station only charges batteries it can see directly, so the synth's cell organ is handled here.
    /// </summary>
    private bool TryGetCharger(EntityUid synth, [NotNullWhen(true)] out Entity<ChargerComponent>? charger)
    {
        charger = null;

        if (!_container.TryGetContainingContainer((synth, null, null), out var container)
            || !TryComp(container.Owner, out ChargerComponent? chargerComp)
            || container.ID != chargerComp.SlotId
            || HasComp<EmpDisabledComponent>(container.Owner)
            || !chargerComp.Portable && !_receiver.IsPowered(container.Owner)
            || _whitelist.IsWhitelistFail(chargerComp.Whitelist, synth))
            return false;

        charger = (container.Owner, chargerComp);
        return true;
    }

    private void RefreshCharging(Entity<SynthBatteryComponent> ent)
    {
        if (!_synthBattery.TryGetBattery(ent.Owner, out var battery))
            return;

        ent.Comp.InCharger = TryGetCharger(ent.Owner, out _);
        _battery.RefreshChargeRate(battery.Value.AsNullable());
    }

    private void OnInserted(Entity<SynthBatteryComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        RefreshCharging(ent);
    }

    private void OnRemoved(Entity<SynthBatteryComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        RefreshCharging(ent);
    }

    private void OnCellRefreshChargeRate(Entity<SynthPowerCellComponent> ent, ref RefreshChargeRateEvent args)
    {
        if (!_synthBattery.TryGetSynth(ent.Owner, out var synth)
            || !TryGetCharger(synth.Owner, out var charger))
            return;

        args.NewChargeRate += charger.Value.Comp.ChargeRate;
    }

    private void OnCellChargeChanged(Entity<SynthPowerCellComponent> ent, ref ChargeChangedEvent args)
    {
        if (!_synthBattery.TryGetSynth(ent.Owner, out var synth) || _mobState.IsDead(synth.Owner))
            return;

        SetUnpowered(synth, args.CurrentCharge <= 0f);
        _alerts.UpdateBatteryAlert(synth);
    }

    private void OnEmpPulse(Entity<SynthBatteryComponent> ent, ref EmpPulseEvent args)
    {
        args.Affected = true;

        if (ent.Comp.EmpDamage is { } damage)
            _damageable.TryChangeDamage(ent.Owner, damage, origin: args.User);

        if (_synthBattery.TryGetBattery(ent.Owner, out var battery))
            _battery.UseCharge(battery.Value.AsNullable(), args.EnergyConsumption);

        if (ent.Comp.BatteryEmpText is { } text)
            _popup.PopupEntity(Loc.GetString(text), ent, ent, PopupType.LargeCaution);
    }
}
