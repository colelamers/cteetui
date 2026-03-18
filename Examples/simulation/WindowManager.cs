using System.Collections.Generic;

namespace Tui.Core
{
    // -------------------------------------------------------------------------
    // WindowManager
    // Central hub for the TUI event loop.
    //
    // Window navigation keybindings:
    //   F6              focus next window
    //   Shift+F6        focus previous window
    //   Alt+RightArrow  focus next window
    //   Alt+LeftArrow   focus previous window
    //   Alt+1..9        focus window by visible index
    //
    // Widget navigation (handled by Window):
    //   Tab             next focusable widget in the active window
    //   Shift+Tab       previous focusable widget
    //
    // Global:
    //   Ctrl+Q / Ctrl+C quit
    // -------------------------------------------------------------------------
    public sealed class WindowManager : System.IDisposable
    {
        // =====================================================================
        // Private state
        // =====================================================================

        private ScreenBuffer buf_;
        private List<Window> windows_  = new List<Window>();
        private Window       focused_  = null;
        private bool         running_  = false;
        private bool         disposed_ = false;

        // =====================================================================
        // Public state
        // =====================================================================

        public Color GlobalBackground = Color.Black;

        // ---------------------------------------------------------------------
        // FrameDelayMs
        // Milliseconds to sleep between frames when no key is pending.
        // Set to 0 for maximum throughput (busy-wait).
        // Default 16 ms gives approximately 60 fps.
        // ---------------------------------------------------------------------
        public int FrameDelayMs = 16;

        // =====================================================================
        // Events
        // =====================================================================

        // ---------------------------------------------------------------------
        // OnFrame
        // Fired once per frame before drawing.
        // Use for animation, state polling, and UI updates.
        // ---------------------------------------------------------------------
        public event System.Action OnFrame;

        // ---------------------------------------------------------------------
        // OnUnhandledKey
        // Fired for keys not consumed by any window or built-in binding.
        // ---------------------------------------------------------------------
        public event System.Action<System.ConsoleKeyInfo> OnUnhandledKey;

        // =====================================================================
        // Construction
        // =====================================================================

        public WindowManager()
        {
            System.Console.OutputEncoding        = System.Text.Encoding.UTF8;
            System.Console.CursorVisible         = false;
            System.Console.TreatControlCAsInput  = true;

            buf_ = new ScreenBuffer(System.Console.WindowWidth, System.Console.WindowHeight);
        }

        // =====================================================================
        // Window management
        // =====================================================================

        // ---------------------------------------------------------------------
        // AddWindow
        // Register a window with the manager.
        // If this is the first visible window it is focused automatically.
        // ---------------------------------------------------------------------
        public Window AddWindow(Window win)
        {
            windows_.Add(win);
            sort_by_z_();

            if (focused_ == null && win.Visible)
            {
                focus_window_(win);
            }
            return win;
        }

        // ---------------------------------------------------------------------
        // RemoveWindow
        // Unregister a window permanently and force a full redraw.
        // ---------------------------------------------------------------------
        public void RemoveWindow(Window win)
        {
            windows_.Remove(win);

            if (focused_ == win)
            {
                focused_ = null;
                focus_topmost_();
            }

            buf_.Invalidate();
        }

        // ---------------------------------------------------------------------
        // HideWindow / ShowWindow
        // Temporarily hide or re-show a window without removing it.
        // ---------------------------------------------------------------------
        public void HideWindow(Window win)
        {
            win.Visible = false;

            if (focused_ == win)
            {
                focused_ = null;
                focus_topmost_();
            }

            buf_.Invalidate();
        }

        public void ShowWindow(Window win)
        {
            win.Visible = true;
            focus_window_(win);
            buf_.Invalidate();
        }

        // ---------------------------------------------------------------------
        // BringToFront
        // Assign a z-index above all existing windows.
        // ---------------------------------------------------------------------
        public void BringToFront(Window win)
        {
            int max_z = 0;
            for (int i = 0; i < windows_.Count; i++)
            {
                if (windows_[i].ZIndex > max_z)
                {
                    max_z = windows_[i].ZIndex;
                }
            }
            win.ZIndex = max_z + 1;
            sort_by_z_();
        }

        // =====================================================================
        // Main loop
        // =====================================================================

        // ---------------------------------------------------------------------
        // Run
        // Blocks until Stop() is called or Ctrl+Q is pressed.
        // ---------------------------------------------------------------------
        public void Run()
        {
            running_ = true;
            buf_.Invalidate();

            while (running_)
            {
                handle_resize_();

                if (OnFrame != null)
                {
                    OnFrame();
                }

                buf_.ClearBack(GlobalBackground);

                for (int i = 0; i < windows_.Count; i++)
                {
                    if (windows_[i].Visible)
                    {
                        windows_[i].Draw(buf_);
                    }
                }

                buf_.Flush();
                System.Console.CursorVisible = false;

                if (System.Console.KeyAvailable)
                {
                    System.ConsoleKeyInfo key = System.Console.ReadKey(true);
                    dispatch_key_(key);
                }
                else if (FrameDelayMs > 0)
                {
                    System.Threading.Thread.Sleep(FrameDelayMs);
                }
            }
        }

        // ---------------------------------------------------------------------
        // Stop
        // Signal the event loop to exit after the current frame.
        // ---------------------------------------------------------------------
        public void Stop()
        {
            running_ = false;
        }

        // =====================================================================
        // Drawing helpers
        // =====================================================================

        // ---------------------------------------------------------------------
        // DrawStatusBar
        // Write a full-width status line at the bottom row of the screen.
        // Call from OnFrame.
        // ---------------------------------------------------------------------
        public void DrawStatusBar(string text, Color fg, Color bg)
        {
            int row = buf_.Height - 1;
            buf_.Fill(new Rect(0, row, buf_.Width, 1), ' ', fg, bg);
            buf_.WriteString(0, row, text, fg, bg, buf_.Width);
        }

