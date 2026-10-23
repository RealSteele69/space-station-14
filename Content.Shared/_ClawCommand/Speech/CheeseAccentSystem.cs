using Content.Shared.Speech.EntitySystems;

namespace Content.Shared._ClawCommand.Speech;

/// <summary>
///     Claw Command - Applies the <c>cheese</c> word-replacement accent for <see cref="CheeseAccentComponent"/>.
/// </summary>
public sealed partial class CheeseAccentSystem : RelayAccentSystem<CheeseAccentComponent>
{
    private const string CheeseAccent = "cheese";

    [Dependency] private ReplacementAccentSystem _replacement = default!;

    public override string Accentuate(string message, Entity<CheeseAccentComponent>? ent = null)
    {
        return _replacement.ApplyReplacements(message, CheeseAccent, ent?.Owner);
    }
}
