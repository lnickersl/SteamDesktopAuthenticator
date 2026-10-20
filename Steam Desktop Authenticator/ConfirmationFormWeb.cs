using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using SteamAuth;

namespace Steam_Desktop_Authenticator
{
    public partial class ConfirmationFormWeb : Form
    {
        private SteamGuardAccount steamAccount;
        private readonly Dictionary<string, Image> iconCache = new Dictionary<string, Image>();
        private static readonly HttpClient httpClient = new HttpClient();

        public ConfirmationFormWeb(SteamGuardAccount steamAccount)
        {
            InitializeComponent();
            this.steamAccount = steamAccount;
            this.Text = String.Format("Trade Confirmations - {0}", steamAccount.AccountName);
        }
        private async Task LoadData()
        {
            // Check for a valid refresh token first
            if (steamAccount.Session.IsRefreshTokenExpired())
            {
                MessageBox.Show("Your session has expired. Use the login again button under the selected account menu.", "Trade Confirmations", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                return;
            }

            // Check for a valid access token, refresh it if needed
            if (steamAccount.Session.IsAccessTokenExpired())
            {
                try
                {
                    await steamAccount.Session.RefreshAccessToken();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Steam Login Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.Close();
                    return;
                }
            }

            try
            {
                var confirmations = await steamAccount.FetchConfirmationsAsync();

                // Build all rows first; the current list stays visible until the swap below
                var rows = new List<Control>();

                if (confirmations == null || confirmations.Length == 0)
                {
                    Label errorLabel = new Label() { Text = "Nothing to confirm/cancel", AutoSize = true, ForeColor = Color.Black, Location = new Point(150, 20) };
                    rows.Add(errorLabel);
                }

                foreach (var confirmation in confirmations ?? Array.Empty<Confirmation>())
                {
                    // Anchored instead of docked: AutoScroll does not work with docked controls
                    Panel panel = new Panel() { Height = 120, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                    panel.Paint += (s, e) =>
                    {
                        using (LinearGradientBrush brush = new LinearGradientBrush(panel.ClientRectangle, Color.Black, Color.DarkCyan, 90F))
                        {
                            e.Graphics.FillRectangle(brush, panel.ClientRectangle);
                        }
                    };

                    if (!string.IsNullOrEmpty(confirmation.Icon))
                    {
                        PictureBox pictureBox = new PictureBox() { Width = 60, Height = 60, Location = new Point(20, 20), SizeMode = PictureBoxSizeMode.Zoom };
                        panel.Controls.Add(pictureBox);
                        // Icons are loaded asynchronously after the row is shown, never blocking the UI
                        _ = LoadIconAsync(pictureBox, confirmation.Icon);
                    }

                    Label nameLabel = new Label()
                    {
                        Text = $"{confirmation.Headline}\n{confirmation.Creator.ToString()}",
                        AutoSize = true,
                        ForeColor = Color.Snow,
                        Location = new Point(90, 20),
                        BackColor = Color.Transparent
                    };
                    panel.Controls.Add(nameLabel);

                    ConfirmationButton acceptButton = new ConfirmationButton()
                    {
                        Text = confirmation.Accept,
                        Location = new Point(90, 50),
                        FlatStyle = FlatStyle.Flat,
                        FlatAppearance = { BorderSize = 0 },
                        BackColor = Color.Black,
                        ForeColor = Color.Snow,
                        Confirmation = confirmation
                    };
                    acceptButton.Click += btnAccept_Click;
                    panel.Controls.Add(acceptButton);

                    ConfirmationButton cancelButton = new ConfirmationButton()
                    {
                        Text = confirmation.Cancel,
                        Location = new Point(180, 50),
                        FlatStyle = FlatStyle.Flat,
                        FlatAppearance = { BorderSize = 0 },
                        BackColor = Color.Black,
                        ForeColor = Color.Snow,
                        Confirmation = confirmation
                    };
                    cancelButton.Click += btnCancel_Click;
                    panel.Controls.Add(cancelButton);

                    Label summaryLabel = new Label()
                    {
                        Text = String.Join("\n", confirmation.Summary),
                        AutoSize = true,
                        ForeColor = Color.Snow,
                        Location = new Point(90, 80),
                        BackColor = Color.Transparent
                    };
                    panel.Controls.Add(summaryLabel);

                    rows.Add(panel);
                }

                // Swap old rows for new ones in a single layout pass
                var container = this.splitContainer1.Panel2;
                container.SuspendLayout();
                try
                {
                    foreach (Control old in container.Controls.Cast<Control>().ToArray())
                    {
                        old.Dispose();
                    }
                    container.Controls.Clear();

                    foreach (var row in rows)
                    {
                        container.Controls.Add(row);
                    }
                    LayoutConfirmationRows(container);
                }
                finally
                {
                    container.ResumeLayout(true);
                }
            }
            catch (Exception ex)
            {
                Label errorLabel = new Label() { Text = "Something went wrong:\n" + ex.Message, AutoSize = true, ForeColor = Color.Red, Location = new Point(20, 20) };
                this.splitContainer1.Panel2.Controls.Add(errorLabel);
            }
        }

        /// <summary>
        /// Stacks confirmation rows top-to-bottom manually. Replaces Dock=Top (AutoScroll
        /// ignores docked controls) while preserving its visual order: last added row on top.
        /// </summary>
        private void LayoutConfirmationRows(Control container)
        {
            var rowPanels = container.Controls.Cast<Control>()
                .Where(c => c is Panel)
                .ToList();

            int y = 0;
            int width = container.ClientSize.Width;
            // Iterate in reverse add order so the most recently added row renders at the top,
            // matching the previous Dock=Top stacking behavior.
            for (int i = rowPanels.Count - 1; i >= 0; i--)
            {
                Control row = rowPanels[i];
                row.Location = new Point(0, y);
                row.Size = new Size(width, row.Height);
                y += row.Height;
            }
        }

        /// <summary>
        /// Loads a confirmation icon off the UI thread, reusing cached images when possible.
        /// </summary>
        private async Task LoadIconAsync(PictureBox target, string url)
        {
            try
            {
                Image image;
                if (iconCache.TryGetValue(url, out image))
                {
                    // Cached image may have been disposed with its previous row - refetch if so
                    try
                    {
                        var _ = image.Width;
                    }
                    catch (ObjectDisposedException)
                    {
                        iconCache.Remove(url);
                        image = null;
                    }
                }

                if (image == null)
                {
                    byte[] bytes = await httpClient.GetByteArrayAsync(url);
                    using (var stream = new System.IO.MemoryStream(bytes))
                    using (var loaded = System.Drawing.Image.FromStream(stream))
                    {
                        image = new System.Drawing.Bitmap(loaded); // detach from the stream
                    }
                    iconCache[url] = image;
                }

                if (!target.IsDisposed && !this.IsDisposed)
                {
                    target.Image = image;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to load avatar: " + ex.Message);
            }
        }

        private async void btnAccept_Click(object sender, EventArgs e)
        {
            await HandleConfirmationAction(sender, accept: true);
        }

        private async void btnCancel_Click(object sender, EventArgs e)
        {
            await HandleConfirmationAction(sender, accept: false);
        }

        /// <summary>
        /// Sends a single confirmation action; on success removes only that row from the list
        /// instead of reloading everything.
        /// </summary>
        private async Task HandleConfirmationAction(object sender, bool accept)
        {
            var button = (ConfirmationButton)sender;
            var confirmation = button.Confirmation;
            var row = button.Parent;

            // Prevent double-clicks while the request is in flight
            button.Enabled = false;
            try
            {
                bool result = accept
                    ? await steamAccount.AcceptConfirmation(confirmation)
                    : await steamAccount.DenyConfirmation(confirmation);

                if (!result)
                {
                    MessageBox.Show(
                        accept ? "Failed to accept this confirmation." : "Failed to cancel this confirmation.",
                        "Trade Confirmations", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    button.Enabled = true;
                    return;
                }

                // Action succeeded - drop just this row, no network reload needed
                if (row != null && row.Parent != null)
                {
                    var container = row.Parent;
                    container.Controls.Remove(row);
                    row.Dispose();

                    // Show empty state if that was the last confirmation
                    if (!container.Controls.OfType<Panel>().Any())
                    {
                        Label emptyLabel = new Label() { Text = "Nothing to confirm/cancel", AutoSize = true, ForeColor = Color.Black, Location = new Point(150, 20) };
                        container.Controls.Add(emptyLabel);
                    }
                    else
                    {
                        LayoutConfirmationRows(container);
                    }
                }
            }
            catch (Exception ex)
            {
                button.Enabled = true;
                MessageBox.Show("Something went wrong:\n" + ex.Message, "Trade Confirmations", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            this.btnRefresh.Enabled = false;
            this.btnRefresh.Text = "Refreshing...";

            await this.LoadData();

            this.btnRefresh.Enabled = true;
            this.btnRefresh.Text = "Refresh";
        }

        private async void ConfirmationFormWeb_Shown(object sender, EventArgs e)
        {
            this.btnRefresh.Enabled = false;
            this.btnRefresh.Text = "Refreshing...";

            await this.LoadData();

            this.btnRefresh.Enabled = true;
            this.btnRefresh.Text = "Refresh";
        }
    }
}
