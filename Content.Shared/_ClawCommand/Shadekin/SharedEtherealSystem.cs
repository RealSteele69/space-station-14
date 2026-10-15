using Content.Shared._ClawCommand.Shadekin.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Mobs;
using Content.Shared.Temperature; // Claw Command
using Robust.Shared.Physics.Events;
using Robust.Shared.Network;


namespace Content.Shared._ClawCommand.Shadekin;

public abstract partial class SharedEtherealSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EtherealComponent, MapInitEvent>(OnStartup);
        SubscribeLocalEvent<EtherealComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<EtherealComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<EtherealComponent, BeforeThrowEvent>(OnBeforeThrow);
        SubscribeLocalEvent<EtherealComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<EtherealComponent, ShotAttemptedEvent>(OnShootAttempt);
        SubscribeLocalEvent<EtherealComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EtherealComponent, PreventCollideEvent>(PreventCollision);
        SubscribeLocalEvent<EtherealComponent, BeforeHeatExchangeEvent>(OnBeforeHeatExchange); // Claw Command
    }

    /// <summary>
    /// Claw Command - Ethereals are phased out into the Dark and don't trade heat with the world around them.
    /// This used to be done by zeroing TemperatureComponent.AtmosTemperatureTransferEfficiency, which upstream
    /// removed when temperature moved onto heat containers; every conductance-respecting exchange (atmos
    /// exposure included) now goes through this event instead.
    /// </summary>
    private void OnBeforeHeatExchange(Entity<EtherealComponent> ent, ref BeforeHeatExchangeEvent args)
    {
        args.HeatTransferModifier = 0f;
    }

    public virtual void OnStartup(EntityUid uid, EtherealComponent component, MapInitEvent args)
    {
    }

    public virtual void OnShutdown(EntityUid uid, EtherealComponent component, ComponentShutdown args)
    {
    }

    private void OnMobStateChanged(EntityUid uid, EtherealComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Critical
            || args.NewMobState == MobState.Dead)
        {
            SpawnAtPosition("ShadekinShadow", Transform(uid).Coordinates);
            RemComp(uid, component);
        }
    }

    private void OnShootAttempt(Entity<EtherealComponent> ent, ref ShotAttemptedEvent args)
    {
        args.Cancel();
    }

    private void OnAttackAttempt(EntityUid uid, EtherealComponent component, AttackAttemptEvent args)
    {
        if (HasComp<EtherealComponent>(args.Target))
            return;

        args.Cancel();
    }

    private void OnBeforeThrow(Entity<EtherealComponent> ent, ref BeforeThrowEvent args)
    {
        var thrownItem = args.ItemUid;

        // Raise an AttemptPacifiedThrow event and rely on other systems to check
        // whether the candidate item is OK to throw:
        var ev = new AttemptPacifiedThrowEvent(thrownItem, ent);
        RaiseLocalEvent(thrownItem, ref ev);
        if (!ev.Cancelled)
            return;

        args.Cancelled = true;
    }

    private void OnInteractionAttempt(EntityUid uid, EtherealComponent component, ref InteractionAttemptEvent args)
    {
        if (HasComp<EtherealComponent>(args.Target))
            return;

        args.Cancelled = true;
    }

    private void PreventCollision(EntityUid uid, EtherealComponent component, ref PreventCollideEvent args)
    {
        if (!_net.IsClient)
            args.Cancelled = true;
    }
}
