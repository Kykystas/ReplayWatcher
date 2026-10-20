using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ReplayWatcher
{
    internal sealed class LanguageSwitch : CheckBox
    {
        internal LanguageSwitch()
        {
            AutoSize = false; Size = new Size(120, 36); Cursor = Cursors.Hand;
            AccessibleName = L.T("Язык интерфейса");
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float unit = Height / 36f;
            RectangleF track = new RectangleF(34 * unit, 7 * unit, 48 * unit, 22 * unit);
            using (var path = new GraphicsPath())
            {
                path.AddArc(track.Left, track.Top, track.Height, track.Height, 90, 180);
                path.AddArc(track.Right - track.Height, track.Top, track.Height, track.Height, 270, 180); path.CloseFigure();
                using (var brush = new SolidBrush(Theme.Accent)) e.Graphics.FillPath(brush, path);
            }
            using (var thumb = new SolidBrush(Theme.Text))
                e.Graphics.FillEllipse(thumb, (Checked ? 63 : 37) * unit, 10 * unit, 16 * unit, 16 * unit);
            TextRenderer.DrawText(e.Graphics, "RU", Font, new Rectangle(0, 0, (int)(31 * unit), Height), Checked ? Theme.Muted : Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(e.Graphics, "EN", Font, new Rectangle((int)(85 * unit), 0, Width - (int)(85 * unit), Height), Checked ? Theme.Text : Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -1, -1), Theme.Muted, BackColor);
        }
    }
}
