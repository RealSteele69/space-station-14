using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.TapeRecorder;

[Serializable, NetSerializable]
public enum TapeRecorderVisuals : byte
{
    Mode,
    TapeInserted
}

[Serializable, NetSerializable]
public enum TapeRecorderMode : byte
{
    Stopped,
    Recording,
    Playing,
    Rewinding
}

[Serializable, NetSerializable]
public enum TapeRecorderUIKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class ChangeModeTapeRecorderMessage(TapeRecorderMode mode) : BoundUserInterfaceMessage
{
    public TapeRecorderMode Mode = mode;
}

[Serializable, NetSerializable]
public sealed class PrintTapeRecorderMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class EraseTapeRecorderMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class TapeRecorderState : BoundUserInterfaceState
{
    public bool HasCasette;
    public bool HasData;
    public float CurrentTime;
    public float MaxTime;
    public string CassetteName;
    public TimeSpan PrintCooldown;
    public bool CanPrint;
    public bool CanErase;

    public TapeRecorderState(
        bool hasCasette,
        bool hasData,
        float currentTime,
        float maxTime,
        string cassetteName,
        TimeSpan printCooldown,
        bool canPrint,
        bool canErase)
    {
        CanPrint = canPrint;
        CanErase = canErase;
        HasCasette = hasCasette;
        HasData = hasData;
        CurrentTime = currentTime;
        MaxTime = maxTime;
        CassetteName = cassetteName;
        PrintCooldown = printCooldown;
    }
}
