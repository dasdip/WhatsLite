using System;
using System.Drawing;
using System.Windows.Forms;

namespace WhatsLite
{
    public class DarkColorTable : ProfessionalColorTable
    {
        private Color _bg = Color.FromArgb(32, 44, 51);         // #202c33
        private Color _menuBarBg = Color.FromArgb(24, 34, 41);   // #182229
        private Color _border = Color.FromArgb(42, 57, 66);     // #2a3942
        private Color _hover = Color.FromArgb(42, 57, 66);      // #2a3942
        private Color _green = Color.FromArgb(0, 168, 132);     // #00a884

        public override Color MenuStripGradientBegin { get { return _menuBarBg; } }
        public override Color MenuStripGradientEnd { get { return _menuBarBg; } }
        public override Color ToolStripDropDownBackground { get { return _bg; } }
        public override Color MenuBorder { get { return _border; } }
        public override Color MenuItemBorder { get { return _border; } }
        public override Color MenuItemSelected { get { return _hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return _hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return _hover; } }
        public override Color MenuItemPressedGradientBegin { get { return _bg; } }
        public override Color MenuItemPressedGradientEnd { get { return _bg; } }
        public override Color ImageMarginGradientBegin { get { return _bg; } }
        public override Color ImageMarginGradientMiddle { get { return _bg; } }
        public override Color ImageMarginGradientEnd { get { return _bg; } }
        public override Color SeparatorDark { get { return _border; } }
        public override Color SeparatorLight { get { return _border; } }
        public override Color CheckBackground { get { return Color.FromArgb(10, 60, 50); } }
        public override Color CheckSelectedBackground { get { return Color.FromArgb(10, 60, 50); } }
        public override Color ButtonSelectedHighlight { get { return _hover; } }
        public override Color ButtonSelectedBorder { get { return _green; } }
    }

    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable())
        {
            this.RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item.Tag is Color)
            {
                e.TextColor = (Color)e.Item.Tag;
            }
            else
            {
                e.TextColor = e.Item.Selected ? Color.White : Color.FromArgb(233, 237, 239);
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Color.FromArgb(0, 168, 132);
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle r = e.ImageRectangle;
            r.Inflate(2, 2);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(10, 60, 50)))
            {
                e.Graphics.FillRectangle(bg, r);
            }
            using (Font checkFont = new Font("Segoe UI", 9.0f, FontStyle.Bold))
            using (SolidBrush checkBrush = new SolidBrush(Color.FromArgb(0, 168, 132)))
            {
                e.Graphics.DrawString("✔", checkFont, checkBrush, r.X + 2, r.Y);
            }
        }
    }

    public class LightColorTable : ProfessionalColorTable
    {
        private Color _bg = Color.FromArgb(255, 255, 255);         // #ffffff
        private Color _menuBarBg = Color.FromArgb(240, 242, 245);   // #f0f2f5 (WhatsApp light gray)
        private Color _border = Color.FromArgb(218, 224, 228);     // #dae0e4
        private Color _hover = Color.FromArgb(234, 237, 240);      // #eaedf0
        private Color _green = Color.FromArgb(0, 168, 132);        // #00a884

        public override Color MenuStripGradientBegin { get { return _menuBarBg; } }
        public override Color MenuStripGradientEnd { get { return _menuBarBg; } }
        public override Color ToolStripDropDownBackground { get { return _bg; } }
        public override Color MenuBorder { get { return _border; } }
        public override Color MenuItemBorder { get { return _border; } }
        public override Color MenuItemSelected { get { return _hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return _hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return _hover; } }
        public override Color MenuItemPressedGradientBegin { get { return _hover; } }
        public override Color MenuItemPressedGradientEnd { get { return _hover; } }
        public override Color ImageMarginGradientBegin { get { return _bg; } }
        public override Color ImageMarginGradientMiddle { get { return _bg; } }
        public override Color ImageMarginGradientEnd { get { return _bg; } }
        public override Color SeparatorDark { get { return _border; } }
        public override Color SeparatorLight { get { return _border; } }
        public override Color CheckBackground { get { return Color.FromArgb(220, 248, 238); } }
        public override Color CheckSelectedBackground { get { return Color.FromArgb(200, 242, 230); } }
        public override Color ButtonSelectedHighlight { get { return _hover; } }
        public override Color ButtonSelectedBorder { get { return _green; } }
    }

    public class LightMenuRenderer : ToolStripProfessionalRenderer
    {
        public LightMenuRenderer() : base(new LightColorTable())
        {
            this.RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item.Tag is Color)
            {
                e.TextColor = (Color)e.Item.Tag;
            }
            else
            {
                e.TextColor = Color.FromArgb(17, 27, 33); // #111b21 dark text
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Color.FromArgb(0, 168, 132);
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle r = e.ImageRectangle;
            r.Inflate(2, 2);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(220, 248, 238)))
            {
                e.Graphics.FillRectangle(bg, r);
            }
            using (Font checkFont = new Font("Segoe UI", 9.0f, FontStyle.Bold))
            using (SolidBrush checkBrush = new SolidBrush(Color.FromArgb(0, 168, 132)))
            {
                e.Graphics.DrawString("✔", checkFont, checkBrush, r.X + 2, r.Y);
            }
        }
    }
}
