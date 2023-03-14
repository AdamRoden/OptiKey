// Copyright (c) 2022 OPTIKEY LTD (UK company number 11854839) - All Rights Reserved
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using JuliusSweetland.OptiKey.Models.ScalingModels;
using JuliusSweetland.OptiKey.Native.Common.Enums;
using JuliusSweetland.OptiKey.Static;
using JuliusSweetland.OptiKey.UI.ViewModels;

namespace JuliusSweetland.OptiKey.UI.Windows
{
    public partial class AxisOverlayWindow : Window
    {
        private readonly IOverlayViewModel viewModel;

        public AxisOverlayWindow(IOverlayViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            DataContext = viewModel;

            this.UpdateContours();
        }

        // Based on: https://stackoverflow.com/a/3367137/9091159
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Apply the WS_EX_TRANSPARENT flag to the overlay window so events will pass through to any window underneath.
            var windowHandle = new WindowInteropHelper(this).Handle;
            Static.Windows.SetExtendedWindowStyle(windowHandle,
                Static.Windows.GetExtendedWindowStyle(windowHandle) | ExtendedWindowStyles.WS_EX_TRANSPARENT | ExtendedWindowStyles.WS_EX_TOOLWINDOW);
        }

        private void UpdateContours()
        {
            canvas.Children.Clear();
            var scrWidth = Graphics.PrimaryScreenWidthInPixels / Graphics.DipScalingFactorX;
            var scrHeight = Graphics.PrimaryScreenHeightInPixels / Graphics.DipScalingFactorY;
            foreach (var contour in viewModel.Contours)
            {
                var rect = new Rectangle
                {
                    Width = contour.Width * scrWidth,
                    Height = contour.Height * scrHeight,
                    RadiusX = contour.Curve * scrWidth,
                    RadiusY = contour.Curve * scrHeight,
                    Stroke = Brushes.CadetBlue,
                    Opacity = contour.Opacity,
                    StrokeThickness = 4,
                };

                Canvas.SetLeft(rect, contour.Left * scrWidth);
                Canvas.SetTop(rect, contour.Top * scrHeight);

                canvas.Children.Add(rect);
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals("Contours", e.PropertyName))
                UpdateContours();
        }
    }
}
