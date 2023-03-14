// Copyright (c) 2022 OPTIKEY LTD (UK company number 11854839) - All Rights Reserved
using JuliusSweetland.OptiKey.Models.ScalingModels;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace JuliusSweetland.OptiKey.UI.ViewModels
{
    public interface IOverlayViewModel : INotifyPropertyChanged
    {
        bool IsActive { get; }
        List<Region> Contours { get; }
    }
}
