// Copyright (c) 2022 OPTIKEY LTD (UK company number 11854839) - All Rights Reserved
using JuliusSweetland.OptiKey.Extensions;
using JuliusSweetland.OptiKey.Native.Common.Enums;
using JuliusSweetland.OptiKey.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace JuliusSweetland.OptiKey.UI.Windows
{
    public partial class OverlayWindow : Window
    {
        private Window window;
        private Screen screen;
        public OverlayWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            window = Window.GetWindow(this);
            screen = window.GetScreen();
            Left = screen.Bounds.Left;
            Top = screen.Bounds.Top;
            Width = screen.Bounds.Width;
            Height = screen.Bounds.Height;
            DataContext = viewModel;

            foreach (var control in viewModel.AxisControls.Values)
                OverlayCanvas.Children.Add(control);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var windowHandle = new WindowInteropHelper(this).Handle;
            Static.Windows.SetExtendedWindowStyle(windowHandle, 
                Static.Windows.GetExtendedWindowStyle(windowHandle) | ExtendedWindowStyles.WS_EX_TRANSPARENT | ExtendedWindowStyles.WS_EX_TOOLWINDOW);
        }
    }
}
