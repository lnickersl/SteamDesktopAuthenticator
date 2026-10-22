using System;
using System.Drawing;
using System.Windows.Forms;

namespace Steam_Desktop_Authenticator
{
    /// <summary>
    /// Small non-blocking notification: borderless, buttonless, semi-transparent
    /// rectangle in the bottom-right corner of the screen that closes itself
    /// after a short delay. Unlike MessageBox, it never blocks the caller.
    /// </summary>
    public class ToastForm : Form
    {
        private const int AutoCloseDelayMs = 2000;
        private readonly Timer closeTimer;

        public ToastForm(string message)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            Opacity = 0.75; // semi-transparent
            BackColor = Color.FromArgb(30, 30, 30);
            Size = new Size(260, 70);
            StartPosition = FormStartPosition.Manual;

            // Bottom-right corner of the working area (taskbar-aware), small margin
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - 10, area.Bottom - Height - 10);

            Label label = new Label()
            {
                Text = message,
                ForeColor = Color.Snow,
                AutoSize = false,
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(label);

            closeTimer = new Timer { Interval = AutoCloseDelayMs };
            closeTimer.Tick += (s, e) => Close();
            closeTimer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            closeTimer.Stop();
            closeTimer.Dispose();
            base.OnFormClosed(e);
        }
    }
}