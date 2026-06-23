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
using System.Drawing;
using System.Windows.Forms;

namespace SAM.Common
{
    // A small, self-contained dark theme ("sober dark + blue accent") shared by
    // both SAM.Picker and SAM.Game. Apply() recolours an existing form/control
    // tree, and DarkToolStripRenderer handles tool strips, menus and status bars.
    internal static class Theme
    {
        // Surfaces (deep charcoal, never pure black).
        public static readonly Color Background = Color.FromArgb(24, 25, 28);
        public static readonly Color Surface = Color.FromArgb(32, 34, 38);
        public static readonly Color SurfaceAlt = Color.FromArgb(40, 42, 47);
        public static readonly Color Hover = Color.FromArgb(45, 55, 74);
        public static readonly Color Border = Color.FromArgb(52, 54, 60);

        // Text.
        public static readonly Color TextPrimary = Color.FromArgb(230, 231, 234);
        public static readonly Color TextSecondary = Color.FromArgb(150, 153, 160);

        // A single, sober blue accent.
        public static readonly Color Accent = Color.FromArgb(76, 141, 255);
        public static readonly Color AccentHover = Color.FromArgb(104, 160, 255);
        public static readonly Color AccentPressed = Color.FromArgb(58, 120, 224);

        // A muted red used to mark protected/online achievements.
        public static readonly Color DangerSurface = Color.FromArgb(74, 38, 42);

        public static void Apply(Form form)
        {
            if (form == null)
            {
                return;
            }

            form.BackColor = Background;
            form.ForeColor = TextPrimary;

            foreach (Control child in form.Controls)
            {
                ApplyControl(child);
            }
        }

        private static void ApplyControl(Control control)
        {
            switch (control)
            {
                case MenuStrip menu:
                    menu.BackColor = Background;
                    menu.ForeColor = TextPrimary;
                    ApplyToolStripItems(menu);
                    break;

                case StatusStrip status:
                    status.BackColor = Background;
                    status.ForeColor = TextSecondary;
                    ApplyToolStripItems(status);
                    break;

                case ToolStrip strip:
                    strip.BackColor = Background;
                    strip.ForeColor = TextPrimary;
                    ApplyToolStripItems(strip);
                    break;

                case TabControl tab:
                    StyleTabControl(tab);
                    break;

                case ListView list:
                    list.BackColor = Surface;
                    list.ForeColor = TextPrimary;
                    list.BorderStyle = BorderStyle.None;
                    break;

                case DataGridView grid:
                    StyleGrid(grid);
                    break;

                case TextBoxBase text:
                    text.BackColor = Surface;
                    text.ForeColor = TextPrimary;
                    text.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case LinkLabel link:
                    link.BackColor = Background;
                    link.ForeColor = TextPrimary;
                    link.LinkColor = Accent;
                    link.ActiveLinkColor = AccentHover;
                    link.VisitedLinkColor = Accent;
                    break;

                case Button button:
                    StyleButton(button);
                    break;

                case CheckBox check:
                    check.BackColor = Background;
                    check.ForeColor = TextPrimary;
                    check.FlatStyle = FlatStyle.Flat;
                    break;

                case Label label:
                    label.BackColor = Color.Transparent;
                    label.ForeColor = TextPrimary;
                    break;

                default:
                    control.BackColor = Background;
                    control.ForeColor = TextPrimary;
                    break;
            }

            foreach (Control child in control.Controls)
            {
                ApplyControl(child);
            }
        }

        private static void ApplyToolStripItems(ToolStrip strip)
        {
            foreach (ToolStripItem item in strip.Items)
            {
                ApplyToolStripItem(item);
            }
        }

        private static void ApplyToolStripItem(ToolStripItem item)
        {
            switch (item)
            {
                case ToolStripTextBox box:
                    box.BackColor = Surface;
                    box.ForeColor = TextPrimary;
                    box.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case ToolStripStatusLabel status:
                    status.ForeColor = TextSecondary;
                    break;

                case ToolStripDropDownItem dropDown:
                    dropDown.ForeColor = TextPrimary;
                    foreach (ToolStripItem child in dropDown.DropDownItems)
                    {
                        ApplyToolStripItem(child);
                    }
                    break;

                default:
                    item.ForeColor = TextPrimary;
                    break;
            }
        }

        public static void StyleButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Surface;
            button.ForeColor = TextPrimary;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = SurfaceAlt;
            button.FlatAppearance.MouseDownBackColor = Hover;
            button.UseVisualStyleBackColor = false;
        }

