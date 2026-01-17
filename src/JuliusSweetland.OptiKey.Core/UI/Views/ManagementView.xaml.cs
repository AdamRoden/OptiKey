// Copyright (c) 2022 OPTIKEY LTD (UK company number 11854839) - All Rights Reserved
using MahApps.Metro.Controls;
using System.Windows.Controls;

namespace JuliusSweetland.OptiKey.UI.Views
{
    public partial class ManagementView : UserControl
    {
        public ManagementView()
        {
            InitializeComponent();
        }

        private void HamburgerMenu_OnItemInvoked(object sender, HamburgerMenuItemInvokedEventArgs e)
        {
            var menuItem = e.InvokedItem as HamburgerMenuItem;
            if (menuItem != null)
            {
                HamburgerMenu.Content = menuItem;
            }
        }
    }
}
