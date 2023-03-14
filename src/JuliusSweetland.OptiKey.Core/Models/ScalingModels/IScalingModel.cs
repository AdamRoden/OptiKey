using System.Collections.Generic;
using System.Windows;

namespace JuliusSweetland.OptiKey.Models.ScalingModels
{
    public interface IScalingModel
    {
        Vector CalculateScaling(Point point);
        bool Contains(Point point);
        List<Region> GetContours();
    }
}
