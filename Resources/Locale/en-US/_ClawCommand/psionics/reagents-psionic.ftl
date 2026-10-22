psionic-regeneration-begin = {CAPITALIZE($entity)}'s eyes twitch under a wave of sweat.

psionic-regeneration-essence-sweat = {CAPITALIZE($entity)} sweats profusely.
psionic-regeneration-essence-veins = {CAPITALIZE($entity)}'s veins pulse.
psionic-regeneration-essence-breath = {CAPITALIZE($entity)} breathes rapidly.

# Claw Command - reagent-name-prometheum / reagent-desc-prometheum are deliberately NOT defined here.
# _ClawCommand/reagents/space_port.ftl already owns them, and that is the flatter _ClawCommand path.

# Guidebook text for the psionic reagent effects.
entity-effect-guidebook-chem-remove-psionic =
    { $chance ->
        [1] Permanently strips
        *[other] permanently strip
    } the metabolizer of any psionic powers

entity-effect-guidebook-chem-reroll-psionic =
    { $chance ->
        [1] Grants
        *[other] grant
    } the metabolizer another chance to awaken a psionic power
