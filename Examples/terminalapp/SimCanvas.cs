namespace Tui.Sim
{
    // =========================================================================
    // SimCanvas
    // Widget that renders a Simulation grid directly into the ScreenBuffer.
    // Each cell maps to exactly one terminal character with a color that
    // reflects its state and age.
    // =========================================================================
    public class SimCanvas : Core.Widget
    {
        // =====================================================================
        // Private state
        // =====================================================================

        private Simulation sim_;

        // =====================================================================
        // Construction
        // =====================================================================

        public SimCanvas(int x, int y, int width, int height, Simulation sim)
        {
            X        = x;
            Y        = y;
            Width    = width;
            Height   = height;
            CanFocus = false;
            sim_     = sim;
        }

        // =====================================================================
        // Widget interface
        // =====================================================================

        public override void Draw(Core.ScreenBuffer buf, int ox, int oy)
        {
            if (!Visible) return;

            int sim_w = sim_.Width;
            int sim_h = sim_.Height;

            for (int row = 0; row < Height && row < sim_h; row++)
            {
                for (int col = 0; col < Width && col < sim_w; col++)
                {
                    CellState state = sim_.Get(col, row);
                    int       age   = sim_.GetAge(col, row);

                    char        ch = cell_char_(state, age);
                    Core.Color  fg = cell_fg_(state, age);
                    Core.Color  bg = cell_bg_(state);

                    buf.Set(ox + X + col, oy + Y + row, ch, fg, bg);
                }
            }
        }

        // =====================================================================
        // Private helpers
        // =====================================================================

        private static char cell_char_(CellState state, int age)
        {
            switch (state)
            {
                case CellState.AlphaA:
                    // Pulsing density character based on age
                    if (age < 3)  return '\u2591';   // ░
                    if (age < 10) return '\u2592';   // ▒
                    return '\u2593';                  // ▓
                case CellState.AlphaB:
                    return '\u2588';                  // █
                case CellState.AlphaC:
                    return '\u25cf';                  // ●  (or fall back to 'o')
                case CellState.Food:
                    return '\u00b7';                  // ·
                case CellState.Spark:
                    return '*';
                case CellState.Decay:
                    return '\u00b0';                  // °
                case CellState.Ember:
                    if (age < 1) return '\u2665';     // ♥
                    if (age < 2) return '\u25aa';     // ▪
                    return '.';
                default:
                    return ' ';
            }
        }

        private static Core.Color cell_fg_(CellState state, int age)
        {
            switch (state)
            {
                case CellState.AlphaA:
                    // Fade from bright to dim as it ages
                    if (age < 5)  return Core.Color.Green;
                    if (age < 15) return Core.Color.DarkGreen;
                    return Core.Color.DarkGray;
                case CellState.AlphaB:
                    if (age < 10) return Core.Color.Cyan;
                    if (age < 40) return Core.Color.DarkCyan;
                    return Core.Color.DarkBlue;
                case CellState.AlphaC:
                    return Core.Color.Yellow;
                case CellState.Food:
                    return Core.Color.DarkGreen;
                case CellState.Spark:
                    return Core.Color.White;
                case CellState.Decay:
                    return Core.Color.DarkGray;
                case CellState.Ember:
                    if (age < 1) return Core.Color.Red;
                    if (age < 2) return Core.Color.DarkRed;
                    return Core.Color.DarkGray;
                default:
                    return Core.Color.Black;
            }
        }

        private static Core.Color cell_bg_(CellState state)
        {
            switch (state)
            {
                case CellState.Spark: return Core.Color.Yellow;
                case CellState.AlphaC: return Core.Color.DarkYellow;
                default: return Core.Color.Black;
            }
        }
    }
}
