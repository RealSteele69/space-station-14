using Content.Shared.GameTicking.Components;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Psionics.Glimmer;
using Content.Shared.Psionics.Glimmer;

namespace Content.Server.StationEvents.Events
{
    public sealed partial class GlimmerEventSystem : StationEventSystem<GlimmerEventComponent>
    {
        [Dependency] private GlimmerSystem _glimmerSystem = default!;

        protected override void Ended(Entity<GlimmerEventComponent> rule, ref GameRuleEndedEvent args)
        {
            var uid = rule.Owner;
            var component = rule.Comp;
            base.Ended(rule, ref args);

            var glimmerBurned = RobustRandom.Next(component.GlimmerBurnLower, component.GlimmerBurnUpper);
            _glimmerSystem.Glimmer -= glimmerBurned;

            var reportEv = new GlimmerEventEndedEvent(component.SophicReport, glimmerBurned);
            RaiseLocalEvent(reportEv);
        }
    }

    public sealed partial class GlimmerEventEndedEvent : EntityEventArgs
    {
        public string Message = "";
        public int GlimmerBurned = 0;

        public GlimmerEventEndedEvent(string message, int glimmerBurned)
        {
            Message = message;
            GlimmerBurned = glimmerBurned;
        }
    }
}
