using Content.Server.Shuttles.Components;
using Content.Shared.CCVar;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    /// <summary>
    /// Sets a shuttle's inertial dampener mode and applies the resulting drag, unless the shuttle
    /// is actively in FTL transit, in which case the mode is stored and applied once it completes.
    /// </summary>
    public void SetInertiaDampening(EntityUid uid, InertiaDampeningMode mode, ShuttleComponent? shuttle = null)
    {
        if (!Resolve(uid, ref shuttle))
            return;

        shuttle.DampeningMode = mode;

        if (!HasComp<FTLComponent>(uid))
            shuttle.DampingModifier = GetDampingModifier(shuttle);

        _console.RefreshShuttleConsoles(uid);
    }

    public InertiaDampeningMode GetInertiaDampeningMode(EntityUid entity)
    {
        if (!TryComp(entity, out TransformComponent? xform) ||
            !TryComp<ShuttleComponent>(xform.GridUid, out var shuttle))
        {
            return InertiaDampeningMode.Dampened;
        }

        return shuttle.DampeningMode;
    }

    public float GetDampingModifier(ShuttleComponent shuttle)
    {
        return shuttle.DampeningMode switch
        {
            InertiaDampeningMode.Off => _cfg.GetCVar(CCVars.ShuttleDampeningOffStrength),
            InertiaDampeningMode.Anchored => _cfg.GetCVar(CCVars.ShuttleDampeningAnchoredStrength),
            _ => shuttle.BodyModifier,
        };
    }
}
