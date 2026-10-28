using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Utility;

namespace Content.Shared._ClawCommand.Religion;

public sealed partial class HandHeldArmorSystem : EntitySystem
{
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandHeldArmorComponent, GotEquippedHandEvent>(OnHandEquipped);
        SubscribeLocalEvent<HandHeldArmorComponent, HeldRelayedEvent<CoefficientQueryEvent>>(OnCoefficientQuery);
        SubscribeLocalEvent<HandHeldArmorComponent, HeldRelayedEvent<DamageModifyEvent>>(OnDamageModify);
        SubscribeLocalEvent<HandHeldArmorComponent, GetVerbsEvent<ExamineVerb>>(OnHeldArmorVerbExamine);
    }

    private void OnHandEquipped(Entity<HandHeldArmorComponent> armor, ref GotEquippedHandEvent args)
    {
        // The relayed events below don't carry the holder, so remember it here.
        armor.Comp.Holder = args.User;
        Dirty(armor);
    }

    private bool IsActive(Entity<HandHeldArmorComponent> armor)
    {
        return armor.Comp.Holder.HasValue && !_whitelist.IsWhitelistFail(armor.Comp.Whitelist, armor.Comp.Holder.Value);
    }

    private void OnCoefficientQuery(Entity<HandHeldArmorComponent> armor, ref HeldRelayedEvent<CoefficientQueryEvent> args)
    {
        if (!IsActive(armor))
            return;

        foreach (var armorCoefficient in armor.Comp.Modifiers.Coefficients)
        {
            args.Args.DamageModifiers.Coefficients[armorCoefficient.Key] =
                args.Args.DamageModifiers.Coefficients.TryGetValue(armorCoefficient.Key, out var coefficient)
                ? coefficient * armorCoefficient.Value
                : armorCoefficient.Value;
        }
    }

    private void OnDamageModify(Entity<HandHeldArmorComponent> armor, ref HeldRelayedEvent<DamageModifyEvent> args)
    {
        if (!IsActive(armor))
            return;

        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, armor.Comp.Modifiers);
    }

    private void OnHeldArmorVerbExamine(Entity<HandHeldArmorComponent> armor, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !armor.Comp.ShowArmorOnExamine)
            return;

        var examineMarkup = _whitelist.IsWhitelistPassOrNull(armor.Comp.Whitelist, args.User)
            ? GetHeldArmorExamine(armor.Comp)
            : GetHeldArmorFailExamine(armor.Comp);

        _examine.AddDetailedExamineVerb(args, armor, examineMarkup,
            Loc.GetString("armor-examinable-verb-text"), "/Textures/Interface/VerbIcons/dot.svg.192dpi.png",
            Loc.GetString("armor-examinable-verb-message"));
    }

    private FormattedMessage GetHeldArmorExamine(HandHeldArmorComponent armor)
    {
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("held-armor-examine"));

        foreach (var coefficientArmor in armor.Modifiers.Coefficients)
        {
            msg.PushNewline();

            var armorType = Loc.GetString("armor-damage-type-" + coefficientArmor.Key.Id.ToLower());
            msg.AddMarkupOrThrow(Loc.GetString("armor-coefficient-value",
                ("type", armorType),
                ("value", MathF.Round((1f - coefficientArmor.Value) * 100, 1))
            ));
        }

        return msg;
    }

    private FormattedMessage GetHeldArmorFailExamine(HandHeldArmorComponent armor)
    {
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("held-armor-fail-examine"));

        if (armor.WhitelistFailMessage != null)
        {
            msg.PushNewline();
            msg.AddMarkupOrThrow(Loc.GetString("held-armor-whitelist-fail", ("reason", Loc.GetString(armor.WhitelistFailMessage))));
        }

        return msg;
    }
}
