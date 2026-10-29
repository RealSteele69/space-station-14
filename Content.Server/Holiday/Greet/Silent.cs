using Content.Server.Holiday.Interfaces;
using JetBrains.Annotations;

namespace Content.Server.Holiday.Greet
{
    /// <summary>
    /// Claw Command - a holiday that doesn't send a round-start server announcement at all, for
    /// holidays whose only effect is a Celebrate (e.g. spawning something) with nothing to say.
    /// </summary>
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class Silent : IHolidayGreet
    {
        public string Greet(HolidayPrototype holiday)
        {
            return string.Empty;
        }
    }
}
