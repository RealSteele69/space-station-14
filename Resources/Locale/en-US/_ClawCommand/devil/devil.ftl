# --- devil
devil-roundend-name = Devil
objective-issuer-devil = Devil

roundend-prepend-devil-contracts = Someone has made [color=red]{$number}[/color] successful contracts.
roundend-prepend-devil-contracts-named = [color=white]{$name}[/color] has made [color=red]{$number}[/color] successful contracts.

devil-component-examined = [color=darkred]{CAPITALIZE(POSS-ADJ($target))} eyes glow a faint red.[/color]
condemned-component-examined = [color=chartreuse]{CAPITALIZE($target)}'s eyes are hollow and soulless.[/color]

devil-role-greeting = You are a devil from the depths of hell,
  who has possessed the body of {$playerName}
  Corrupt mortals and collect souls through the use of contracts.
  It is not in your interest to do tasks unrelated to your deals, they are beneath you.
  Your true name is {$trueName} - Try to keep it a secret, will you?

devil-true-name-heard = The act of {$speaker} commanding {$target}'s true name compels {POSS-ADJ($target)} to stop.
devil-true-name-heard-chaplain = The act of {$speaker}'s holy words commanding their true name burns away at {$target}'s very soul.

objective-condition-contract-title = Collect Souls
objective-condition-contract-description = Collect as many souls as possible.

objective-condition-weight-title = Make profitable deals.
objective-condition-weight-description = Ensure the deals benefit you as much as possible.

hand-shake-prompt-verb = Offer handshake to {$target}
hand-shake-accept-verb = Shake {$target}'s hand

handshake-success = You feel slightly lighter as you shake {$user}'s hand.
handshake-fail =  You shake {POSS-ADJ($user)} hand, but nothing happens!

handshake-offer-popup = {$user} offers {POSS-ADJ($user)} hand to you, a sly grin on {POSS-ADJ($user)} face.
handshake-offer-popup-self = You outstretch your hand to {$target}, a sly grin on your face.

weaktoholy-component-bible-sizzle = {$target}'s skin is singed with the {$item}!
devil-banish-begin = {$user} begins to perform an exorcism on {$target}!
devil-exorcised = The ground cracks and splinters beneath {$target} as they are forcefully returned to hell!

invalid-possession-target = You cannot possess this target!

devil-power-level-increase-weak = Bits of flame flare out from your fingertips. Your connection with hell has strengthened.
devil-power-level-increase-moderate = You feel your power surge. Your connection with hell has strengthened.
devil-power-level-increase-powerful = You've almost completed your tasks here. Just a few more souls.

# --- grip
devil-speech-grip = Stupete et cadite!!

# --- cheatdeath
cheat-death-component-examined = [color=red] Something tells you {SUBJECT($target)} has no fear of death. [/color]
cheat-death-component-remaining-revives = { $amount ->
[0] You have no revives left.
[one] You have one revive left.
*[other] You have {$amount} revives left.
}

cheat-death-component-remaining-revives-unlimited = You have an unlimited amount of revives.

action-cheat-death-fail-no-lives = You aren't slipping away this time.
action-cheat-death-fail-not-dead = You can't cheat death while alive!
action-cheat-death-holy-damage = Divine intervention prevents your revival.

action-cheated-death-dead = {$name} jerks awake. Bones mended, wounds sewn, organs arranged ever so perfectly.
action-cheated-death-alive = {$name} violently shakes, all manners of wounds disappearing in a flash of flames.

# --- contract
devil-contract-contractee = contractee
devil-contract-contractor = contractor

devil-contract-early-sign-failed = The contractor must wait for the contractee to sign first!
devil-contract-no-soul-sign-failed = You do not have a soul to sell!
devil-sign-invalid-user = You do not have the power to fulfill this contract!
devil-contract-mind-shielded-failed = A voice comes from your Nanotrasen™ mindshield. "Nice try."

contract-summon-flavor = {$name}'s hand bursts into flames before revealing a black roll of parchment...
burn-contract-prompt = Burn contract.
burn-contract-popup-success = The contract bursts into flames! It seems someones fate has been sealed...
burn-contract-popup-fail = You cannot burn a contract during a deal!


contract-victim-signed = A chill runs down your spine as you lift the pen...
contract-devil-signed = The contract pulses with dark energy...
contract-soul-added = You gain another soul in your possession...
contract-uneven-odds = These odds aren't fair. You need {$number} more contract weight.
devil-contract-examined = This contract has a weight of: {$weight}.

devil-deal-time-ran-out = Your vision goes black as your muscles fail you. You've run out of time.

revival-contract-accepted = You feel your connection to your body strengthen...
revival-contract-rejected = You reject the unholy offer.
revival-contract-expired = Times up.
revival-contract-prompt = {$offerer} offers to restore your life for a price... Do you accept?
revival-contract-use-success = You offer {$target} a deal no man could refuse...
revival-contract-use-failed = You cannot offer {$target} this deal.

revival-contract-prompt-reject = Reject Offer
revival-contract-prompt-accept = Accept Offer

revival-contract-menu-title = Contract From the Depths
revival-contract-menu-prompt = Sell your soul for a second chance?

default-contract-content = This binding contract made this day between:

            - The Infernal Party (Hereinafter "Contractor")
            - The Mortal Party (Hereinafter "Contractee")

            {"["}bold]Clauses of Binding:{"["}/bold]
            {"["}italic]The following parties give up the following possessions{"["}/italic{"]"}

            Contractee: Soul Ownership
            Contractee: Death

            {"["}italic]All clauses come into effect upon signing of this paper by both parties.{"["}/italic]
            {"["}italic]{"["}color=#ac3a0d]Clauses may not be broken. Ever.{"["}/color]{"["}/italic]

# --- antags
roles-antag-devil-name = Devil
roles-antag-devil-description = Use your wit and silver tongue to trick crew members into devilish deals.

# --- condemned
condemned-start = The ground splinters and cracks around {$target} as an unholy force approaches!

# --- possession
possession-fail-ghoul = There's no mind for you to possess!
possession-fail-devil = You cannot possess another devil!
possession-fail-ghost = There's no mind for you to possess!
possession-fail-temporary = There's no mind for you to possess!
possession-fail-target-dead = There's no mind for you to possess!
possession-fail-target-shielded = A pesky shield prevents you from prying at their mind!
possession-fail-target-already-possessed = Someone else has already taken over this mind...
possession-fail-target-chaplain = A holy force prevents you from possessing this target!
possession-fail-target-immune = An unusual barrier blocks your possession attempt!
possession-fail-target-polymorphed = This form is too unstable to possess!

possession-popup-others = {$target}'s limbs contort in a strange way, {POSS-ADJ($target)} eyes rolling back in {POSS-ADJ($target)} head.
possession-popup-self = Your soul is forced out of your body!
possession-end-popup = {$target} seizes up and falls limp to the ground.

possessed-component-examined = You have {$timeremaining} seconds left in this body.

# --- Claw Command additions
revival-contract-no-mind = There's no soul left in this body to bargain with.
nullrod-spelldenial-popup = THE POWER OF GOD COMPELS YOU!
role-subtype-devil = Devil
admin-verb-text-make-devil = Make Devil
admin-verb-make-devil = Make the target into a devil.
guide-entry-devil = Devil
guide-entry-devil-clauses = Devil Clauses
