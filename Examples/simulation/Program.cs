using System.Collections.Generic;
using Tui.Core;
using Tui.Sim;

// =============================================================================
// Ecosystem Simulation
//
// Three species compete on a live grid.  A log window captures events.
// Population sparklines track history.  A stats panel shows live metrics.
//
// Controls:
//   F6 / Shift+F6          cycle windows
//   Tab / Shift+Tab        cycle widgets in active window
//   R                      reset simulation
//   Space                  pause / unpause
//   Ctrl+Q                 quit
// =============================================================================

namespace Tui
{
    class Program
    {
        // =====================================================================
        // Layout constants
        // =====================================================================

        // Simulation canvas dimensions (terminal cells)
        private const int k_sim_w_ = 70;
        private const int k_sim_h_ = 32;

        // =====================================================================
        // Private state
        // =====================================================================

        private static WindowManager mgr_       = null;
        private static LogView       log_       = null;
        private static Simulation    sim_       = null;
        private static Sparkline     spark_a_   = null;
        private static Sparkline     spark_b_   = null;
        private static Sparkline     spark_c_   = null;
        private static Label         tick_lbl_  = null;
        private static Label         pause_lbl_ = null;
        private static int           frame_     = 0;
        private static bool          paused_    = false;

        // Advance the simulation this many steps per rendered frame
        private const int k_ticks_per_frame_ = 2;

        // =====================================================================
        // Entry point
        // =====================================================================

        static void Main()
        {
            sim_ = new Simulation(k_sim_w_, k_sim_h_);

            using (mgr_ = new WindowManager())
            {
                mgr_.FrameDelayMs = 32;    // ~30 fps

                build_sim_window_();
                build_stats_window_();
                build_log_window_();

                mgr_.OnFrame        += on_frame_;
                mgr_.OnUnhandledKey += on_unhandled_key_;

                seed_log_();

                mgr_.Run();
            }
        }

        // =====================================================================
        // Window builders
        // =====================================================================

        private static void build_sim_window_()
        {
            // Window tall enough for canvas + 4 rows of sparklines/labels
            int win_w = k_sim_w_ + 2;
            int win_h = k_sim_h_ + 2 + 5;

            Window sim_win = new Window(0, 0, win_w, win_h, "Ecosystem Simulation");
            sim_win.BorderColor        = Color.DarkGreen;
            sim_win.FocusedBorderColor = Color.Green;

            SimCanvas canvas = new SimCanvas(0, 0, k_sim_w_, k_sim_h_, sim_);

            // Sparklines sit immediately below the canvas
            int sl_y = k_sim_h_ + 1;
            int sl_w = k_sim_w_ - 2;

            spark_a_ = new Sparkline(0, sl_y,     sl_w, "A", Color.Green);
            spark_b_ = new Sparkline(0, sl_y + 1, sl_w, "B", Color.Cyan);
            spark_c_ = new Sparkline(0, sl_y + 2, sl_w, "C", Color.Yellow);

            tick_lbl_  = new Label(0, sl_y + 3, new string(' ', k_sim_w_), Color.DarkGray, Color.Black);
            pause_lbl_ = new Label(k_sim_w_ - 9, sl_y + 3, "         ",   Color.Black,    Color.Black);

            sim_win.Add(canvas);
            sim_win.Add(spark_a_);
            sim_win.Add(spark_b_);
            sim_win.Add(spark_c_);
            sim_win.Add(tick_lbl_);
            sim_win.Add(pause_lbl_);

            mgr_.AddWindow(sim_win);
        }

        private static void build_stats_window_()
        {
            int left = k_sim_w_ + 2;
            int w    = 28;

            Window stats_win = new Window(left, 0, w, 22, "Stats");
            stats_win.BorderColor        = Color.DarkCyan;
            stats_win.FocusedBorderColor = Color.Cyan;

            StatsPanel pnl = new StatsPanel(0, 0, w - 2, sim_.Stats);

            Button reset_btn = new Button(0, 13, "Reset Sim",  Color.White, Color.DarkRed);
            reset_btn.TabIndex = 0;
            reset_btn.Clicked += on_reset_clicked_;

            Button pause_btn = new Button(14, 13, "Pause",     Color.White, Color.DarkBlue);
            pause_btn.TabIndex = 1;
            pause_btn.Clicked += on_pause_clicked_;

            Label legend_hdr = new Label(0, 15, "Legend:",                  Color.DarkGray, Color.Black);
            Label legend_a   = new Label(0, 16, "\u2593 A  fast spreader",  Color.Green,    Color.Black);
            Label legend_b   = new Label(0, 17, "\u2588 B  conqueror",      Color.Cyan,     Color.Black);
            Label legend_c   = new Label(0, 18, "\u25cf C  walker",         Color.Yellow,   Color.Black);
            Label legend_f   = new Label(0, 19, "\u00b7   food",            Color.DarkGreen,Color.Black);

            stats_win.Add(pnl);
            stats_win.Add(reset_btn);
            stats_win.Add(pause_btn);
            stats_win.Add(legend_hdr);
            stats_win.Add(legend_a);
            stats_win.Add(legend_b);
            stats_win.Add(legend_c);
            stats_win.Add(legend_f);

            mgr_.AddWindow(stats_win);
        }

