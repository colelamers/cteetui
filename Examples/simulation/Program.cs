using System.Collections.Generic;
using Tui.Core;

// =============================================================================
// TUI Framework Demo
//
// Controls:
//   Tab / Shift+Tab    cycle widget focus inside the active window
//   Alt+1 / 2 / 3      switch the focused window
//   Ctrl+Q             quit
// =============================================================================

namespace Tui
{
    class Program
    {
        // =====================================================================
        // Private state (class-level so event handlers can reach it)
        // =====================================================================

        private static WindowManager mgr_ = null;
        private static LogView       log_ = null;
        private static TextBox       name_box_ = null;
        private static TextBox       email_box_ = null;
        private static Label         status_lbl_ = null;
        private static ProgressBar   pb1_ = null;
        private static ProgressBar   pb2_ = null;
        private static ProgressBar   pb3_ = null;
        private static System.Random random_ = new System.Random();
        private static int           frame_ = 0;

        private static readonly string[] k_log_messages_ = new string[]
        {
            "INFO  System started",
            "INFO  Listening on :8080",
            "WARN  High memory pressure detected",
            "INFO  Request GET /api/users 200 OK",
            "INFO  Request POST /api/data 201 Created",
            "ERROR Connection timeout after 30s",
            "INFO  Background job finished",
            "WARN  Disk usage above 80%",
            "INFO  Cache invalidated",
            "INFO  Config reloaded",
        };

        private static readonly Color[] k_log_colors_ = new Color[]
        {
            Color.Cyan,
            Color.Cyan,
            Color.Yellow,
            Color.Gray,
            Color.Gray,
            Color.Red,
            Color.Cyan,
            Color.Yellow,
            Color.Gray,
            Color.Green,
        };

        // =====================================================================
        // Entry point
        // =====================================================================

        static void Main()
        {
            using (mgr_ = new WindowManager())
            {
                build_form_window_();
                build_log_window_();
                build_progress_window_();

                mgr_.OnFrame += on_frame_;

                log_.Append(timestamp_() + " INFO  TUI Framework started",       Color.Cyan);
                log_.Append(timestamp_() + " INFO  Press Tab to cycle focus",     Color.Gray);
                log_.Append(timestamp_() + " INFO  Alt+1/2/3 to switch windows",  Color.Gray);

                mgr_.Run();
            }
        }

        // =====================================================================
        // Window builders
        // =====================================================================

        private static void build_form_window_()
        {
            Window form_win = new Window(1, 1, 50, 12, "Input Form");
            form_win.BorderColor = Color.DarkCyan;
            form_win.FocusedBorderColor = Color.Cyan;

            Label name_label = new Label(1, 1, "Name   :", Color.White, Color.Black);
            Label email_label = new Label(1, 3, "Email  :", Color.White, Color.Black);

            name_box_ = new TextBox(10, 1, 30);
            name_box_.Placeholder = "Enter your name...";
            name_box_.TabIndex = 0;

            email_box_ = new TextBox(10, 3, 30);
            email_box_.Placeholder = "user@example.com";
            email_box_.TabIndex = 1;

            status_lbl_ = new Label(1, 5, new string(' ', 46), Color.DarkGray, Color.Black);

            Button submit_btn = new Button(10, 7, "Submit", Color.White, Color.DarkGreen);
            submit_btn.TabIndex = 2;
            submit_btn.Clicked += on_submit_clicked_;

            Button clear_btn = new Button(24, 7, "Clear", Color.White, Color.DarkRed);
            clear_btn.TabIndex = 3;
            clear_btn.Clicked += on_clear_clicked_;

            form_win.Add(name_label);
            form_win.Add(name_box_);
            form_win.Add(email_label);
            form_win.Add(email_box_);
            form_win.Add(status_lbl_);
            form_win.Add(submit_btn);
            form_win.Add(clear_btn);

            mgr_.AddWindow(form_win);
        }

        private static void build_log_window_()
        {
            Window log_win = new Window(52, 1, 50, 20, "Live Log");
            log_win.BorderColor = Color.DarkMagenta;
            log_win.FocusedBorderColor = Color.Magenta;

            log_ = new LogView(0, 0, 48, 15);
            log_.TabIndex = 0;

            Button clear_log_btn = new Button(0, 16, "Clear Log", Color.White, Color.DarkBlue);
            clear_log_btn.TabIndex = 1;
            clear_log_btn.Clicked += on_clear_log_clicked_;

            log_win.Add(log_);
            log_win.Add(clear_log_btn);

            mgr_.AddWindow(log_win);
        }

        private static void build_progress_window_()
        {
            Window progress_win = new Window(1, 14, 50, 8, "Progress Bars");
            progress_win.BorderColor = Color.DarkYellow;
            progress_win.FocusedBorderColor = Color.Yellow;

            Label cpu_lbl = new Label(1, 0, "CPU  ", Color.Gray, Color.Black);
            Label mem_lbl = new Label(1, 2, "MEM  ", Color.Gray, Color.Black);
            Label disk_lbl = new Label(1, 4, "DISK ", Color.Gray, Color.Black);

            pb1_ = new ProgressBar(1, 1, 44, Color.DarkGray);
            pb1_.FillColor = Color.Green;
            pb1_.Value = 0.0;

            pb2_ = new ProgressBar(1, 3, 44, Color.DarkGray);
            pb2_.FillColor = Color.Yellow;
            pb2_.Value = 0.0;

            pb3_ = new ProgressBar(1, 5, 44, Color.DarkGray);
            pb3_.FillColor = Color.Red;
            pb3_.Value = 0.0;

            progress_win.Add(cpu_lbl);
            progress_win.Add(pb1_);
            progress_win.Add(mem_lbl);
            progress_win.Add(pb2_);
            progress_win.Add(disk_lbl);
            progress_win.Add(pb3_);

            mgr_.AddWindow(progress_win);
        }

        // =====================================================================
        // Event handlers
        // =====================================================================

        private static void on_submit_clicked_()
        {
            status_lbl_.Text = "Submitted: " + name_box_.Text + " / " + email_box_.Text;
            status_lbl_.Foreground = Color.Green;
        }

        private static void on_clear_clicked_()
        {
            name_box_.Text = "";
            email_box_.Text = "";
            status_lbl_.Text = "Form cleared.";
            status_lbl_.Foreground = Color.Yellow;
        }

        private static void on_clear_log_clicked_()
        {
            log_.Clear();
        }

        private static void on_frame_()
        {
            frame_++;

            // Animate progress bars with sine waves
            pb1_.Value = 0.5  + 0.45 * System.Math.Sin(frame_ * 0.030);
            pb2_.Value = 0.3  + 0.30 * System.Math.Sin(frame_ * 0.017 + 1.0);
            pb3_.Value = 0.6  + 0.20 * System.Math.Sin(frame_ * 0.011 + 2.5);

            // Append a log line every ~60 frames
            if (frame_ % 60 == 0)
            {
                int idx = random_.Next(k_log_messages_.Length);
                log_.Append(timestamp_() + " " + k_log_messages_[idx], k_log_colors_[idx]);
            }

            mgr_.DrawStatusBar(
                " Tab=Next widget  Shift+Tab=Prev  Alt+1/2/3=Switch window  Ctrl+Q=Quit",
                Color.Black,    
                Color.DarkCyan);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static string timestamp_()
        {
            return "[" + System.DateTime.Now.ToString("HH:mm:ss") + "]";
        }
    }
}
