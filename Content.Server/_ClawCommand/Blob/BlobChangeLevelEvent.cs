using Content.Server._ClawCommand.Blob.GameTicking;
using Content.Server.GameTicking.Rules.Components;

namespace Content.Server._ClawCommand.Blob;

public sealed class BlobChangeLevelEvent : EntityEventArgs
{
    public EntityUid Station;
    public BlobStage Level;
}