        private static void build_log_window_()
        {
            int left = k_sim_w_ + 2;
            int w    = 28;
            int top  = 22;
            int h    = k_sim_h_ + 2 + 5 - top;

            Window log_win = new Window(left, top, w, h, "Event Log");
            log_win.BorderColor        = Color.DarkMagenta;
            log_win.FocusedBorderColor = Color.Magenta;

            int log_h = h - 4;
            if (log_h < 3) log_h = 3;

            log_          = new LogView(0, 0, w - 2, log_h);
            log_.TabIndex = 0;

            Button clear_btn = new Button(0, log_h + 1, "Clear", Color.White, Color.DarkBlue);
            clear_btn.TabIndex = 1;
            clear_btn.Clicked += on_clear_log_;

            log_win.Add(log_);
            log_win.Add(clear_btn);

            mgr_.AddWindow(log_win);
        }

        // =====================================================================
        // Event handlers
        // =====================================================================

        private static void on_frame_()
        {
            frame_++;

            // Advance simulation
            if (!paused_)
            {
                for (int t = 0; t < k_ticks_per_frame_; t++)
                {
                    sim_.Tick();
                }
            }

            // Drain simulation events into the log
            List<SimEvent> events = sim_.DrainEvents();
            for (int i = 0; i < events.Count; i++)
            {
                log_.Append(timestamp_() + " " + events[i].Message, events[i].Color);
            }

            // Update sparklines
            spark_a_.Append(sim_.Stats.PopA);
            spark_b_.Append(sim_.Stats.PopB);
            spark_c_.Append(sim_.Stats.PopC);

            // Update tick label
            string state_str = paused_ ? "  [PAUSED]" : "";
            tick_lbl_.Text =
                  "tick=" + sim_.Stats.TickCount
                + "  A="  + sim_.Stats.PopA
                + "  B="  + sim_.Stats.PopB
                + "  C="  + sim_.Stats.PopC
                + "  \u00b7=" + sim_.Stats.FoodCount
                + state_str;

            if (paused_)
            {
                pause_lbl_.Text       = " PAUSED  ";
                pause_lbl_.Foreground = Color.Black;
                pause_lbl_.Background = Color.Yellow;
            }
            else
            {
                pause_lbl_.Text       = "         ";
                pause_lbl_.Background = Color.Black;
            }

            mgr_.DrawStatusBar(
                " F6=Next win  Tab=Next widget  Space=Pause  R=Reset  Ctrl+Q=Quit",
                Color.Black,
                Color.DarkCyan);
        }

        private static void on_unhandled_key_(System.ConsoleKeyInfo key)
        {
            if (key.Key == System.ConsoleKey.Spacebar)
            {
                on_pause_clicked_();
                return;
            }
            if (key.Key == System.ConsoleKey.R || key.KeyChar == 'r')
            {
                on_reset_clicked_();
                return;
            }
        }

        private static void on_reset_clicked_()
        {
            sim_.Reset();
            log_.Append(timestamp_() + " RESET   Simulation restarted", Color.Yellow);
        }

        private static void on_pause_clicked_()
        {
            paused_ = !paused_;
            if (paused_)
            {
                log_.Append(timestamp_() + " PAUSE   Simulation paused",  Color.DarkGray);
            }
            else
            {
                log_.Append(timestamp_() + " RESUME  Simulation resumed", Color.Gray);
            }
        }

        private static void on_clear_log_()
        {
            log_.Clear();
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static void seed_log_()
        {
            log_.Append(timestamp_() + " BOOT    Ecosystem started",           Color.Cyan);
            log_.Append(timestamp_() + " INFO    A  fast spreader (eats food)", Color.Green);
            log_.Append(timestamp_() + " INFO    B  slow conqueror",            Color.Cyan);
            log_.Append(timestamp_() + " INFO    C  random walker",             Color.Yellow);
            log_.Append(timestamp_() + " INFO    Space=pause  R=reset",         Color.DarkGray);
        }

        private static string timestamp_()
        {
            return "[" + System.DateTime.Now.ToString("HH:mm:ss") + "]";
        }
    }
}
