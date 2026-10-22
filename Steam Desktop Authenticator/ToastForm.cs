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
        private const int AutoCloseDelayMs = 3000;
        private readonly Timer closeTimer;
        private static ToastForm current; // only one toast on screen at a time

        public ToastForm(string message)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            Opacity = 0.75; // semi-transparent
            BackColor = Color.FromArgb(30, 30, 30);
            StartPosition = FormStartPosition.Manual;

            // Bottom-right corner of the working area (taskbar-aware), small margin
            Rectangle area = Screen.PrimaryScreen.WorkingArea;

            // Auto-height so longer warning texts stay readable (never taller than 1/3 of the screen)
            Size textSize = TextRenderer.MeasureText(message, Font,
                new Size(276, area.Height / 3),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            Width = 300;
            Height = Math.Max(70, textSize.Height + 24);
            Location = new Point(area.Right - Width - 10, area.Bottom - Height - 10);

            // Replace a toast that is still on screen so they never overlap
            current?.Close();
            current = this;

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
            if (current == this)
            {
                current = null;
            }
            base.OnFormClosed(e);
        }
    }
}