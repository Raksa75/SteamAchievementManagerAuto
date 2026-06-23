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
using System.Windows.Forms;

namespace SAM.Picker
{
    // A small, code-built modal dialog for entering/saving the Steam Web API key.
    internal sealed class ApiKeyForm : Form
    {
        private readonly TextBox _KeyTextBox;

        public string ApiKey => this._KeyTextBox.Text.Trim();

        public ApiKeyForm(string current)
        {
            this.Text = "Steam Web API Key";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.ClientSize = new Size(470, 188);

            var info = new Label()
            {
                Text =
                    "Paste your Steam Web API key. It lets Auto-Unlock All read your " +
                    "achievement progress so games that are already 100% (or have no " +
                    "achievements) can be skipped.\n\nYour Steam profile and game " +
                    "details must be set to public.",
                AutoSize = false,
                Bounds = new Rectangle(16, 14, 438, 66),
            };

            var link = new LinkLabel()
            {
                Text = "Get a key at steamcommunity.com/dev/apikey",
                AutoSize = true,
                Location = new Point(16, 86),
            };
            link.LinkClicked += (sender, e) =>
            {
                try
                {
                    Process.Start("https://steamcommunity.com/dev/apikey");
                }
                catch (Exception)
                {
                }
            };

            this._KeyTextBox = new TextBox()
            {
                Bounds = new Rectangle(16, 112, 438, 24),
                Text = current ?? "",
            };

            var save = new Button()
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                Bounds = new Rectangle(294, 146, 75, 30),
            };

            var cancel = new Button()
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Bounds = new Rectangle(379, 146, 75, 30),
            };

            this.Controls.Add(info);
            this.Controls.Add(link);
            this.Controls.Add(this._KeyTextBox);
            this.Controls.Add(save);
            this.Controls.Add(cancel);

            this.AcceptButton = save;
            this.CancelButton = cancel;

            Common.Theme.Apply(this);
            Common.Theme.StylePrimaryButton(save);
        }
    }
}
