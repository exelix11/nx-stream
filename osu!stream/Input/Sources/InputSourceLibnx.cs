using System;
using Libnx;
using osum.Support;

namespace osum.Input.Sources
{
    public class InputSourceLibnx : InputSource, IUpdateable
    {
        // Needed to initialize the hid service
        readonly Libnx.Pad pad = new();
        readonly Libnx.TouchScreen touch = new();

        // When non-null, this is a valid tracking point
        readonly TrackingPoint[] touches = new TrackingPoint[HidTouchScreenState.FingerCountMax];
        readonly bool[] updateTouches = new bool[HidTouchScreenState.FingerCountMax];

        public void Update()
        {
            if (touch.Update())
            {
                // HidTouchAttribute_Start seem broken, at least in the emulator.
                // Instead keep track of active fingers and manually send start/end events.
                updateTouches.AsSpan().Clear();

                for (int i = 0; i < touch.State.count; i++)
                {
                    ref var t = ref touch.State.touches[i];
                    var fingerId = t.finger_id;

                    if (fingerId < 0 || fingerId >= touches.Length)
                        continue;

                    var wasActive = touches[fingerId] != null;
                    updateTouches[fingerId] = true;

                    if (!wasActive)
                    {
                        var point = new TrackingPoint(new(t.x, t.y), new());
                        touches[fingerId] = point;
                        TriggerOnDown(point);
                    }
                    else
                    {
                        var point = touches[fingerId];
                        point.Location = new(t.x, t.y);
                        TriggerOnMove(point);
                    }
                }

                for (int fingerId = 0; fingerId < touches.Length; fingerId++)
                {
                    // FingerId was sent in a previous update but it's not active anymore, so send an "up" event and clear it.
                    if (touches[fingerId] != null && !updateTouches[fingerId])
                    {
                        var point = touches[fingerId];
                        TriggerOnUp(point);
                        touches[fingerId] = null;
                    }
                }
            }
        }
    }
}