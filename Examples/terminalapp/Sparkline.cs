using System.Collections.Generic;

namespace Tui.Core
{
    // =========================================================================
    // Sparkline
    // Scrolling single-row bar chart.
    // Push values with Append(); the widget renders a Unicode block-height
    // bar for each sample, scrolling left as new samples arrive.
    // =========================================================================
    public class Sparkline : Widget
    {
        // =====================================================================
        // Private state
        // =====================================================================

        private Queue<double> samples_ = new Queue<double>();
        private double        max_     = 1.0;    // auto-scaling ceiling
        private string        label_   = "";

        // Unicode eighth-blocks from empty to full:
        private static readonly char[] k_blocks_ = new char[]
        {
            ' ',
            '\u2581',   // ▁
            '\u2582',   // ▂
            '\u2583',   // ▃
            '\u2584',   // ▄
            '\u2585',   // ▅
            '\u2586',   // ▆
            '\u2587',   // ▇
            '\u2588',   // █
        };

        // =====================================================================
        // Public state
        // =====================================================================

        public Color LineColor  = Color.Cyan;
        public Color PeakColor  = Color.White;
        public bool  AutoScale  = true;

        // =====================================================================
        // Construction
        // =====================================================================

        public Sparkline(int x, int y, int width, string label, Color fg)
        {
            X          = x;
            Y          = y;
            Width      = width;
            Height     = 1;
            label_     = label;
            LineColor  = fg;
            CanFocus   = false;
        }

        public Sparkline(int x, int y, int width, string label)
            : this(x, y, width, label, Color.Cyan)
        {
        }

        // =====================================================================
        // Public methods
        // =====================================================================

        public void Append(double value)
        {
            samples_.Enqueue(value);

            // Keep only as many samples as we can display
            int label_w = label_.Length > 0 ? label_.Length + 1 : 0;
            int chart_w = Width - label_w;
            while (samples_.Count > chart_w)
            {
                samples_.Dequeue();
            }

            // Auto-scale: track the maximum seen
            if (AutoScale)
            {
                if (value > max_) max_ = value;
            }
        }

        public void SetMax(double max)
        {
            max_ = max;
        }

        // =====================================================================
        // Widget interface
        // =====================================================================

        public override void Draw(ScreenBuffer buf, int ox, int oy)
        {
            if (!Visible) return;

            int bx       = ox + X;
            int by       = oy + Y;
            int label_w  = label_.Length > 0 ? label_.Length + 1 : 0;
            int chart_w  = Width - label_w;

            // Draw label
            if (label_.Length > 0)
            {
                buf.WriteString(bx, by, label_ + " ", Color.DarkGray, Background, label_w);
            }

            // Fill chart area background
            buf.Fill(new Rect(bx + label_w, by, chart_w, 1), ' ', LineColor, Background);

            // Draw samples
            double[] arr     = samples_.ToArray();
            double   peak    = 0.0;
            int      peak_i  = 0;

            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] > peak) { peak = arr[i]; peak_i = i; }
            }

            for (int i = 0; i < arr.Length && i < chart_w; i++)
            {
                double norm  = max_ > 0 ? arr[i] / max_ : 0.0;
                int    block = (int)(norm * (k_blocks_.Length - 1));

                if (block < 0)                   block = 0;
                if (block >= k_blocks_.Length)   block = k_blocks_.Length - 1;

                Color fg = (i == peak_i) ? PeakColor : LineColor;
                buf.Set(bx + label_w + i, by, k_blocks_[block], fg, Background);
            }
        }
    }
}
