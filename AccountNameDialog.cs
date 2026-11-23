using System;
using System.Drawing;
using System.Windows.Forms;

namespace WhatsLite
{
    public class AccountNameDialog : Form
    {
        private TextBox _txtInput;
        private Button _btnOk;
        private Button _btnCancel;

        public string AccountName
        {
            get { return _txtInput.Text.Trim(); }
        }

        public AccountNameDialog(string title, string promptText, string defaultName)
        {
            this.Text = title;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(400, 180);
            this.BackColor = Color.FromArgb(32, 44, 51); // WhatsApp dark theme
            this.ForeColor = Color.FromArgb(233, 237, 239);

            Label lblPrompt = new Label();
            lblPrompt.Text = promptText;
            lblPrompt.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            lblPrompt.Location = new Point(20, 15);
            lblPrompt.MaximumSize = new Size(360, 0);
            lblPrompt.AutoSize = true;

            _txtInput = new TextBox();
            _txtInput.Text = defaultName ?? "";
            _txtInput.Font = new Font("Segoe UI", 10.0f);
            _txtInput.BackColor = Color.FromArgb(42, 57, 66);
            _txtInput.ForeColor = Color.White;
            _txtInput.BorderStyle = BorderStyle.FixedSingle;
            _txtInput.Location = new Point(20, 72);
            _txtInput.Size = new Size(360, 26);

            _btnOk = new Button();
            _btnOk.Text = "OK";
            _btnOk.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            _btnOk.BackColor = Color.FromArgb(37, 211, 102); // #25D366 Green
            _btnOk.ForeColor = Color.White;
            _btnOk.FlatStyle = FlatStyle.Flat;
            _btnOk.FlatAppearance.BorderSize = 0;
            _btnOk.Location = new Point(195, 125);
            _btnOk.Size = new Size(85, 32);
            _btnOk.Click += delegate {
                if (string.IsNullOrEmpty(_txtInput.Text.Trim()))
                {
                    MessageBox.Show("Please enter a valid account name.", "WhatsLite", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            _btnCancel = new Button();
            _btnCancel.Text = "Cancel";
            _btnCancel.Font = new Font("Segoe UI", 9.0f);
            _btnCancel.BackColor = Color.FromArgb(42, 57, 66);
            _btnCancel.ForeColor = Color.FromArgb(233, 237, 239);
            _btnCancel.FlatStyle = FlatStyle.Flat;
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Location = new Point(295, 125);
            _btnCancel.Size = new Size(85, 32);
            _btnCancel.DialogResult = DialogResult.Cancel;
            _btnCancel.Click += delegate { this.Close(); };

            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;

            this.Controls.Add(lblPrompt);
            this.Controls.Add(_txtInput);
            this.Controls.Add(_btnOk);
            this.Controls.Add(_btnCancel);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ThemeHelper.ApplyTitleBarTheme(this.Handle, ThemeHelper.IsWindowsDarkTheme());
        }
    }
}
