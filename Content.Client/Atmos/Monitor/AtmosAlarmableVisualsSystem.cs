using Content.Shared.Atmos.Monitor;
using Content.Shared.Power;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.Atmos.Monitor;

public sealed partial class AtmosAlarmableVisualsSystem : VisualizerSystem<AtmosAlarmableVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, AtmosAlarmableVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || !SpriteSystem.LayerMapTryGet((uid, args.Sprite), component.LayerMap, out var layer, false))
            return;

        if (!args.AppearanceData.TryGetValue(PowerDeviceVisuals.Powered, out var poweredObject) ||
            poweredObject is not bool powered)
        {
            return;
        }

        if (component.HideOnDepowered != null)
        {
            foreach (var visLayer in component.HideOnDepowered)
            {
                if (SpriteSystem.LayerMapTryGet((uid, args.Sprite), visLayer, out var powerVisibilityLayer, false))
                    SpriteSystem.LayerSetVisible((uid, args.Sprite), powerVisibilityLayer, powered);
            }
        }

        if (component.SetOnDepowered != null && !powered)
        {
            foreach (var (setLayer, powerState) in component.SetOnDepowered)
            {
                if (SpriteSystem.LayerMapTryGet((uid, args.Sprite), setLayer, out var setStateLayer, false))
                    SpriteSystem.LayerSetRsiState((uid, args.Sprite), setStateLayer, new RSI.StateId(powerState));
            }
        }

        if (args.AppearanceData.TryGetValue(AtmosMonitorVisuals.AlarmType, out var alarmTypeObject)
            && alarmTypeObject is AtmosAlarmType alarmType
            && powered)
        {
            if (component.HiddenAlarmTypes != null && component.HiddenAlarmTypes.Contains(alarmType))
            {
                // Nothing worth drawing attention to right now - hide this layer so whatever
                // is underneath (e.g. a station alert level indicator) isn't permanently masked.
                SpriteSystem.LayerSetVisible((uid, args.Sprite), layer, false);
            }
            else if (component.AlarmStates.TryGetValue(alarmType, out var state))
            {
                SpriteSystem.LayerSetVisible((uid, args.Sprite), layer, true);
                SpriteSystem.LayerSetRsiState((uid, args.Sprite), layer, new RSI.StateId(state));
            }
        }
    }
}
