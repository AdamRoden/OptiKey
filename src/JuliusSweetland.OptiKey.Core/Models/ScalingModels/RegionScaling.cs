using JuliusSweetland.OptiKey.Static;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace JuliusSweetland.OptiKey.Models.ScalingModels
{
    class RegionScaling : IScalingModel
    {
        private List<Region> regions;

        public RegionScaling(List<Region> regions)
        {
            this.regions = regions;
        }

        public List<Region> GetContours()
        {
            return regions.Where(r => r.Opacity > 0).ToList();
        }

        public Vector CalculateScaling(Point point)
        {
            point.X /= Graphics.PrimaryScreenWidthInPixels;
            point.Y /= Graphics.PrimaryScreenHeightInPixels;
           
            var amountX = 0d;
            var amountY = 0d;
            var center = regions[0];
            var innerLeft = center.Left;
            var innerRight = center.Right;
            var innerTop = center.Top;
            var innerBottom = center.Bottom;

            for (int i = 1; i < regions.Count(); i++)
            {
                var outer = regions[i];
                if (point.Y >= outer.Top && point.Y <= outer.Bottom)
                {
                    amountX = point.X <= outer.Left ? -outer.AmountX
                        : point.X < innerLeft ? amountX + (-outer.AmountX - amountX) * (point.X - innerLeft) / (outer.Left - innerLeft)
                        : amountX;
                    amountX = point.X >= outer.Right ? outer.AmountX
                        : point.X > innerRight ? amountX + (outer.AmountX - amountX) * (point.X - innerRight) / (outer.Right - innerRight)
                        : amountX;

                    innerLeft = point.X < outer.Left ? outer.Left : innerLeft;
                    innerRight = point.X > outer.Right ? outer.Right : innerRight;
                }
                if (point.X >= outer.Left && point.X <= outer.Right)
                {
                    amountY = point.Y <= outer.Top ? -outer.AmountY
                        : point.Y < innerTop ? amountY + (-outer.AmountY - amountY) * (point.Y - innerTop) / (outer.Top - innerTop)
                        : amountY;
                    amountY = point.Y >= outer.Bottom ? outer.AmountY
                        : point.Y > innerBottom ? amountY + (outer.AmountY - amountY) * (point.Y - innerBottom) / (outer.Bottom - innerBottom)
                        : amountY;

                    innerTop = point.Y < outer.Top ? outer.Top : innerTop;
                    innerBottom = point.Y > outer.Bottom ? outer.Bottom : innerBottom;
                }

                if (outer.Contains(point.X, point.Y))
                    break;
            }

            return new Vector(amountX, amountY);
        }

        public bool Contains(Point point)
        {
            point.X /= Graphics.PrimaryScreenWidthInPixels;
            point.Y /= Graphics.PrimaryScreenHeightInPixels;
            
            return regions.Exists(x => x.Contains(point.X, point.Y));
        }
    }
}
