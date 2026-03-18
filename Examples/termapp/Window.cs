using System.Collections.Generic;

namespace Tui.Core
{
    // -------------------------------------------------------------------------
    // Window
    // A bordered, titled container that hosts Widget instances.
    // Manages tab-order focus within its widget list.
    // Windows carry a ZIndex; higher values are drawn on top.
    // -------------------------------------------------------------------------
    public class Window
    {
        // =====================================================================
        // Public state
        // =====================================================================

        public string Title   = "";
        public bool   Visible = true;
        public bool   Focused = false;
        public int    ZIndex  = 0;

        public int X      = 0;
        public int Y      = 0;
        public int Width  = 0;
        public int Height = 0;

        public Color BorderColor        = Color.DarkCyan;
        public Color FocusedBorderColor = Color.Cyan;
        public Color TitleColor         = Color.White;
        public Color BackgroundColor    = Color.Black;

        // -------------------------------------------------------------------------
        // Border characters.
        // Replace with ASCII equivalents if the terminal lacks Unicode:
        //   TopLeft = '+', TopRight = '+', BottomLeft = '+', BottomRight = '+'
        //   Horizontal = '-', Vertical = '|'
        // -------------------------------------------------------------------------
        public char TopLeft     = '\u2554';    // ╔
        public char TopRight    = '\u2557';    // ╗
        public char BottomLeft  = '\u255a';    // ╚
        public char BottomRight = '\u255d';    // ╝
        public char Horizontal  = '\u2550';    // ═
        public char Vertical    = '\u2551';    // ║

        // =====================================================================
        // Private state
        // =====================================================================

        private List<Widget> widgets_   = new List<Widget>();
        private int          focus_idx_ = -1;

        // =====================================================================
        // Construction
        // =====================================================================

        public Window(int x, int y, int width, int height, string title)
        {
            X      = x;
            Y      = y;
            Width  = width;
            Height = height;
            Title  = title;
        }

        public Window(int x, int y, int width, int height)
            : this(x, y, width, height, "")
        {
        }

        // =====================================================================
        // Public properties
        // =====================================================================

        public int ContentX
        {
            get { return X + 1; }
        }

        public int ContentY
        {
            get { return Y + 1; }
        }

        public int ContentWidth
        {
            get
            {
                int w = Width - 2;
                return w < 0 ? 0 : w;
            }
        }

        public int ContentHeight
        {
            get
            {
                int h = Height - 2;
                return h < 0 ? 0 : h;
            }
        }

        public IList<Widget> Widgets
        {
            get { return widgets_; }
        }

        // =====================================================================
        // Widget management
        // =====================================================================

        // ---------------------------------------------------------------------
        // Add
        // Register a widget with this window.
        // Maintains ascending TabIndex order at all times.
        // Returns the widget so callers can chain construction.
        // ---------------------------------------------------------------------
        public Widget Add(Widget widget)
        {
            widgets_.Add(widget);
            sort_by_tab_index_();
            return widget;
        }

        // ---------------------------------------------------------------------
        // Remove
        // Unregister a widget and reset focus state.
        // ---------------------------------------------------------------------
        public void Remove(Widget widget)
        {
            widgets_.Remove(widget);
            focus_idx_ = -1;
            clear_all_focus_();
        }

        // ---------------------------------------------------------------------
        // ClearWidgets
        // Remove all widgets.
        // ---------------------------------------------------------------------
        public void ClearWidgets()
        {
            widgets_.Clear();
            focus_idx_ = -1;
        }

        // =====================================================================
        // Focus management
        // =====================================================================

        // ---------------------------------------------------------------------
        // FocusFirst
        // Move focus to the first focusable widget by TabIndex.
        // ---------------------------------------------------------------------
        public void FocusFirst()
        {
            List<Widget> focusable = get_focusable_();
            if (focusable.Count == 0)
            {
                set_focus_index_(-1, focusable);
            }
            else
            {
                set_focus_index_(0, focusable);
            }
        }

        // ---------------------------------------------------------------------
        // FocusNext / FocusPrev
        // Cycle focus forward or backward through focusable widgets.
        // ---------------------------------------------------------------------
        public void FocusNext()
        {
            List<Widget> focusable = get_focusable_();
            if (focusable.Count == 0)
            {
                return;
            }

            int next = focus_idx_ + 1;
            if (next >= focusable.Count)
            {
                next = 0;
            }
            set_focus_index_(next, focusable);
        }

        public void FocusPrev()
        {
            List<Widget> focusable = get_focusable_();
            if (focusable.Count == 0)
            {
                return;
            }

            int prev = focus_idx_ - 1;
            if (prev < 0)
            {
                prev = focusable.Count - 1;
            }
            set_focus_index_(prev, focusable);
        }

