using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WhatsLite
{
    public class AboutForm : Form
    {
        public AboutForm(Bitmap appIconBmp)
        {
            this.Text = "About WhatsLite";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(500, 490);
            this.BackColor = Color.FromArgb(17, 27, 33); // #111b21 WhatsApp dark
            this.ForeColor = Color.FromArgb(233, 237, 239); // #e9edef

            // 1. Header with Icon and App Name
            PictureBox picIcon = new PictureBox();
            picIcon.Location = new Point(24, 20);
            picIcon.Size = new Size(56, 56);
            picIcon.SizeMode = PictureBoxSizeMode.Zoom;
            if (appIconBmp != null)
            {
                picIcon.Image = appIconBmp;
            }

            Label lblTitle = new Label();
            lblTitle.Text = "WhatsLite";
            lblTitle.Font = new Font("Segoe UI", 16.5f, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(233, 237, 239);
            lblTitle.Location = new Point(92, 18);
            lblTitle.AutoSize = true;

            Label lblVersion = new Label();
            lblVersion.Text = "Version 1.3 — Native Desktop Client";
            lblVersion.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            lblVersion.ForeColor = Color.FromArgb(0, 168, 132); // #00a884 WhatsApp green
            lblVersion.Location = new Point(94, 54);
            lblVersion.AutoSize = true;

            // 2. Creator Card
            Panel pnlCreator = new Panel();
            pnlCreator.Location = new Point(24, 94);
            pnlCreator.Size = new Size(452, 74);
            pnlCreator.BackColor = Color.FromArgb(32, 44, 51); // #202c33
            pnlCreator.Paint += delegate(object s, PaintEventArgs e) {
                using (Pen borderPen = new Pen(Color.FromArgb(42, 57, 66), 1))
                {
                    e.Graphics.DrawRectangle(borderPen, 0, 0, pnlCreator.Width - 1, pnlCreator.Height - 1);
                }
            };

            Label lblCreatedBy = new Label();
            lblCreatedBy.Text = "CREATED & DEVELOPED BY";
            lblCreatedBy.UseMnemonic = false; // Show literal '&' without Windows mnemonic hiding
            lblCreatedBy.Font = new Font("Segoe UI", 8.0f, FontStyle.Bold);
            lblCreatedBy.ForeColor = Color.FromArgb(134, 150, 160); // #8696a0
            lblCreatedBy.Location = new Point(16, 12);
            lblCreatedBy.AutoSize = true;

            Label lblAuthor = new Label();
            lblAuthor.Text = "Mr. Dipendu Das";
            lblAuthor.Font = new Font("Segoe UI", 13.5f, FontStyle.Bold);
            lblAuthor.ForeColor = Color.FromArgb(0, 168, 132); // WhatsApp Green
            lblAuthor.Location = new Point(15, 32);
            lblAuthor.AutoSize = true;

            pnlCreator.Controls.Add(lblCreatedBy);
            pnlCreator.Controls.Add(lblAuthor);

            // 3. Description Card
            Panel pnlDesc = new Panel();
            pnlDesc.Location = new Point(24, 184);
            pnlDesc.Size = new Size(452, 226);
            pnlDesc.BackColor = Color.FromArgb(32, 44, 51);
            pnlDesc.Paint += delegate(object s, PaintEventArgs e) {
                using (Pen borderPen = new Pen(Color.FromArgb(42, 57, 66), 1))
                {
                    e.Graphics.DrawRectangle(borderPen, 0, 0, pnlDesc.Width - 1, pnlDesc.Height - 1);
                }
            };

            Label lblDescHeader = new Label();
            lblDescHeader.Text = "ABOUT THIS APP";
            lblDescHeader.Font = new Font("Segoe UI", 8.0f, FontStyle.Bold);
            lblDescHeader.ForeColor = Color.FromArgb(134, 150, 160);
            lblDescHeader.Location = new Point(16, 12);
            lblDescHeader.AutoSize = true;

            Label lblDescText = new Label();
            lblDescText.Text = "WhatsLite is an ultra-fast, native Windows desktop client for WhatsApp Web built using Microsoft's evergreen WebView2 engine.\n\n• True multi-account isolation with independent logins\n• Active-only notifications & quiet background accounts\n• Dedicated offline screen with auto-reconnection\n• Sub-second startup & zero bundled Electron bloat";
            lblDescText.Font = new Font("Segoe UI", 9.25f);
            lblDescText.ForeColor = Color.FromArgb(209, 215, 219);
            lblDescText.Location = new Point(16, 36);
            lblDescText.Size = new Size(420, 175);

            pnlDesc.Controls.Add(lblDescHeader);
            pnlDesc.Controls.Add(lblDescText);

            // 4. Back Button
            Button btnBack = new Button();
            btnBack.Text = "←  Back";
            btnBack.Font = new Font("Segoe UI", 10.0f, FontStyle.Bold);
            btnBack.BackColor = Color.FromArgb(0, 168, 132); // #00a884 WhatsApp Green
            btnBack.ForeColor = Color.White;
            btnBack.FlatStyle = FlatStyle.Flat;
            btnBack.FlatAppearance.BorderSize = 0;
            btnBack.Location = new Point(346, 426);
            btnBack.Size = new Size(130, 42);
            btnBack.Cursor = Cursors.Hand;
            btnBack.DialogResult = DialogResult.OK;
            btnBack.Click += delegate { this.Close(); };

            this.AcceptButton = btnBack;
            this.CancelButton = btnBack;

            this.Controls.Add(picIcon);
            this.Controls.Add(lblTitle);
            this.Controls.Add(lblVersion);
            this.Controls.Add(pnlCreator);
            this.Controls.Add(pnlDesc);
            this.Controls.Add(btnBack);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ThemeHelper.ApplyTitleBarTheme(this.Handle, ThemeHelper.IsWindowsDarkTheme());
        }
    }
}