        public static void StylePrimaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Accent;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = Accent;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = AccentHover;
            button.FlatAppearance.MouseDownBackColor = AccentPressed;
            button.UseVisualStyleBackColor = false;
        }

        public static void StyleGrid(DataGridView grid)
        {
            grid.EnableHeadersVisualStyles = false;
            grid.BackgroundColor = Surface;
            grid.GridColor = Border;
            grid.BorderStyle = BorderStyle.None;
            grid.ForeColor = TextPrimary;

            grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceAlt;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SurfaceAlt;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextPrimary;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

            grid.RowHeadersDefaultCellStyle.BackColor = SurfaceAlt;
            grid.RowHeadersDefaultCellStyle.ForeColor = TextPrimary;
            grid.RowHeadersDefaultCellStyle.SelectionBackColor = SurfaceAlt;

            grid.DefaultCellStyle.BackColor = Surface;
            grid.DefaultCellStyle.ForeColor = TextPrimary;
            grid.DefaultCellStyle.SelectionBackColor = Accent;
            grid.DefaultCellStyle.SelectionForeColor = Color.White;

            grid.AlternatingRowsDefaultCellStyle.BackColor = SurfaceAlt;
            grid.AlternatingRowsDefaultCellStyle.ForeColor = TextPrimary;
        }

        private static void StyleTabControl(TabControl tab)
        {
            tab.DrawMode = TabDrawMode.OwnerDrawFixed;
            tab.SizeMode = TabSizeMode.Fixed;
            tab.ItemSize = new Size(120, 28);
            tab.DrawItem -= OnDrawTabItem;
            tab.DrawItem += OnDrawTabItem;

            foreach (TabPage page in tab.TabPages)
            {
                page.BackColor = Background;
                page.ForeColor = TextPrimary;
            }
        }

        private static void OnDrawTabItem(object sender, DrawItemEventArgs e)
        {
            var tab = (TabControl)sender;
            if (e.Index < 0 || e.Index >= tab.TabPages.Count)
            {
                return;
            }

            var page = tab.TabPages[e.Index];
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using (var background = new SolidBrush(selected ? Surface : Background))
            {
                e.Graphics.FillRectangle(background, e.Bounds);
            }

            if (selected == true)
            {
                using var accent = new SolidBrush(Accent);
                e.Graphics.FillRectangle(accent, e.Bounds.Left, e.Bounds.Bottom - 2, e.Bounds.Width, 2);
            }

            TextRenderer.DrawText(
                e.Graphics,
                page.Text,
                tab.Font,
                e.Bounds,
                selected ? TextPrimary : TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // Gives a Details-view ListView dark column headers while leaving item
        // drawing (checkboxes, icons, per-item colours) to the system.
        public static void StyleDetailsHeaders(ListView list)
        {
            list.OwnerDraw = true;
            list.DrawColumnHeader -= OnDrawColumnHeader;
            list.DrawColumnHeader += OnDrawColumnHeader;
            list.DrawItem -= OnDrawListItemDefault;
            list.DrawItem += OnDrawListItemDefault;
            list.DrawSubItem -= OnDrawListSubItemDefault;
            list.DrawSubItem += OnDrawListSubItemDefault;
        }

        private static void OnDrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var background = new SolidBrush(SurfaceAlt))
            {
                e.Graphics.FillRectangle(background, e.Bounds);
            }

            using (var separator = new Pen(Border))
            {
                e.Graphics.DrawLine(separator, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
                e.Graphics.DrawLine(separator, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            var textBounds = e.Bounds;
            textBounds.X += 6;
            TextRenderer.DrawText(
                e.Graphics,
                e.Header.Text,
                e.Font ?? SystemFonts.DefaultFont,
                textBounds,
                TextSecondary,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        private static void OnDrawListItemDefault(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private static void OnDrawListSubItemDefault(object sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = true;
        }
    }

    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        public DarkColorTable()
        {
            this.UseSystemColors = false;
        }

        public override Color ToolStripGradientBegin => Theme.Background;
        public override Color ToolStripGradientMiddle => Theme.Background;
        public override Color ToolStripGradientEnd => Theme.Background;
        public override Color ToolStripBorder => Theme.Border;
        public override Color ToolStripContentPanelGradientBegin => Theme.Background;
        public override Color ToolStripContentPanelGradientEnd => Theme.Background;
        public override Color ToolStripPanelGradientBegin => Theme.Background;
        public override Color ToolStripPanelGradientEnd => Theme.Background;

        public override Color MenuStripGradientBegin => Theme.Background;
        public override Color MenuStripGradientEnd => Theme.Background;

        public override Color ImageMarginGradientBegin => Theme.Surface;
        public override Color ImageMarginGradientMiddle => Theme.Surface;
        public override Color ImageMarginGradientEnd => Theme.Surface;

        public override Color ToolStripDropDownBackground => Theme.Surface;
        public override Color MenuBorder => Theme.Border;
        public override Color MenuItemBorder => Theme.Accent;
        public override Color MenuItemSelected => Theme.Hover;
        public override Color MenuItemSelectedGradientBegin => Theme.Hover;
        public override Color MenuItemSelectedGradientEnd => Theme.Hover;
        public override Color MenuItemPressedGradientBegin => Theme.Surface;
        public override Color MenuItemPressedGradientEnd => Theme.Surface;

        public override Color ButtonSelectedGradientBegin => Theme.Hover;
        public override Color ButtonSelectedGradientMiddle => Theme.Hover;
        public override Color ButtonSelectedGradientEnd => Theme.Hover;
        public override Color ButtonSelectedBorder => Theme.Accent;
        public override Color ButtonPressedGradientBegin => Theme.AccentPressed;
        public override Color ButtonPressedGradientMiddle => Theme.AccentPressed;
        public override Color ButtonPressedGradientEnd => Theme.AccentPressed;
        public override Color ButtonPressedBorder => Theme.Accent;
        public override Color ButtonCheckedGradientBegin => Theme.AccentPressed;
        public override Color ButtonCheckedGradientMiddle => Theme.AccentPressed;
        public override Color ButtonCheckedGradientEnd => Theme.AccentPressed;
        public override Color CheckBackground => Theme.AccentPressed;
        public override Color CheckSelectedBackground => Theme.Accent;
        public override Color CheckPressedBackground => Theme.AccentPressed;

        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Border;
        public override Color GripDark => Theme.Border;
        public override Color GripLight => Theme.Border;
    }

    internal sealed class DarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        public DarkToolStripRenderer()
            : base(new DarkColorTable())
        {
            this.RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var background = new SolidBrush(Theme.Background);
            e.Graphics.FillRectangle(background, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            // Keep tool strips flat (no etched border).
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled == true ? Theme.TextPrimary : Theme.TextSecondary;
            base.OnRenderItemText(e);
        }
    }
}