        // ---------------------------------------------------------------------
        // FocusedWidget
        // Returns the currently focused widget, or null if none.
        // ---------------------------------------------------------------------
        public Widget FocusedWidget()
        {
            List<Widget> focusable = get_focusable_();
            if (focus_idx_ >= 0 && focus_idx_ < focusable.Count)
            {
                return focusable[focus_idx_];
            }
            return null;
        }

        // =====================================================================
        // Key routing
        // =====================================================================

        // ---------------------------------------------------------------------
        // HandleKey
        // Tab and Shift+Tab cycle focus; all other keys are forwarded to the
        // currently focused widget.
        // Returns true if the key was consumed.
        // ---------------------------------------------------------------------
        public bool HandleKey(System.ConsoleKeyInfo key)
        {
            if (key.Key == System.ConsoleKey.Tab)
            {
                bool shift = (key.Modifiers & System.ConsoleModifiers.Shift) != 0;
                if (shift)
                {
                    FocusPrev();
                }
                else
                {
                    FocusNext();
                }
                return true;
            }

            Widget fw = FocusedWidget();
            if (fw != null)
            {
                return fw.HandleKey(key);
            }
            return false;
        }

        // =====================================================================
        // Drawing
        // =====================================================================

        public void Draw(ScreenBuffer buf)
        {
            if (!Visible)
            {
                return;
            }

            Color bg     = BackgroundColor;
            Color border = Focused ? FocusedBorderColor : BorderColor;

            // Background fill
            buf.Fill(new Rect(X, Y, Width, Height), ' ', Color.White, bg);

            // Border and title
            if (Width >= 2 && Height >= 2)
            {
                draw_border_(buf, border, bg);
            }

            // Widgets (coordinates relative to content area)
            for (int i = 0; i < widgets_.Count; i++)
            {
                Widget w = widgets_[i];
                if (w.Visible)
                {
                    w.Draw(buf, ContentX, ContentY);
                }
            }
        }

        // =====================================================================
        // Private helpers
        // =====================================================================

        private void draw_border_(ScreenBuffer buf, Color border, Color bg)
        {
            // Top row
            buf.Set(X, Y, TopLeft, border, bg);
            for (int c = X + 1; c < X + Width - 1; c++)
            {
                buf.Set(c, Y, Horizontal, border, bg);
            }
            buf.Set(X + Width - 1, Y, TopRight, border, bg);

            // Bottom row
            buf.Set(X, Y + Height - 1, BottomLeft, border, bg);
            for (int c = X + 1; c < X + Width - 1; c++)
            {
                buf.Set(c, Y + Height - 1, Horizontal, border, bg);
            }
            buf.Set(X + Width - 1, Y + Height - 1, BottomRight, border, bg);

            // Left and right sides
            for (int r = Y + 1; r < Y + Height - 1; r++)
            {
                buf.Set(X,             r, Vertical, border, bg);
                buf.Set(X + Width - 1, r, Vertical, border, bg);
            }

            // Title (centered, clipped to available space)
            if (Title != null && Title.Length > 0)
            {
                string t       = " " + Title + " ";
                int    max_len = Width - 4;

                if (max_len > 0)
                {
                    if (t.Length > max_len)
                    {
                        t = t.Substring(0, max_len);
                    }
                    int tx = X + (Width - t.Length) / 2;
                    buf.WriteString(tx, Y, t, TitleColor, bg);
                }
            }
        }

        private List<Widget> get_focusable_()
        {
            List<Widget> result = new List<Widget>();
            for (int i = 0; i < widgets_.Count; i++)
            {
                if (widgets_[i].CanFocus && widgets_[i].Visible)
                {
                    result.Add(widgets_[i]);
                }
            }
            return result;
        }

        private void set_focus_index_(int idx, List<Widget> focusable)
        {
            clear_all_focus_();
            focus_idx_ = idx;
            if (idx >= 0 && idx < focusable.Count)
            {
                focusable[idx].Focused = true;
            }
        }

        private void clear_all_focus_()
        {
            for (int i = 0; i < widgets_.Count; i++)
            {
                widgets_[i].Focused = false;
            }
        }

        private void sort_by_tab_index_()
        {
            // Insertion sort -- widget lists are short
            for (int i = 1; i < widgets_.Count; i++)
            {
                Widget key = widgets_[i];
                int    j   = i - 1;
                while (j >= 0 && widgets_[j].TabIndex > key.TabIndex)
                {
                    widgets_[j + 1] = widgets_[j];
                    j--;
                }
                widgets_[j + 1] = key;
            }
        }
    }
}
