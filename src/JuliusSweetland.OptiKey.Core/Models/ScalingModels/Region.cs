using System;
using System.Windows;

namespace JuliusSweetland.OptiKey.Models.ScalingModels
{
    public struct Region
    {
        public Region(double left, double top, double width, double height, double curve = 0,
            double amountX = 0, double amountY = 0, double opacity = 0)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
            Curve = curve;
            AmountX = amountX;
            AmountY = amountY;
            Opacity = opacity;
        }
        public double Left;
        public double Top;
        public double Width;
        public double Height;
        public double Curve;
        public double AmountX;
        public double AmountY;
        public double Opacity;
        public Point Center { get { return new Point(Left + Width / 2, Top + Height / 2); } }
        public double Right { get { return Left + Width; } }
        public double Bottom { get { return Top + Height; } }
        public bool Contains(double x, double y)
        {
            if (Curve > 0) //use ellipse logic
            {
                x = .5 - x;
                y = .5 - y;
                return Math.Pow(x, 2) / Math.Pow(Width / 2, 2) + Math.Pow(y, 2) / Math.Pow(Height / 2, 2) <= 1;
            }
            else //use rectangle logic
                return x >= Left && x <= Right && y >= Top && y <= Bottom;
        }
    }
}
