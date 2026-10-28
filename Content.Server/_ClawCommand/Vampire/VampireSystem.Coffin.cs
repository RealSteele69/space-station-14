// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Vampire;
using Content.Shared.Storage.Components;

namespace Content.Server._ClawCommand.Vampire;

public sealed partial class VampireSystem
{
    private void InitializeCoffin()
    {
        SubscribeLocalEvent<EntityStorageComponent, StorageAfterCloseEvent>(OnStorageClosed);
    }

    /// <summary>
    /// Any closet/crate can become a vampire's coffin: close the lid while resting inside one you don't
    /// already own, and it's yours - no vote, no fuss.
    /// </summary>
    private void OnStorageClosed(Entity<EntityStorageComponent> storage, ref StorageAfterCloseEvent args)
    {
        if (args.User is not { } user
            || !TryComp<VampireComponent>(user, out var vampireComp)
            || (vampireComp.ClaimedCoffin is { } claimed && !TerminatingOrDeleted(claimed)) // a destroyed coffin frees the claim
            || HasComp<VampireCoffinComponent>(storage)
            || Transform(user).ParentUid != storage.Owner)
            return;

        EnsureComp<VampireCoffinComponent>(storage).Owner = user;
        vampireComp.ClaimedCoffin = storage.Owner;

        _popup.PopupEntity(Loc.GetString("vampire-coffin-claimed"), user, user);
    }

    /// <summary>
    /// Is this vampire currently resting inside their own coffin with the lid shut? That's the one place
    /// they heal fast and free.
    /// </summary>
    private bool IsRestingInOwnCoffin(Entity<VampireComponent> vampire)
    {
        if (vampire.Comp.ClaimedCoffin is not { } coffin || !Exists(coffin))
            return false;

        return Transform(vampire).ParentUid == coffin
               && TryComp<EntityStorageComponent>(coffin, out var storage)
               && !storage.Open;
    }
}
