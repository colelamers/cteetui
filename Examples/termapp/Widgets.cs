using System.Collections.Generic;

namespace Tui.Core
{
    // =========================================================================
    // Widget
    // Base class for all UI elements that live inside a Window.
    // Coordinates are relative to the parent window's content origin.
    // =========================================================================
    public abstract class Widget
    {
        // =====================================================================
        // Public state
        // =====================================================================

        public int  X        = 0;
        public int  Y        = 0;
        public int  Width    = 0;
        public int  Height   = 0;
        public bool Visible  = true;
        public bool CanFocus = false;
        public bool Focused  = false;
        public int  TabIndex = 0;

        public Color Foreground = Color.White;
        public Color Background = Color.Black;

        // =====================================================================
        // Abstract interface
        // =====================================================================

        // ---------------------------------------------------------------------
        // Draw
        // Render the widget into the buffer.
        // ox, oy are the window content origin in screen coordinates.
        // ---------------------------------------------------------------------
        public abstract void Draw(ScreenBuffer buf, int ox, int oy);

        // ---------------------------------------------------------------------
        // HandleKey
        // Process a keypress. Returns true if the widget consumed it.
        // ---------------------------------------------------------------------
        public virtual bool HandleKey(System.ConsoleKeyInfo key)
        {
            return false;
        }
    }

    // =========================================================================
    // Label
    // Non-interactive static text widget.
    // =========================================================================
    public class Label : Widget
    {
        // =====================================================================
        // Public state
        // =====================================================================

        public string Text = "";

        // =====================================================================
        // Construction
        // =====================================================================

        public Label(int x, int y, string text, Color fg, Color bg)
        {
            X          = x;
            Y          = y;
            Text       = text;
            Width      = text.Length;
            Height     = 1;
            Foreground = fg;
            Background = bg;
            CanFocus   = false;
        }

        public Label(int x, int y, string text) : this(x, y, text, Color.White, Color.Black)
        {
        }

        // =====================================================================
        // Widget interface
        // =====================================================================

        public override void Draw(ScreenBuffer buf, int ox, int oy)
        {
            if (!Visible)
            {
                return;
            }
            buf.WriteString(ox + X, oy + Y, Text, Foreground, Background, Width);
        }
    }

    // =========================================================================
    // Button
    // Focusable widget that highlights when focused and fires Clicked on
    // Enter or Space.
    // =========================================================================
    public class Button : Widget
    {
        // =====================================================================
        // Public state
        // =====================================================================

        public string Text = "";

        public Color FocusedForeground = Color.Black;
        public Color FocusedBackground = Color.Cyan;

        // =====================================================================
        // Events
        // =====================================================================

        public event System.Action Clicked;

        // =====================================================================
        // Construction
        // =====================================================================

        public Button(int x, int y, string text, Color fg, Color bg)
        {
            X          = x;
            Y          = y;
            Text       = text;
            Width      = text.Length + 4;    // "[ text ]"
            Height     = 1;
            Foreground = fg;
            Background = bg;
            CanFocus   = true;
        }

        public Button(int x, int y, string text) : this(x, y, text, Color.White, Color.DarkBlue)
        {
        }

        // =====================================================================
        // Widget interface
        // =====================================================================

        public override void Draw(ScreenBuffer buf, int ox, int oy)
        {
            if (!Visible)
            {
                return;
            }

            Color fg;
            Color bg;

            if (Focused)
            {
                fg = FocusedForeground;
                bg = FocusedBackground;
            }
            else
            {
                fg = Foreground;
                bg = Background;
            }

            string label = "[ " + Text + " ]";

            if (label.Length < Width)
            {
                label = label + new string(' ', Width - label.Length);
            }
            else if (label.Length > Width)
            {
                label = label.Substring(0, Width);
            }

            buf.WriteString(ox + X, oy + Y, label, fg, bg, Width);
        }

        public override bool HandleKey(System.ConsoleKeyInfo key)
        {
            if (key.Key == System.ConsoleKey.Enter || key.Key == System.ConsoleKey.Spacebar)
            {
                if (Clicked != null)
                {
                    Clicked();
                }
                return true;
            }
            return false;
        }
    }

