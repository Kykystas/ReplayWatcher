using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace ReplayWatcher
{
    internal static class Theme
    {
        internal static readonly Color Background = Color.FromArgb(14, 14, 18), Surface = Color.FromArgb(25, 23, 29), Text = Color.FromArgb(246, 239, 242), Muted = Color.FromArgb(179, 160, 169), Accent = Color.FromArgb(242, 62, 83), Border = Color.FromArgb(76, 41, 51);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        internal static void DarkTitle(Form form)
        {
            using (var stream = typeof(Theme).Assembly.GetManifestResourceStream("ReplayWatcher.AppIcon"))
                if (stream != null)
                {
                    var icon = new Icon(stream, new Size(32, 32));
                    form.Icon = icon; form.Disposed += delegate { icon.Dispose(); };
                }
            form.HandleCreated += delegate { try { int dark = 1; DwmSetWindowAttribute(form.Handle, 20, ref dark, 4); } catch { } };
        }
        internal static Button Button(string text, EventHandler action)
        {
            var button = new DarkButton { Text = text, AutoSize = true, Height = 36, Padding = new Padding(10, 3, 10, 3), FlatStyle = FlatStyle.Flat, BackColor = Surface, ForeColor = Text, Margin = new Padding(0, 0, 10, 0) };
            button.FlatAppearance.BorderColor = Border; button.FlatAppearance.MouseOverBackColor = Color.FromArgb(64, 27, 39); button.FlatAppearance.MouseDownBackColor = Color.FromArgb(98, 32, 48); button.Click += action; return button;
        }
    }
    internal sealed class DarkButton : Button
    {
        private bool hover;
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Enabled && hover ? Color.FromArgb(75, 27, 40) : BackColor);
            using (var pen = new Pen(Enabled && hover ? Theme.Accent : Theme.Border)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? ForeColor : Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), Theme.Muted, BackColor);
        }
    }
    internal sealed class DarkComboBox : ComboBox
    {
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr window, string app, string id);
        internal DarkComboBox()
        {
            BackColor = Theme.Surface; ForeColor = Theme.Text; FlatStyle = FlatStyle.Flat; DrawMode = DrawMode.OwnerDrawFixed; ItemHeight = 22;
            HandleCreated += delegate { try { SetWindowTheme(Handle, "", ""); } catch { } };
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            using (var brush = new SolidBrush((e.State & DrawItemState.Selected) != 0 && DroppedDown ? Color.FromArgb(90, 28, 43) : Theme.Surface)) e.Graphics.FillRectangle(brush, e.Bounds);
            string text = e.Index >= 0 ? GetItemText(Items[e.Index]) : Text;
            TextRenderer.DrawText(e.Graphics, text, Font, e.Bounds, Enabled ? Theme.Text : Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
    internal sealed class LevelMeter : Control
    {
        private int value;
        internal int Value { get { return value; } set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        internal int Maximum { get { return 100; } set { } }
        internal LevelMeter() { DoubleBuffered = true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Background);
            using (var brush = new SolidBrush(Theme.Accent)) e.Graphics.FillRectangle(brush, 0, 0, Width * value / 100, Height);
            using (var pen = new Pen(Theme.Border)) e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        }
    }
}
