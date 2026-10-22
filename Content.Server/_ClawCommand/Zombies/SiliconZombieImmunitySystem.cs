using Content.Shared.Silicon.Components;
using Content.Shared.Zombies;

namespace Content.Server._ClawCommand.Zombies;

/// <summary>
/// Synthetic bodies can't carry the zombie virus. Every silicon (IPC and anything built on it) is made
/// zombie immune, even if its prototype forgot to add <see cref="ZombieImmuneComponent"/>.
/// Borgs already get it from BaseBorgChassis and synths from their species prototype.
/// </summary>
public sealed partial class SiliconZombieImmunitySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiliconComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<SiliconComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<ZombieImmuneComponent>(ent);
    }
}
