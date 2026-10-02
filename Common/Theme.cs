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
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SAM.Common
{
    // A small, self-contained dark theme ("sober dark + blue accent") shared by
    // both SAM.Picker and SAM.Game. Apply() recolours an existing form/control
    // tree, and DarkToolStripRenderer handles tool strips, menus and status bars.
    //
    // Native touches (dark title bar, dark scrollbars, cue banners, progress bar
    // colours) use Win32 calls that silently do nothing where unsupported.
    internal static class Theme
    {
        // Surfaces (deep charcoal, never pure black).
        public static readonly Color Background = Color.FromArgb(24, 25, 28);
        public static readonly Color Surface = Color.FromArgb(32, 34, 38);
        public static readonly Color SurfaceAlt = Color.FromArgb(40, 42, 47);
        public static readonly Color Hover = Color.FromArgb(45, 55, 74);
        public static readonly Color Border = Color.FromArgb(52, 54, 60);
        public static readonly Color Input = Color.FromArgb(46, 48, 55);

        // Text.
        public static readonly Color TextPrimary = Color.FromArgb(230, 231, 234);
        public static readonly Color TextSecondary = Color.FromArgb(150, 153, 160);

        // A single, sober blue accent.
        public static readonly Color Accent = Color.FromArgb(76, 141, 255);
        public static readonly Color AccentHover = Color.FromArgb(104, 160, 255);
        public static readonly Color AccentPressed = Color.FromArgb(58, 120, 224);

        // Status colours (used sparingly, for messages).
        public static readonly Color Success = Color.FromArgb(94, 201, 131);
        public static readonly Color Warning = Color.FromArgb(235, 182, 84);
        public static readonly Color Danger = Color.FromArgb(240, 104, 104);

        // A muted red used to mark protected/online achievements.
        public static readonly Color DangerSurface = Color.FromArgb(74, 38, 42);

        public static readonly Font UiFont = new("Segoe UI", 9F);
        public static readonly Font TitleFont = new("Segoe UI Semibold", 12F);

        public static void Apply(Form form)
        {
            if (form == null)
            {
                return;
            }

            // With AutoScaleMode.Font this also rescales the layout to the new font.
            form.Font = UiFont;
            form.BackColor = Background;
            form.ForeColor = TextPrimary;
            WhenHandleCreated(form, UseDarkTitleBar);

            foreach (Control child in form.Controls)
            {
                ApplyControl(child);
            }
        }

        private static void ApplyControl(Control control)
        {
            switch (control)
            {
                case StatusStrip status:
                    status.BackColor = Background;
                    status.ForeColor = TextSecondary;
                    ApplyToolStripItems(status);
                    break;

                case ToolStrip strip:
                    strip.BackColor = Background;
                    strip.ForeColor = TextPrimary;
                    strip.GripStyle = ToolStripGripStyle.Hidden;
                    ApplyToolStripItems(strip);
                    break;

                case TabControl tab:
                    StyleTabControl(tab);
                    break;

                case ListView list:
                    list.BackColor = Surface;
                    list.ForeColor = TextPrimary;
                    list.BorderStyle = BorderStyle.None;
                    list.GridLines = false;
                    WhenHandleCreated(list, handle => SetNativeTheme(handle, "DarkMode_Explorer"));
                    break;

                case DataGridView grid:
                    StyleGrid(grid);
                    break;

                case ScrollBar scrollBar:
                    WhenHandleCreated(scrollBar, handle => SetNativeTheme(handle, "DarkMode_Explorer"));
                    break;

                case TextBoxBase text:
                    text.BackColor = Input;
                    text.ForeColor = TextPrimary;
                    text.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case LinkLabel link:
                    link.BackColor = Color.Transparent;
                    link.ForeColor = TextPrimary;
                    link.LinkColor = Accent;
                    link.ActiveLinkColor = AccentHover;
                    link.VisitedLinkColor = Accent;
                    link.LinkBehavior = LinkBehavior.HoverUnderline;
                    break;

                case Button button:
                    StyleButton(button);
                    break;

                case CheckBox check:
                    check.BackColor = Color.Transparent;
                    check.ForeColor = TextPrimary;
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
                    box.BackColor = Input;
                    box.ForeColor = TextPrimary;
                    box.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case ToolStripProgressBar progress:
                    StyleProgressBar(progress.ProgressBar);
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
            button.BackColor = SurfaceAlt;
            button.ForeColor = TextPrimary;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = Hover;
            button.FlatAppearance.MouseDownBackColor = AccentPressed;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
        }

        public static void StylePrimaryButton(Button button)
        {
            StyleButton(button);
            button.BackColor = Accent;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = Accent;
            button.FlatAppearance.MouseOverBackColor = AccentHover;
            button.FlatAppearance.MouseDownBackColor = AccentPressed;
        }

        public static void StyleGrid(DataGridView grid)
        {
            grid.EnableHeadersVisualStyles = false;
            grid.BackgroundColor = Surface;
            grid.GridColor = Border;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ForeColor = TextPrimary;

            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceAlt;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SurfaceAlt;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextSecondary;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 0, 0, 0);

            grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.RowHeadersDefaultCellStyle.BackColor = Surface;
            grid.RowHeadersDefaultCellStyle.ForeColor = TextSecondary;
            grid.RowHeadersDefaultCellStyle.SelectionBackColor = Hover;

            grid.DefaultCellStyle.BackColor = Surface;
            grid.DefaultCellStyle.ForeColor = TextPrimary;
            grid.DefaultCellStyle.SelectionBackColor = Hover;
            grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
            grid.DefaultCellStyle.Padding = new Padding(4, 0, 0, 0);

            grid.AlternatingRowsDefaultCellStyle.BackColor = Background;
            grid.AlternatingRowsDefaultCellStyle.ForeColor = TextPrimary;
        }

        private static void StyleTabControl(TabControl tab)
        {
            foreach (TabPage page in tab.TabPages)
            {
                page.BackColor = Background;
                page.ForeColor = TextPrimary;
            }

            if (tab is DarkTabControl)
            {
                return;
            }

            tab.DrawMode = TabDrawMode.OwnerDrawFixed;
            tab.SizeMode = TabSizeMode.Fixed;
            tab.ItemSize = new Size(130, 30);
            tab.DrawItem -= OnDrawTabItem;
            tab.DrawItem += OnDrawTabItem;
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

            // Paint a little beyond the bounds to cover the native tab edges.
            var bounds = Rectangle.Inflate(e.Bounds, 2, 2);
            using (var background = new SolidBrush(selected ? Surface : Background))
            {
                e.Graphics.FillRectangle(background, bounds);
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

        // Makes one column take the remaining width, so no unpainted header area
        // is left after the last column. Call after items change and on resize.
        public static void FillColumn(ListView list, int index, int minimumWidth = 120)
        {
            if (index < 0 || index >= list.Columns.Count)
            {
                return;
            }

            int others = 0;
            for (int i = 0; i < list.Columns.Count; i++)
            {
                if (i != index)
                {
                    others += list.Columns[i].Width;
                }
            }

            int width = list.ClientSize.Width - others;
            if (width >= minimumWidth && list.Columns[index].Width != width)
            {
                list.Columns[index].Width = width;
            }
        }

        private static void OnDrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var background = new SolidBrush(SurfaceAlt))
            {
                e.Graphics.FillRectangle(background, e.Bounds);
            }

            using (var separator = new Pen(Border))
            {
                e.Graphics.DrawLine(separator, e.Bounds.Right - 1, e.Bounds.Top + 4, e.Bounds.Right - 1, e.Bounds.Bottom - 5);
            }

            var textBounds = e.Bounds;
            textBounds.X += 8;
            textBounds.Width -= 8;
            TextRenderer.DrawText(
                e.Graphics,
                e.Header.Text,
                ((ListView)sender).Font,
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

        // Grey hint text shown in an empty search box.
        public static void SetCueBanner(ToolStripTextBox box, string text)
        {
            SetCueBanner(box.TextBox, text);
        }

        public static void SetCueBanner(TextBox box, string text)
        {
            const int EM_SETCUEBANNER = 0x1501;
            WhenHandleCreated(box, handle => NativeMethods.Invoke(
                () => NativeMethods.SendMessage(handle, EM_SETCUEBANNER, (IntPtr)1, text)));
        }

        // Native progress bars ignore BackColor/ForeColor under visual styles, so
        // drop the visual style for this one control and set the colours directly.
        public static void StyleProgressBar(ProgressBar bar)
        {
            const int PBM_SETBARCOLOR = 0x0409;
            const int PBM_SETBKCOLOR = 0x2001;
            WhenHandleCreated(bar, handle => NativeMethods.Invoke(() =>
            {
                NativeMethods.SetWindowTheme(handle, "", "");
                NativeMethods.SendMessage(handle, PBM_SETBARCOLOR, IntPtr.Zero, (IntPtr)ColorTranslator.ToWin32(Accent));
                NativeMethods.SendMessage(handle, PBM_SETBKCOLOR, IntPtr.Zero, (IntPtr)ColorTranslator.ToWin32(SurfaceAlt));
            }));
        }

        // Windows 10 (1809+) / 11: dark window caption; Windows 11 also takes the
        // exact caption and border colours.
        private static void UseDarkTitleBar(IntPtr handle)
        {
            const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
            const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
            const int DWMWA_BORDER_COLOR = 34;
            const int DWMWA_CAPTION_COLOR = 35;

            NativeMethods.Invoke(() =>
            {
                int enabled = 1;
                if (NativeMethods.DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int)) != 0)
                {
                    NativeMethods.DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref enabled, sizeof(int));
                }

                int caption = ColorTranslator.ToWin32(Background);
                NativeMethods.DwmSetWindowAttribute(handle, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
                int border = ColorTranslator.ToWin32(Border);
                NativeMethods.DwmSetWindowAttribute(handle, DWMWA_BORDER_COLOR, ref border, sizeof(int));
            });
        }

        private static void SetNativeTheme(IntPtr handle, string theme)
        {
            NativeMethods.Invoke(() => NativeMethods.SetWindowTheme(handle, theme, null));
        }

        private static void WhenHandleCreated(Control control, Action<IntPtr> action)
        {
            if (control.IsHandleCreated == true)
            {
                action(control.Handle);
            }
            else
            {
                control.HandleCreated += (sender, e) => action(((Control)sender).Handle);
            }
        }

        private static class NativeMethods
        {
            [DllImport("dwmapi.dll")]
            public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

            [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
            public static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, string lParam);

            [DllImport("user32.dll")]
            public static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

            // Purely cosmetic calls: never let a missing API break the UI.
            public static void Invoke(Action action)
            {
                try
                {
                    action();
                }
                catch (Exception e) when (
                    e is DllNotFoundException ||
                    e is EntryPointNotFoundException ||
                    e is ExternalException)
                {
                }
            }
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
        public override Color MenuItemBorder => Theme.Hover;
        public override Color MenuItemSelected => Theme.Hover;
        public override Color MenuItemSelectedGradientBegin => Theme.Hover;
        public override Color MenuItemSelectedGradientEnd => Theme.Hover;
        public override Color MenuItemPressedGradientBegin => Theme.Surface;
        public override Color MenuItemPressedGradientEnd => Theme.Surface;

        public override Color ButtonSelectedGradientBegin => Theme.Hover;
        public override Color ButtonSelectedGradientMiddle => Theme.Hover;
        public override Color ButtonSelectedGradientEnd => Theme.Hover;
        public override Color ButtonSelectedBorder => Theme.Hover;
        public override Color ButtonPressedGradientBegin => Theme.AccentPressed;
        public override Color ButtonPressedGradientMiddle => Theme.AccentPressed;
        public override Color ButtonPressedGradientEnd => Theme.AccentPressed;
        public override Color ButtonPressedBorder => Theme.AccentPressed;
        public override Color ButtonCheckedGradientBegin => Theme.AccentPressed;
        public override Color ButtonCheckedGradientMiddle => Theme.AccentPressed;
        public override Color ButtonCheckedGradientEnd => Theme.AccentPressed;
        public override Color ButtonCheckedHighlight => Theme.AccentPressed;
        public override Color ButtonCheckedHighlightBorder => Theme.AccentPressed;
        public override Color CheckBackground => Theme.AccentPressed;
        public override Color CheckSelectedBackground => Theme.Accent;
        public override Color CheckPressedBackground => Theme.AccentPressed;

        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Background;
        public override Color GripDark => Theme.Border;
        public override Color GripLight => Theme.Background;
        public override Color StatusStripGradientBegin => Theme.Background;
        public override Color StatusStripGradientEnd => Theme.Background;
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
            var color = e.ToolStrip is ToolStripDropDown ? Theme.Surface : Theme.Background;
            using var background = new SolidBrush(color);
            e.Graphics.FillRectangle(background, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is ToolStripDropDown)
            {
                // Thin border around drop-down menus only.
                using var pen = new Pen(Theme.Border);
                var bounds = e.AffectedBounds;
                e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            }
            else if (e.ToolStrip is StatusStrip)
            {
                // A hairline separating the status bar from the content.
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawLine(pen, 0, 0, e.ToolStrip.Width, 0);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item is ToolStripStatusLabel)
            {
                e.TextColor = Theme.TextSecondary;
            }
            else
            {
                e.TextColor = e.Item.Enabled == true ? Theme.TextPrimary : Theme.TextSecondary;
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Theme.TextSecondary;
            base.OnRenderArrow(e);
        }
    }
}
