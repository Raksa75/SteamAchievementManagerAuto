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

using System.Drawing;
using System.Windows.Forms;

namespace SAM.Common
{
    // A TabControl painted entirely by us. Owner-drawing the tabs alone still
    // leaves the native light strip next to the tabs and a 3D border around the
    // pages, which look broken in a dark theme.
    internal sealed class DarkTabControl : TabControl
    {
        public DarkTabControl()
        {
            this.SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            this.SizeMode = TabSizeMode.Fixed;
            this.ItemSize = new Size(130, 32);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            using (var background = new SolidBrush(Theme.Background))
            {
                graphics.FillRectangle(background, this.ClientRectangle);
            }

            if (this.TabCount == 0)
            {
                return;
            }

            // Hairline under the tab row, spanning the whole width.
            var tabRow = this.GetTabRect(0);
            using (var line = new Pen(Theme.Border))
            {
                graphics.DrawLine(line, 0, tabRow.Bottom, this.Width, tabRow.Bottom);
            }

            for (int i = 0; i < this.TabCount; i++)
            {
                var bounds = this.GetTabRect(i);
                bool selected = i == this.SelectedIndex;

                if (selected == true)
                {
                    using var surface = new SolidBrush(Theme.Surface);
                    graphics.FillRectangle(surface, bounds);
                    using var accent = new SolidBrush(Theme.Accent);
                    graphics.FillRectangle(accent, bounds.Left, bounds.Bottom - 2, bounds.Width, 3);
                }

                TextRenderer.DrawText(
                    graphics,
                    this.TabPages[i].Text,
                    this.Font,
                    bounds,
                    selected == true ? Theme.TextPrimary : Theme.TextSecondary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void OnSelectedIndexChanged(System.EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            this.Invalidate();
        }
    }
}