        public void DrawStatusBar(string text)
        {
            DrawStatusBar(text, Color.Black, Color.DarkCyan);
        }

        // =====================================================================
        // IDisposable
        // =====================================================================

        public void Dispose()
        {
            if (disposed_)
            {
                return;
            }
            disposed_ = true;
            System.Console.CursorVisible = true;
            System.Console.ResetColor();
            System.Console.Clear();
        }

        // =====================================================================
        // Private helpers
        // =====================================================================

        private void dispatch_key_(System.ConsoleKeyInfo key)
        {
            bool ctrl  = (key.Modifiers & System.ConsoleModifiers.Control) != 0;
            bool alt   = (key.Modifiers & System.ConsoleModifiers.Alt)     != 0;
            bool shift = (key.Modifiers & System.ConsoleModifiers.Shift)   != 0;

            // -----------------------------------------------------------------
            // Ctrl+Q / Ctrl+C -- quit
            // -----------------------------------------------------------------
            if (ctrl && key.Key == System.ConsoleKey.Q)
            {
                if (OnUnhandledKey != null)
                {
                    OnUnhandledKey(key);
                }
                Stop();
                return;
            }

            if (ctrl && key.Key == System.ConsoleKey.C)
            {
                if (OnUnhandledKey != null)
                {
                    OnUnhandledKey(key);
                }
                Stop();
                return;
            }

            // -----------------------------------------------------------------
            // F6 / Shift+F6 -- cycle focused window forward / backward
            // -----------------------------------------------------------------
            if (key.Key == System.ConsoleKey.F6)
            {
                if (shift)
                {
                    focus_window_prev_();
                }
                else
                {
                    focus_window_next_();
                }
                return;
            }

            // -----------------------------------------------------------------
            // Alt+RightArrow / Alt+LeftArrow -- cycle focused window
            // -----------------------------------------------------------------
            if (alt && key.Key == System.ConsoleKey.RightArrow)
            {
                focus_window_next_();
                return;
            }

            if (alt && key.Key == System.ConsoleKey.LeftArrow)
            {
                focus_window_prev_();
                return;
            }

            // -----------------------------------------------------------------
            // Alt+1 through Alt+9 -- focus window by visible index
            // -----------------------------------------------------------------
            if (alt && key.KeyChar >= '1' && key.KeyChar <= '9')
            {
                int          target  = key.KeyChar - '1';
                List<Window> visible = get_visible_windows_();
                if (target < visible.Count)
                {
                    focus_window_(visible[target]);
                }
                return;
            }

            // -----------------------------------------------------------------
            // Forward to focused window
            // -----------------------------------------------------------------
            if (focused_ != null && focused_.Visible)
            {
                if (focused_.HandleKey(key))
                {
                    return;
                }
            }

            if (OnUnhandledKey != null)
            {
                OnUnhandledKey(key);
            }
        }

        private void handle_resize_()
        {
            int cw = System.Console.WindowWidth;
            int ch = System.Console.WindowHeight;

            if (cw != buf_.Width || ch != buf_.Height)
            {
                buf_.Resize(cw, ch);
                buf_.Invalidate();
                System.Console.Clear();
            }
        }

        private void focus_window_(Window win)
        {
            if (focused_ != null)
            {
                focused_.Focused = false;
            }
            focused_         = win;
            focused_.Focused = true;
            BringToFront(win);
            win.FocusFirst();
        }

        // ---------------------------------------------------------------------
        // focus_window_next_ / focus_window_prev_
        // Cycle focus through the list of visible windows.
        // ---------------------------------------------------------------------
        private void focus_window_next_()
        {
            List<Window> visible = get_visible_windows_();
            if (visible.Count == 0)
            {
                return;
            }

            int current = find_focused_index_(visible);
            int next    = current + 1;
            if (next >= visible.Count)
            {
                next = 0;
            }
            focus_window_(visible[next]);
        }

        private void focus_window_prev_()
        {
            List<Window> visible = get_visible_windows_();
            if (visible.Count == 0)
            {
                return;
            }

            int current = find_focused_index_(visible);
            int prev    = current - 1;
            if (prev < 0)
            {
                prev = visible.Count - 1;
            }
            focus_window_(visible[prev]);
        }

        // ---------------------------------------------------------------------
        // find_focused_index_
        // Returns the index of the currently focused window in the given list,
        // or 0 if none is found.
        // ---------------------------------------------------------------------
        private int find_focused_index_(List<Window> visible)
        {
            for (int i = 0; i < visible.Count; i++)
            {
                if (visible[i] == focused_)
                {
                    return i;
                }
            }
            return 0;
        }

        private void focus_topmost_()
        {
            Window top = null;
            for (int i = 0; i < windows_.Count; i++)
            {
                if (windows_[i].Visible)
                {
                    top = windows_[i];
                }
            }
            if (top != null)
            {
                focus_window_(top);
            }
        }

        private void sort_by_z_()
        {
            // Insertion sort -- window lists are small
            for (int i = 1; i < windows_.Count; i++)
            {
                Window key = windows_[i];
                int    j   = i - 1;
                while (j >= 0 && windows_[j].ZIndex > key.ZIndex)
                {
                    windows_[j + 1] = windows_[j];
                    j--;
                }
                windows_[j + 1] = key;
            }
        }

        private List<Window> get_visible_windows_()
        {
            List<Window> result = new List<Window>();
            for (int i = 0; i < windows_.Count; i++)
            {
                if (windows_[i].Visible)
                {
                    result.Add(windows_[i]);
                }
            }
            return result;
        }
    }
}
