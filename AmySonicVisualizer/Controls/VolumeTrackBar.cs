
namespace AmySonicVisualizer.Controls;

public class VolumeTrackBar 
    : TrackBar
{
    private const int WM_MOUSEHWHEEL = 0x020E;
    private const float WHEEL_DELTA = 120f;

    // Accumulator stores high-precision sub-tick movements
    private float _horizontalAccumulator = 0f;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEHWHEEL)
        {
            // Extract horizontal wheel delta
            short delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);

            // Invert the delta and add to our high-precision accumulator
            _horizontalAccumulator += (-delta);

            // Calculate how many standard 120-delta ticks have been accumulated
            // C# integer division truncates towards zero, which is exactly what we want
            int ticks = (int)(_horizontalAccumulator / WHEEL_DELTA);

            if (ticks != 0)
            {
                // Subtract only the used whole ticks, preserving the high-precision remainder
                _horizontalAccumulator -= (ticks * WHEEL_DELTA);

                // Scale the movement by the TrackBar's configured step size
                int step = SmallChange > 0 ? SmallChange : 1;
                int newValue = this.Value + (ticks * step);

                // Clamp to Minimum and Maximum bounds safely
                this.Value = Math.Max(this.Minimum, Math.Min(this.Maximum, newValue));
            }

            // Mark message as handled so the OS knows we processed the scroll
            m.Result = IntPtr.Zero;
            return;
        }

        // All other messages, including vertical scrolling (WM_MOUSEWHEEL = 0x020A), 
        // fall through to standard WinForms behavior untouched.
        base.WndProc(ref m);
    }
}