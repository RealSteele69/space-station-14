using Content.Shared.Popups;
using Content.Shared.Revenant.Components;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Revenant;

public abstract partial class SharedRevealRevenantOnCollideSystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    private static readonly ProtoId<StatusEffectPrototype> CorporealStatusId = "Corporeal";
    private static readonly ProtoId<StatusEffectPrototype> StunStatusId = "Stun";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevealRevenantOnCollideComponent, StartCollideEvent>(OnCollideStart);
    }

    private void OnCollideStart(Entity<RevealRevenantOnCollideComponent> ent, ref StartCollideEvent args)
    {
        if (!HasComp<RevenantComponent>(args.OtherEntity))
            return;

        var comp = ent.Comp;

        if (!string.IsNullOrEmpty(comp.PopupText) && !_status.HasStatusEffect(args.OtherEntity, CorporealStatusId))
            _popup.PopupEntity( // the reveal fixture only exists server-side, so PopupClient would never show
                Loc.GetString(comp.PopupText, ("revealer", ent.Owner), ("revenant", args.OtherEntity)),
                args.OtherEntity,
                args.OtherEntity
            );

        _status.TryAddStatusEffect<CorporealComponent>(args.OtherEntity, CorporealStatusId, comp.RevealTime, true);

        if (comp.StunTime != null && !_status.HasStatusEffect(args.OtherEntity, StunStatusId))
            _stun.TryUpdateStunDuration(args.OtherEntity, comp.StunTime.Value);
    }
}
