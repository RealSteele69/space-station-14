using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._ClawCommand.Religion;

/// <summary>
/// Used for armor that only protects while held in hand, such as the chaplain's bible.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(HandHeldArmorSystem))]
public sealed partial class HandHeldArmorComponent : Component
{
    /// <summary>
    /// The entity currently holding this item, so the whitelist/blacklist can be checked later.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Holder;

    /// <summary>
    /// The damage reduction applied while held by a valid holder.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public DamageModifierSet Modifiers = default!;

    /// <summary>
    /// If true, the holder can examine the item to see its protection values.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ShowArmorOnExamine = true;

    /// <summary>
    /// The holder must pass this whitelist for the armor to be active. Null skips the check.
    /// </summary>
    /// <example>A bible only protects the holder if they're a bible user.</example>
    [DataField, AutoNetworkedField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Loc string shown on examine if the holder fails the whitelist.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? WhitelistFailMessage;
}