    // =========================================================================
    // TextBox
    // Single-line editable text input with cursor and horizontal scrolling.
    // =========================================================================
    public class TextBox : Widget
    {
        // =====================================================================
        // Private state
        // =====================================================================

        private List<char> chars_  = new List<char>();
        private int        cursor_ = 0;
        private int        scroll_ = 0;    // horizontal scroll offset in chars

        // =====================================================================
        // Public state
        // =====================================================================

        public Color FocusedForeground = Color.Black;
        public Color FocusedBackground = Color.White;
        public Color CursorForeground  = Color.Black;
        public Color CursorBackground  = Color.Yellow;

        public string Placeholder      = "";
        public Color  PlaceholderColor = Color.DarkGray;

        // =====================================================================
        // Events
        // =====================================================================

        public event System.Action<string> TextChanged;
        public event System.Action<string> Submitted;

        // =====================================================================
        // Public properties
        // =====================================================================

        public string Text
        {
            get
            {
                return new string(chars_.ToArray());
            }
            set
            {
                chars_.Clear();
                if (value != null)
                {
                    chars_.AddRange(value.ToCharArray());
                }
                cursor_ = chars_.Count;
                scroll_ = 0;
            }
        }

        // =====================================================================
        // Construction
        // =====================================================================

        public TextBox(int x, int y, int width, Color fg, Color bg)
        {
            X          = x;
            Y          = y;
            Width      = width;
            Height     = 1;
            Foreground = fg;
            Background = bg;
            CanFocus   = true;
        }

        public TextBox(int x, int y, int width) : this(x, y, width, Color.Black, Color.White)
        {
        }

        // =====================================================================
        // Widget interface
        // =====================================================================

        public override void Draw(ScreenBuffer buf, int ox, int oy)
        {
            if (!Visible)
            {
                return;
            }

            Color fg;
            Color bg;

            if (Focused)
            {
                fg = FocusedForeground;
                bg = FocusedBackground;
            }
            else
            {
                fg = Foreground;
                bg = Background;
            }

            // Fill the whole box
            Rect box = new Rect(ox + X, oy + Y, Width, 1);
            buf.Fill(box, ' ', fg, bg);

            // Show placeholder when empty and not focused
            if (chars_.Count == 0 && !Focused && Placeholder.Length > 0)
            {
                buf.WriteString(ox + X, oy + Y, Placeholder, PlaceholderColor, bg, Width);
                return;
            }

            // Adjust horizontal scroll so cursor stays visible
            if (Focused)
            {
                if (cursor_ - scroll_ >= Width)
                {
                    scroll_ = cursor_ - Width + 1;
                }
                if (cursor_ - scroll_ < 0)
                {
                    scroll_ = cursor_;
                }
            }

            // Render visible characters
            for (int i = 0; i < Width; i++)
            {
                int ci = i + scroll_;
                if (ci >= chars_.Count)
                {
                    break;
                }

                bool is_cursor = Focused && ci == cursor_;

                buf.Set(
                    ox + X + i,
                    oy + Y,
                    chars_[ci],
                    is_cursor ? CursorForeground : fg,
                    is_cursor ? CursorBackground : bg);
            }

            // Draw end-of-text cursor
            if (Focused && cursor_ == chars_.Count)
            {
                int cx = cursor_ - scroll_;
                if (cx >= 0 && cx < Width)
                {
                    buf.Set(ox + X + cx, oy + Y, ' ', CursorForeground, CursorBackground);
                }
            }
        }

