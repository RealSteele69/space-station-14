using System.Linq;
using Content.Server._ClawCommand.Blob.Components;
using Content.Shared._ClawCommand.Blob.Components;
using Content.Server.Popups;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.Explosion.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Blob;

public sealed partial class BlobFactorySystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private MobStateSystem _mobState = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobFactoryComponent, BlobSpecialGetPulseEvent>(OnPulsed);
        SubscribeLocalEvent<BlobFactoryComponent, ProduceBlobbernautEvent>(OnProduceBlobbernaut);
        SubscribeLocalEvent<BlobFactoryComponent, DestructionEventArgs>(OnDestruction);

    }

    private void OnDestruction(EntityUid uid, BlobFactoryComponent component, DestructionEventArgs args)
    {
        if (TryComp<BlobbernautComponent>(component.Blobbernaut, out var blobbernautComponent))
        {
            blobbernautComponent.Factory = null;
        }
    }

    private void OnProduceBlobbernaut(EntityUid uid, BlobFactoryComponent component, ProduceBlobbernautEvent args)
    {
        if (component.Blobbernaut != null)
            return;

        if (!TryComp<BlobTileComponent>(uid, out var blobTileComponent) || blobTileComponent.Core == null)
            return;

        if (!TryComp<BlobCoreComponent>(blobTileComponent.Core, out var blobCoreComponent))
            return;

        var xform = Transform(uid);

        var blobbernaut = Spawn(component.BlobbernautId, xform.Coordinates);

        component.Blobbernaut = blobbernaut;
        if (TryComp<BlobbernautComponent>(blobbernaut, out var blobbernautComponent))
        {
            blobbernautComponent.Factory = uid;
            blobbernautComponent.Color = blobCoreComponent.ChemColors[blobCoreComponent.CurrentChem];
            Dirty(blobbernaut, blobbernautComponent);
        }
        if (TryComp<MeleeWeaponComponent>(blobbernaut, out var meleeWeaponComponent))
        {
            var blobbernautDamage = new DamageSpecifier();
            foreach (var keyValuePair in blobCoreComponent.ChemDamageDict[blobCoreComponent.CurrentChem].DamageDict)
            {
                blobbernautDamage.DamageDict.Add(keyValuePair.Key, keyValuePair.Value * blobCoreComponent.BlobbernautDamageMultiplier);
            }
            meleeWeaponComponent.Damage = blobbernautDamage;
        }
    }

    /// <summary>
    /// Units of the chemical in a pod's death cloud. Claw Command: the original used 30.
    /// </summary>
    private const int SmokeAmount = 20;

    private static readonly ProtoId<ReagentPrototype> Phlogiston = "Phlogiston";

    private static readonly ProtoId<ReagentPrototype> TearGas = "TearGas";

    private static readonly ProtoId<ReagentPrototype> Lexorin = "Lexorin";

    private static readonly ProtoId<ReagentPrototype> Mold = "Mold";

    private static readonly ProtoId<ReagentPrototype> Bicaridine = "Bicaridine";

    private static readonly ProtoId<ReagentPrototype> ChloralHydrate = "ChloralHydrate";

    private static readonly ProtoId<ReagentPrototype> PolytrinicAcid = "PolytrinicAcid";

    private static readonly ProtoId<ReagentPrototype> Ipecac = "Ipecac";

    private static readonly ProtoId<ReagentPrototype> Razorium = "Razorium";

    private static readonly ProtoId<ReagentPrototype> Fresium = "Fresium";

    private static readonly ProtoId<ReagentPrototype> Aluminium = "Aluminium";
    private static readonly ProtoId<ReagentPrototype> Iron = "Iron";
    private static readonly ProtoId<ReagentPrototype> Uranium = "Uranium";

    private void FillSmokeGas(Entity<BlobPodComponent> ent, BlobChemType currentChem)
    {
        var blobGas = EnsureComp<SmokeOnTriggerComponent>(ent).Solution;
        switch (currentChem)
        {
            case BlobChemType.BlazingOil:
                blobGas.AddSolution(new Solution(Phlogiston, FixedPoint2.New(SmokeAmount))
                {
                    Temperature = 1000
                },_prototypeManager);
                break;
            case BlobChemType.ReactiveSpines:
                blobGas.AddSolution(new Solution(Mold, FixedPoint2.New(SmokeAmount)),_prototypeManager);
                break;
            case BlobChemType.RegenerativeMateria:
                blobGas.AddSolution(new Solution(Bicaridine, FixedPoint2.New(SmokeAmount)),_prototypeManager);
                break;
            case BlobChemType.ExplosiveLattice:
                blobGas.AddSolution(new Solution(Lexorin, FixedPoint2.New(SmokeAmount))
                {
                    Temperature = 1000
                },_prototypeManager);
                break;
            case BlobChemType.ElectromagneticWeb:
                blobGas.AddSolution(new Solution(Aluminium, FixedPoint2.New(SmokeAmount / 3)){ CanReact = false },_prototypeManager);
                blobGas.AddSolution(new Solution(Iron, FixedPoint2.New(SmokeAmount / 3)){ CanReact = false },_prototypeManager);
                blobGas.AddSolution(new Solution(Uranium, FixedPoint2.New(SmokeAmount / 3)){ CanReact = false },_prototypeManager);
                break;
            default:
                blobGas.AddSolution(new Solution(TearGas, FixedPoint2.New(SmokeAmount)),_prototypeManager);
                break;
            case BlobChemType.ComatoseFiber:
                blobGas.AddSolution(new Solution(ChloralHydrate, FixedPoint2.New(SmokeAmount)),_prototypeManager);
                break;
            case BlobChemType.ChainCoating:
                blobGas.AddSolution(new Solution(Razorium, FixedPoint2.New(SmokeAmount)),_prototypeManager);
                break;
            case BlobChemType.SinewyTendons:
                blobGas.AddSolution(new Solution(Ipecac, FixedPoint2.New(SmokeAmount)),_prototypeManager);
                break;
            case BlobChemType.CorrosiveSlime:
                blobGas.AddSolution(new Solution(PolytrinicAcid, FixedPoint2.New(SmokeAmount)),_prototypeManager);
                break;
            case BlobChemType.CryogenicPoison:
                blobGas.AddSolution(new Solution(Fresium, FixedPoint2.New(SmokeAmount)),_prototypeManager);
                break;
        }
    }

    private void OnPulsed(EntityUid uid, BlobFactoryComponent component, BlobSpecialGetPulseEvent args)
    {
        if (!TryComp<BlobTileComponent>(uid, out var blobTileComponent) || blobTileComponent.Core == null)
            return;

        if (!TryComp<BlobCoreComponent>(blobTileComponent.Core, out var blobCoreComponent))
            return;

        // forget dead pods
        component.BlobPods = component.BlobPods.Where(b => !TerminatingOrDeleted(b) && _mobState.IsAlive(b)).ToList();
        var spawnLimit = blobCoreComponent.CurrentChem == BlobChemType.SinewyTendons
            ? component.SinewySpawnLimit
            : component.SpawnLimit;

        if (component.BlobPods.Count >= spawnLimit)
            return;

        if (component.Accumulator < component.AccumulateToSpawn)
        {
            component.Accumulator++;
            return;
        }

        var xform = Transform(uid);

        var pod = Spawn(component.Pod, xform.Coordinates);
        component.BlobPods.Add(pod);
        var blobPod = EnsureComp<BlobPodComponent>(pod);
        blobPod.Core = blobTileComponent.Core.Value;
        FillSmokeGas((pod,blobPod), blobCoreComponent.CurrentChem);

        //smokeOnTrigger.SmokeColor = blobCoreComponent.ChemColors[blobCoreComponent.CurrentChem];
        component.Accumulator = 0;
    }
}
