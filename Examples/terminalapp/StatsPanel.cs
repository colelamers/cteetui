namespace Tui.Sim
{
    // =========================================================================
    // StatsPanel
    // Renders a live table of simulation metrics.
    // Bind to a SimStats reference and it will reflect the latest values
    // every frame automatically.
    // =========================================================================
    public class StatsPanel : Core.Widget
    {
        // =====================================================================
        // Private state
        // =====================================================================

        private SimStats stats_;

        // =====================================================================
        // Construction
        // =====================================================================

        public StatsPanel(int x, int y, int width, SimStats stats)
        {
            X        = x;
            Y        = y;
            Width    = width;
            Height   = 12;
            stats_   = stats;
            CanFocus = false;
        }

        // =====================================================================
        // Widget interface
        // =====================================================================

        public override void Draw(Core.ScreenBuffer buf, int ox, int oy)
        {
            if (!Visible) return;

            int bx = ox + X;
            int by = oy + Y;

            // Background
            buf.Fill(new Core.Rect(bx, by, Width, Height), ' ', Core.Color.White, Background);

            // Header
            buf.WriteString(bx, by, "── SIMULATION METRICS ──", Core.Color.DarkCyan, Background, Width);

            int row = 1;

            draw_row_(buf, bx, by + row++,
                "Tick",    stats_.TickCount.ToString(),
                Core.Color.Gray, Core.Color.White);

            draw_row_(buf, bx, by + row++,
                "Species A",  stats_.PopA.ToString(),
                Core.Color.DarkGray, Core.Color.Green);

            draw_row_(buf, bx, by + row++,
                "Species B",  stats_.PopB.ToString(),
                Core.Color.DarkGray, Core.Color.Cyan);

            draw_row_(buf, bx, by + row++,
                "Walker C",   stats_.PopC.ToString(),
                Core.Color.DarkGray, Core.Color.Yellow);

            draw_row_(buf, bx, by + row++,
                "Food",       stats_.FoodCount.ToString(),
                Core.Color.DarkGray, Core.Color.DarkGreen);

            draw_row_(buf, bx, by + row++,
                "Sparks",     stats_.Sparks.ToString(),
                Core.Color.DarkGray, Core.Color.White);

            draw_row_(buf, bx, by + row++,
                "Births",     stats_.TotalBirths.ToString(),
                Core.Color.DarkGray, Core.Color.Green);

            draw_row_(buf, bx, by + row++,
                "Deaths",     stats_.TotalDeaths.ToString(),
                Core.Color.DarkGray, Core.Color.Red);

            draw_row_(buf, bx, by + row++,
                "Peak pop",   stats_.PeakPop.ToString(),
                Core.Color.DarkGray, Core.Color.Yellow);

            draw_row_(buf, bx, by + row++,
                "Extinctions", stats_.ExtinctCount.ToString(),
                Core.Color.DarkGray, Core.Color.Magenta);

            // Entropy bar
            string entropy_lbl = "Entropy  ";
            buf.WriteString(bx, by + row, entropy_lbl, Core.Color.DarkGray, Background, Width);
            int bar_x  = bx + entropy_lbl.Length;
            int bar_w  = Width - entropy_lbl.Length;
            double ent = stats_.Entropy;
            // Max possible entropy for 5 states = log2(5) ≈ 2.32
            double norm = ent / 2.32;
            if (norm > 1.0) norm = 1.0;
            int filled = (int)(norm * bar_w);

            buf.Fill(new Core.Rect(bar_x,          by + row, filled,        1), ' ', Core.Color.Magenta, Core.Color.Magenta);
            buf.Fill(new Core.Rect(bar_x + filled, by + row, bar_w - filled, 1), ' ', Core.Color.DarkGray, Core.Color.DarkGray);
        }

        // =====================================================================
        // Private helpers
        // =====================================================================

        private void draw_row_(Core.ScreenBuffer buf, int bx, int by,
                                string label, string value,
                                Core.Color label_color, Core.Color value_color)
        {
            int label_w = 12;
            string padded = label;
            while (padded.Length < label_w) padded = padded + " ";
            if (padded.Length > label_w) padded = padded.Substring(0, label_w);

            buf.WriteString(bx,            by, padded, label_color, Background, label_w);
            buf.WriteString(bx + label_w,  by, value,  value_color, Background, Width - label_w);
        }
    }
}