        public override bool HandleKey(System.ConsoleKeyInfo key)
        {
            switch (key.Key)
            {
                case System.ConsoleKey.LeftArrow:
                    if (cursor_ > 0)
                    {
                        cursor_--;
                    }
                    return true;

                case System.ConsoleKey.RightArrow:
                    if (cursor_ < chars_.Count)
                    {
                        cursor_++;
                    }
                    return true;

                case System.ConsoleKey.Home:
                    cursor_ = 0;
                    return true;

                case System.ConsoleKey.End:
                    cursor_ = chars_.Count;
                    return true;

                case System.ConsoleKey.Backspace:
                    if (cursor_ > 0)
                    {
                        chars_.RemoveAt(cursor_ - 1);
                        cursor_--;
                        fire_text_changed_();
                    }
                    return true;

                case System.ConsoleKey.Delete:
                    if (cursor_ < chars_.Count)
                    {
                        chars_.RemoveAt(cursor_);
                        fire_text_changed_();
                    }
                    return true;

                case System.ConsoleKey.Enter:
                    if (Submitted != null)
                    {
                        Submitted(Text);
                    }
                    return true;

                default:
                    char ch = key.KeyChar;
                    if (ch >= ' ')
                    {
                        chars_.Insert(cursor_, ch);
                        cursor_++;
                        fire_text_changed_();
                        return true;
                    }
                    return false;
            }
        }

        // =====================================================================
        // Private helpers
        // =====================================================================

        private void fire_text_changed_()
        {
            if (TextChanged != null)
            {
                TextChanged(Text);
            }
        }
    }

    // =========================================================================
    // LogView
    // Append-only scrollable log pane.
    // Append() is thread-safe and can be called from background threads.
    // =========================================================================
    public class LogView : Widget
    {
        // =====================================================================
        // Private state
        // =====================================================================

        private struct LogLine
        {
            public string Text;
            public Color  Fg;
        }

        private List<LogLine> lines_       = new List<LogLine>();
        private object        lock_        = new object();
        private int           scroll_off_  = 0;      // lines scrolled up from bottom
        private bool          auto_scroll_ = true;

        // =====================================================================
        // Construction
        // =====================================================================

        public LogView(int x, int y, int width, int height, Color bg)
        {
            X          = x;
            Y          = y;
            Width      = width;
            Height     = height;
            Background = bg;
            CanFocus   = true;
        }

        public LogView(int x, int y, int width, int height)
            : this(x, y, width, height, Color.Black)
        {
        }

        // =====================================================================
        // Public methods
        // =====================================================================

        // ---------------------------------------------------------------------
        // Append
        // Add a line of text. Long lines are word-wrapped at Width.
        // Thread-safe.
        // ---------------------------------------------------------------------
        public void Append(string line, Color fg)
        {
            lock (lock_)
            {
                while (line.Length > Width)
                {
                    LogLine part;
                    part.Text = line.Substring(0, Width);
                    part.Fg   = fg;
                    lines_.Add(part);
                    line = line.Substring(Width);
                }

                LogLine entry;
                entry.Text = line;
                entry.Fg   = fg;
                lines_.Add(entry);

                if (auto_scroll_)
                {
                    scroll_off_ = 0;
                }
            }
        }

        public void Append(string line)
        {
            Append(line, Color.Gray);
        }

        // ---------------------------------------------------------------------
        // Clear
        // Remove all lines and reset scroll position.
        // ---------------------------------------------------------------------
        public void Clear()
        {
            lock (lock_)
            {
                lines_.Clear();
                scroll_off_ = 0;
            }
        }

        // =====================================================================
        // Widget interface
        // =====================================================================

        public override void Draw(ScreenBuffer buf, int ox, int oy)
        {
            if (!Visible)
            {
                return;
            }

            buf.Fill(new Rect(ox + X, oy + Y, Width, Height), ' ', Foreground, Background);

            lock (lock_)
            {
                int total   = lines_.Count;
                int visible = Height;
                int start   = total - visible - scroll_off_;

                if (start < 0)
                {
                    start = 0;
                }

                int end = start + visible;
                if (end > total)
                {
                    end = total;
                }

                for (int i = start; i < end; i++)
                {
                    int    row  = oy + Y + (i - start);
                    string text = lines_[i].Text;
                    Color  fg   = lines_[i].Fg;
                    buf.WriteString(ox + X, row, text, fg, Background, Width);
                }

                // Scroll indicator when not at bottom
                if (Focused && scroll_off_ > 0)
                {
                    string indicator = "\u2191 " + scroll_off_ + " ";
                    int    ix        = ox + X + Width - indicator.Length;
                    buf.WriteString(ix, oy + Y, indicator, Color.Yellow, Background);
                }
            }
        }

