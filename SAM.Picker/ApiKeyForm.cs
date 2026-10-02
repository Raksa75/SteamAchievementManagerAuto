/* Copyright (c) 2024 Rick (rick 'at' gibbed 'dot' us)
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using SAM.Common;

namespace SAM.Picker
{
    // Dialog to enter, test and save the optional Steam Web API key.
    internal sealed class ApiKeyForm : Form
    {
        private const string KeyPageUrl = "https://steamcommunity.com/dev/apikey";
        private const string PrivacyPageUrl = "https://steamcommunity.com/my/edit/settings";
        private const int ContentWidth = 440;

        private static readonly Regex KeyFormat = new("^[0-9A-Fa-f]{32}$");

        private readonly ulong _SteamId;
        private readonly TextBox _KeyTextBox;
        private readonly CheckBox _ShowKeyCheckBox;
        private readonly Label _StatusLabel;
        private readonly Button _TestButton;
        private readonly Button _SaveButton;

        public string ApiKey => NormalizeKey(this._KeyTextBox.Text);

        public ApiKeyForm(string currentKey, ulong steamId)
        {
            this._SteamId = steamId;

            this.SuspendLayout();
            // Sizes below are in 96-DPI pixels and scaled to the actual display.
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Text = "Steam Web API key";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.Padding = new Padding(20, 16, 20, 16);
            this.Font = Theme.UiFont;

            var layout = new TableLayoutPanel()
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Dock = DockStyle.Fill,
            };

            var title = new Label()
            {
                Text = "Steam Web API key",
                Font = Theme.TitleFont,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6),
            };

            var description = new Label()
            {
                Text =
                    "Optional. With a key, Auto-Unlock All reads your achievement progress " +
                    "from your Steam profile and skips games that are already 100%, which " +
                    "is much faster on large libraries.",
                AutoSize = true,
                MaximumSize = new Size(ContentWidth, 0),
                Margin = new Padding(0, 0, 0, 10),
            };

            var getKeyLink = CreateLink("Get a free key (any domain name works, e.g. \"localhost\")", KeyPageUrl);
            var privacyLink = CreateLink("Steam privacy settings: Profile and Game details must be Public", PrivacyPageUrl);
            privacyLink.Margin = new Padding(0, 2, 0, 12);

            this._KeyTextBox = new TextBox()
            {
                Text = currentKey ?? "",
                UseSystemPasswordChar = true,
                Width = ContentWidth - 80,
                Margin = new Padding(0, 0, 8, 0),
            };
            this._KeyTextBox.TextChanged += (sender, e) => this.SetStatus("", Theme.TextSecondary);

            this._ShowKeyCheckBox = new CheckBox()
            {
                Text = "Show",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
            };
            this._ShowKeyCheckBox.CheckedChanged += (sender, e) =>
                this._KeyTextBox.UseSystemPasswordChar = this._ShowKeyCheckBox.Checked == false;

            var keyRow = new FlowLayoutPanel()
            {
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0),
            };
            keyRow.Controls.Add(this._KeyTextBox);
            keyRow.Controls.Add(this._ShowKeyCheckBox);

            this._StatusLabel = new Label()
            {
                AutoSize = true,
                MaximumSize = new Size(ContentWidth, 0),
                MinimumSize = new Size(ContentWidth, 36),
                Margin = new Padding(0, 8, 0, 8),
            };

            this._TestButton = CreateButton("Test key");
            this._TestButton.Click += this.OnTest;

            this._SaveButton = CreateButton("Save");
            this._SaveButton.Click += this.OnSave;

            var cancelButton = CreateButton("Cancel");
            cancelButton.DialogResult = DialogResult.Cancel;

            var buttons = new FlowLayoutPanel()
            {
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                WrapContents = false,
            };
            buttons.Controls.Add(this._SaveButton);
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(this._TestButton);

            layout.Controls.Add(title);
            layout.Controls.Add(description);
            layout.Controls.Add(getKeyLink);
            layout.Controls.Add(privacyLink);
            layout.Controls.Add(keyRow);
            layout.Controls.Add(this._StatusLabel);
            layout.Controls.Add(buttons);
            this.Controls.Add(layout);

            this.AcceptButton = this._SaveButton;
            this.CancelButton = cancelButton;

            Theme.Apply(this);
            Theme.StylePrimaryButton(this._SaveButton);
            Theme.SetCueBanner(this._KeyTextBox, "32-character key");
            description.ForeColor = Theme.TextSecondary;

            this.SetStatus(
                string.IsNullOrWhiteSpace(currentKey) == true
                    ? "Leave empty to run without a key."
                    : "A key is saved. Clear the field and save to remove it.",
                Theme.TextSecondary);
            this.ResumeLayout(true);
        }

        private static LinkLabel CreateLink(string text, string url)
        {
            var link = new LinkLabel()
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 2),
            };
            link.LinkClicked += (sender, e) => OpenUrl(url);
            return link;
        }

        private static Button CreateButton(string text)
        {
            return new Button()
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(88, 30),
                Margin = new Padding(8, 0, 0, 0),
            };
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch (Exception)
            {
            }
        }

        private static string NormalizeKey(string text)
        {
            // Tolerate stray spaces or quotes from copy/paste.
            return (text ?? "").Trim().Trim('"', '\'').Trim();
        }

        private void SetStatus(string text, Color color)
        {
            this._StatusLabel.Text = text;
            this._StatusLabel.ForeColor = color;
        }

        private bool CheckFormat()
        {
            var key = this.ApiKey;
            if (key.Length == 0 || KeyFormat.IsMatch(key) == true)
            {
                return true;
            }

            this.SetStatus("That doesn't look like a Steam Web API key: it should be 32 characters (0-9, A-F).", Theme.Danger);
            this._KeyTextBox.Focus();
            return false;
        }

        private async void OnTest(object sender, EventArgs e)
        {
            if (this.ApiKey.Length == 0)
            {
                this.SetStatus("Paste a key first.", Theme.Warning);
                return;
            }

            if (this.CheckFormat() == false)
            {
                return;
            }

            if (this._SteamId == 0)
            {
                this.SetStatus("Couldn't determine your Steam account. Is Steam running?", Theme.Warning);
                return;
            }

            this._TestButton.Enabled = false;
            this._SaveButton.Enabled = false;
            this.SetStatus("Testing with Steam…", Theme.TextSecondary);

            var key = this.ApiKey;
            var steamId = this._SteamId;
            var status = await Task.Run(() => SteamWebApi.ValidateKey(key, steamId));

            if (this.IsDisposed == true)
            {
                return;
            }

            this._TestButton.Enabled = true;
            this._SaveButton.Enabled = true;

            switch (status)
            {
                case SteamWebApi.KeyStatus.Valid:
                    this.SetStatus("✓ The key works and your profile is public.", Theme.Success);
                    break;

                case SteamWebApi.KeyStatus.ProfilePrivate:
                    this.SetStatus(
                        "The key works, but your profile is private. Set Profile and Game details " +
                        "to Public (link above), otherwise completed games can't be detected.",
                        Theme.Warning);
                    break;

                case SteamWebApi.KeyStatus.InvalidKey:
                    this.SetStatus("✗ Steam rejected this key. Check that it was copied entirely.", Theme.Danger);
                    break;

                default:
                    this.SetStatus("Couldn't reach Steam. Check your internet connection and try again.", Theme.Warning);
                    break;
            }
        }

        private void OnSave(object sender, EventArgs e)
        {
            if (this.CheckFormat() == true)
            {
                this.DialogResult = DialogResult.OK;
            }
        }
    }
}
