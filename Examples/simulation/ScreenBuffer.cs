namespace Tui.Core
{
    // -------------------------------------------------------------------------
    // ScreenBuffer
    // Double-buffered terminal renderer.
    // All widgets draw into the back buffer.
    // Flush() compares back against front and writes only changed cells
    // to stdout, minimising terminal I/O.
    // -------------------------------------------------------------------------
    public sealed class ScreenBuffer
    {
        // =====================================================================
        // Private state
        // =====================================================================

        private Cell[] front_;
        private Cell[] back_;
        private int    width_;
        private int    height_;

        // =====================================================================
        // Construction
        // =====================================================================

        public ScreenBuffer(int width, int height)
        {
            width_  = width;
            height_ = height;
            front_  = new Cell[width * height];
            back_   = new Cell[width * height];

            Cell blank = new Cell(' ', Color.White, Color.Black);
            fill_array_(front_, blank);
            fill_array_(back_,  blank);
        }

        // =====================================================================
        // Public properties
        // =====================================================================

        public int Width
        {
            get { return width_; }
        }

        public int Height
        {
            get { return height_; }
        }

        // =====================================================================
        // Public methods
        // =====================================================================

        // ---------------------------------------------------------------------
        // Resize
        // Replaces both buffers when the terminal dimensions change.
        // Call Invalidate() after to force a full redraw.
        // ---------------------------------------------------------------------
        public void Resize(int new_width, int new_height)
        {
            width_  = new_width;
            height_ = new_height;
            front_  = new Cell[new_width * new_height];
            back_   = new Cell[new_width * new_height];

            Cell blank = new Cell(' ', Color.White, Color.Black);
            fill_array_(front_, blank);
            fill_array_(back_,  blank);
        }

        // ---------------------------------------------------------------------
        // Set
        // Write a single cell into the back buffer.
        // Out-of-bounds writes are silently ignored.
        // ---------------------------------------------------------------------
        public void Set(int x, int y, Cell cell)
        {
            if (x < 0 || x >= width_ || y < 0 || y >= height_)
            {
                return;
            }
            back_[y * width_ + x] = cell;
        }

        public void Set(int x, int y, char ch, Color fg, Color bg)
        {
            Set(x, y, new Cell(ch, fg, bg));
        }

        // ---------------------------------------------------------------------
        // Fill
        // Flood a rectangle in the back buffer with a single cell value.
        // Clipped to buffer bounds automatically.
        // ---------------------------------------------------------------------
        public void Fill(Rect r, char ch, Color fg, Color bg)
        {
            Cell cell = new Cell(ch, fg, bg);

            for (int row = r.Y; row < r.Bottom && row < height_; row++)
            {
                for (int col = r.X; col < r.Right && col < width_; col++)
                {
                    back_[row * width_ + col] = cell;
                }
            }
        }

        // ---------------------------------------------------------------------
        // WriteString
        // Write a horizontal string into the back buffer.
        // Clipped to max_width characters and buffer bounds.
        // ---------------------------------------------------------------------
        public void WriteString(int x, int y, string text, Color fg, Color bg, int max_width)
        {
            if (y < 0 || y >= height_)
            {
                return;
            }

            int limit = x + max_width;
            if (limit > width_)
            {
                limit = width_;
            }

            for (int i = 0; i < text.Length && x + i < limit; i++)
            {
                Set(x + i, y, text[i], fg, bg);
            }
        }

        public void WriteString(int x, int y, string text, Color fg, Color bg)
        {
            WriteString(x, y, text, fg, bg, int.MaxValue);
        }

        // ---------------------------------------------------------------------
        // ClearBack
        // Reset the entire back buffer to blank space with a given background.
        // ---------------------------------------------------------------------
        public void ClearBack(Color bg)
        {
            Cell blank = new Cell(' ', Color.White, bg);
            fill_array_(back_, blank);
        }

        // ---------------------------------------------------------------------
        // Flush
        // Write all cells that differ between back and front to the terminal.
        // Returns the number of cells actually written.
        // ---------------------------------------------------------------------
        public int Flush()
        {
            int   drawn    = 0;
            bool  has_last = false;
            Color last_fg  = Color.White;
            Color last_bg  = Color.Black;
            int   last_row = -1;
            int   last_col = -1;

            for (int idx = 0; idx < front_.Length; idx++)
            {
                if (front_[idx] == back_[idx])
                {
                    continue;
                }

                front_[idx] = back_[idx];
                Cell cell = front_[idx];

                int row = idx / width_;
                int col = idx % width_;

                if (row != last_row || col != last_col + 1)
                {
                    System.Console.SetCursorPosition(col, row);
                }

                if (!has_last || cell.Foreground != last_fg)
                {
                    System.Console.ForegroundColor = to_console_color_(cell.Foreground);
                    last_fg  = cell.Foreground;
                    has_last = true;
                }

                if (cell.Background != last_bg)
                {
                    System.Console.BackgroundColor = to_console_color_(cell.Background);
                    last_bg = cell.Background;
                }

                System.Console.Write(cell.Char);

                last_row = row;
                last_col = col;
                drawn++;
            }

            System.Console.ResetColor();
            return drawn;
        }

        // ---------------------------------------------------------------------
        // Invalidate
        // Force a full redraw on next Flush() by poisoning the front buffer.
        // Uses '\0' which can never match a real rendered cell.
        // ---------------------------------------------------------------------
        public void Invalidate()
        {
            Cell poison = new Cell('\0', Color.Black, Color.Black);
            fill_array_(front_, poison);
        }

        // =====================================================================
        // Private helpers
        // =====================================================================

        private static void fill_array_(Cell[] arr, Cell value)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = value;
            }
        }

        private static System.ConsoleColor to_console_color_(Color c)
        {
            switch (c)
            {
                case Color.Black:       return System.ConsoleColor.Black;
                case Color.DarkRed:     return System.ConsoleColor.DarkRed;
                case Color.DarkGreen:   return System.ConsoleColor.DarkGreen;
                case Color.DarkYellow:  return System.ConsoleColor.DarkYellow;
                case Color.DarkBlue:    return System.ConsoleColor.DarkBlue;
                case Color.DarkMagenta: return System.ConsoleColor.DarkMagenta;
                case Color.DarkCyan:    return System.ConsoleColor.DarkCyan;
                case Color.Gray:        return System.ConsoleColor.Gray;
                case Color.DarkGray:    return System.ConsoleColor.DarkGray;
                case Color.Red:         return System.ConsoleColor.Red;
                case Color.Green:       return System.ConsoleColor.Green;
                case Color.Yellow:      return System.ConsoleColor.Yellow;
                case Color.Blue:        return System.ConsoleColor.Blue;
                case Color.Magenta:     return System.ConsoleColor.Magenta;
                case Color.Cyan:        return System.ConsoleColor.Cyan;
                case Color.White:       return System.ConsoleColor.White;
                default:                return System.ConsoleColor.White;
            }
        }
    }
}
