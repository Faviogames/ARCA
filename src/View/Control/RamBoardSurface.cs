using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WinMemoryCleaner
{
    /// <summary>Resolution-independent DIMM board decoration; no bitmaps or per-frame animation.</summary>
    public sealed class RamBoardSurface : FrameworkElement
    {
        /// <summary>Whether this board has an exposed row of gold contacts.</summary>
        public static readonly DependencyProperty ShowContactsProperty = DependencyProperty.Register(
            "ShowContacts", typeof(bool), typeof(RamBoardSurface), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Only the outer board and the desktop miniature expose contacts.</summary>
        public bool ShowContacts
        {
            get { return (bool)GetValue(ShowContactsProperty); }
            set { SetValue(ShowContactsProperty, value); }
        }

        /// <summary>Renders a PCB silhouette, circuit traces and gold contacts.</summary>
        protected override void OnRender(DrawingContext dc)
        {
            if (dc == null) throw new ArgumentNullException("dc");
            base.OnRender(dc);
            var w = ActualWidth; var h = ActualHeight;
            if (w < 30 || h < 25) return;
            var s = Math.Min(1, h / 300);
            var notch = Math.Min(18 * s, w / 15);
            var middle = w * .51;
            var path = string.Format(CultureInfo.InvariantCulture,
                "M 20,2 L {0},2 Q {1},2 {1},20 L {1},{2} L {3},{2} Q {4},{2} {4},{5} Q {4},{6} {3},{6} L {1},{6} L {1},{7} Q {1},{8} {0},{8} L {9},{8} L {9},{10} Q {9},{11} {12},{11} Q {13},{11} {13},{10} L {13},{8} L 20,{8} Q 2,{8} 2,{7} L 2,{6} L {14},{6} Q {15},{6} {15},{5} Q {15},{2} {14},{2} L 2,{2} L 2,20 Q 2,2 20,2 Z",
                w - 20, w - 2, h * .46, w - 7, w - notch, h * .51, h * .56, h - 20, h - 2,
                middle + 10 * s, h - 21 * s, h - 26 * s, middle, middle - 10 * s, 7, notch);
            var geometry = Geometry.Parse(path);
            var green = new LinearGradientBrush(Color.FromRgb(9, 83, 52), Color.FromRgb(5, 51, 34), 90);
            dc.DrawGeometry(green, new Pen(Brush("#398D59"), 1.2), geometry);
            dc.PushClip(geometry);
            var trace = new Pen(Brush("#338348"), .8 * s);
            var traceDim = new Pen(Brush("#1A6941"), .6 * s);
            for (var side = 0; side < 2; side++)
            {
                dc.PushTransform(new TranslateTransform(side == 0 ? 0 : w, 0));
                dc.PushTransform(new ScaleTransform(side == 0 ? 1 : -1, 1));
                for (var i = 0; i < 7; i++)
                {
                    var x = (13 + i * 6) * s;
                    var y = (37 + i * 9) * s;
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(x, y), false, false);
                        c.LineTo(new Point(x, h * .31 + i * 3 * s), true, false);
                        c.LineTo(new Point(x + 25 * s, h * .31 + (25 + i * 3) * s), true, false);
                        c.LineTo(new Point(x + 25 * s, h * .69 - i * 2 * s), true, false);
                        c.LineTo(new Point(x, h * .69 + (25 - i * 2) * s), true, false);
                        c.LineTo(new Point(x, h - (45 + i * 3) * s), true, false);
                    }
                    dc.DrawGeometry(null, i % 2 == 0 ? trace : traceDim, g);
                    dc.DrawEllipse(Brush("#0B492E"), new Pen(Brush("#B19F51"), .7 * s), new Point(x, y), 1.8 * s, 1.8 * s);
                    dc.DrawEllipse(null, new Pen(Brush("#8F9951"), .6 * s), new Point(x, h - (45 + i * 3) * s), 1.7 * s, 1.7 * s);
                }
                for (var i = 0; i < 13; i++)
                {
                    var y = (102 * s) + (h - 200 * s) * i / 13;
                    var x = (i % 2 == 0 ? 20 : 31) * s;
                    dc.DrawRoundedRectangle(Brush("#B8B9A2"), null, new Rect(x, y, 3.5 * s, 8 * s), .7, .7);
                    dc.DrawRectangle(Brush("#827744"), null, new Rect(x, y + 2 * s, 3.5 * s, 4 * s));
                }
                dc.Pop(); dc.Pop();
            }
            var gold = new LinearGradientBrush(Color.FromRgb(245, 216, 134), Color.FromRgb(174, 132, 48), 0);
            var pitch = Math.Max(5, 8 * s);
            if (ShowContacts)
                for (var x = 43 * s; x < w - 43 * s; x += pitch)
                    dc.DrawRoundedRectangle(gold, new Pen(Brush("#EAD895"), .5), new Rect(x, h - 26 * s, pitch - 2 * s, 25 * s), 1 * s, 1 * s);
            foreach (var point in new[] { new Point(21 * s, 21 * s), new Point(w - 21 * s, 21 * s), new Point(21 * s, h - 29 * s), new Point(w - 21 * s, h - 29 * s) })
            {
                dc.DrawEllipse(Brush("#101E19"), new Pen(gold, 2.3 * s), point, 7 * s, 7 * s);
            }
            dc.Pop();
        }

        private static SolidColorBrush Brush(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze(); return brush;
        }
    }
}
