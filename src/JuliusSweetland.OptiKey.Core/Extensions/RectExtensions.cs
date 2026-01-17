// Copyright (c) 2022 OPTIKEY LTD (UK company number 11854839) - All Rights Reserved
using System;
using System.Windows;

namespace JuliusSweetland.OptiKey.Extensions
{
    public static class RectExtensions
    {
        public static double CalculateArea(this Rect rect)
        {
            return rect.Width * rect.Height;
        }

        public static Point CalculateCentre(this Rect rect)
        {
            return new Point
            {
                X = (rect.Left + rect.Right) / 2,
                Y = (rect.Top + rect.Bottom) / 2,
            };
        }

        /// <summary>
        /// Calculates the distances from each edge of a Rect to each edge of another Rect that presumably lies
        /// within it. If that is not the case, the margins may be negative or exceed the size of the Rect itself.
        /// </summary>
        /// <param name="outer">the "outer" rectangle</param>
        /// <param name="inner">the "inner" rectangle</param>
        /// <returns>
        /// the left, right, top, and bottom distances between the two Rects
        /// </returns>
        public static Thickness CalculateMarginsAround(this Rect outer, Rect inner)
        {
            return new Thickness
            {
                Left = inner.Left - outer.Left,
                Right = outer.Right - inner.Right,
                Top = inner.Top - outer.Top,
                Bottom = outer.Bottom - inner.Bottom,
            };
        }

        public static bool IsCloseTo(this Rect rect1, Rect rect2, double epsilon = 1e-7)
        {
            bool xIsClose = Math.Abs(rect1.X - rect2.X) < epsilon;
            bool yIsClose = Math.Abs(rect1.Y - rect2.Y) < epsilon;
            bool widthIsClose = Math.Abs(rect1.Width - rect2.Width) < epsilon;
            bool heightIsClose = Math.Abs(rect1.Height - rect2.Height) < epsilon;

            return xIsClose && yIsClose && widthIsClose && heightIsClose;
        }
    }
}
