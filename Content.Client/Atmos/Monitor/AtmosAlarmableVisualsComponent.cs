using Content.Shared.Atmos.Monitor;

namespace Content.Client.Atmos.Monitor;

[RegisterComponent]
public sealed partial class AtmosAlarmableVisualsComponent : Component
{
    [DataField("layerMap")]
    public string LayerMap { get; private set; } = string.Empty;

    [DataField("alarmStates")]
    public Dictionary<AtmosAlarmType, string> AlarmStates = new();

    [DataField("hideOnDepowered")]
    public List<string>? HideOnDepowered;

    // eh...
    [DataField("setOnDepowered")]
    public Dictionary<string, string>? SetOnDepowered;

    /// <summary>
    /// Alarm types for which the layer should be hidden instead of shown, letting whatever
    /// is drawn underneath it (e.g. an AlertLevelDisplay layer) show through. Used so an idle
    /// alarm doesn't permanently paint over a lower layer that shares the same states.
    /// </summary>
    [DataField("hiddenAlarmTypes")]
    public HashSet<AtmosAlarmType>? HiddenAlarmTypes;
}