        public override bool HandleKey(System.ConsoleKeyInfo key)
        {
            switch (key.Key)
            {
                case System.ConsoleKey.UpArrow:
                    scroll_up_(1);
                    return true;

                case System.ConsoleKey.DownArrow:
                    scroll_down_(1);
                    return true;

                case System.ConsoleKey.PageUp:
                    scroll_up_(Height);
                    return true;

                case System.ConsoleKey.PageDown:
                    scroll_down_(Height);
                    return true;

                case System.ConsoleKey.End:
                    lock (lock_)
                    {
                        scroll_off_ = 0;
                    }
                    auto_scroll_ = true;
                    return true;
            }
            return false;
        }

        // =====================================================================
        // Private helpers
        // =====================================================================

        private void scroll_up_(int amount)
        {
            lock (lock_)
            {
                int max = lines_.Count - Height;
                if (max < 0)
                {
                    max = 0;
                }
                scroll_off_ += amount;
                if (scroll_off_ > max)
                {
                    scroll_off_ = max;
                }
            }
            auto_scroll_ = false;
        }

        private void scroll_down_(int amount)
        {
            lock (lock_)
            {
                scroll_off_ -= amount;
                if (scroll_off_ < 0)
                {
                    scroll_off_ = 0;
                }
            }
            if (scroll_off_ == 0)
            {
                auto_scroll_ = true;
            }
        }
    }

    // =========================================================================
    // ProgressBar
    // Non-focusable horizontal progress indicator.
    // Set Value (0.0 -- 1.0) each frame.
    // =========================================================================
    public class ProgressBar : Widget
    {
        // =====================================================================
        // Private state
        // =====================================================================

        private double value_ = 0.0;

        // =====================================================================
        // Public state
        // =====================================================================

        public Color FillColor   = Color.Cyan;
        public bool  ShowPercent = true;

        // =====================================================================
        // Public properties
        // =====================================================================

        public double Value
        {
            get
            {
                return value_;
            }
            set
            {
                if (value < 0.0)
                {
                    value_ = 0.0;
                }
                else if (value > 1.0)
                {
                    value_ = 1.0;
                }
                else
                {
                    value_ = value;
                }
            }
        }

        // =====================================================================
        // Construction
        // =====================================================================

        public ProgressBar(int x, int y, int width, Color bg)
        {
            X          = x;
            Y          = y;
            Width      = width;
            Height     = 1;
            Background = bg;
            CanFocus   = false;
        }

        public ProgressBar(int x, int y, int width) : this(x, y, width, Color.DarkGray)
        {
        }

        // =====================================================================
        // Widget interface
        // =====================================================================

        public override void Draw(ScreenBuffer buf, int ox, int oy)
        {
            if (!Visible)
            {
                return;
            }

            int filled = (int)(value_ * Width);

            // Filled portion
            buf.Fill(new Rect(ox + X, oy + Y, filled, 1), ' ', FillColor, FillColor);

            // Empty portion
            buf.Fill(new Rect(ox + X + filled, oy + Y, Width - filled, 1), ' ', Background, Background);

            // Percent overlay centered on the bar
            if (ShowPercent)
            {
                int    pct_int = (int)(value_ * 100.0);
                string pct     = pct_int.ToString();
                while (pct.Length < 3)
                {
                    pct = " " + pct;
                }
                pct = pct + "%";

                int px = ox + X + (Width - pct.Length) / 2;

                for (int i = 0; i < pct.Length; i++)
                {
                    int  gx      = px + i;
                    bool on_fill = (gx - ox - X) < filled;

                    buf.Set(
                        gx,
                        oy + Y,
                        pct[i],
                        on_fill ? Background : FillColor,
                        on_fill ? FillColor  : Background);
                }
            }
        }
    }
}
