// Copyright(c) 2020 OPTIKEY LTD (UK company number11854839) - All Rights Reserved
using JuliusSweetland.OptiKey.Enums;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace JuliusSweetland.OptiKey.UI.Controls
{
    public class ResourceLookupConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string key = value as string;
            // Looks up the key in the application's merged dictionaries
            return Application.Current.TryFindResource(key) as Geometry;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
    public partial class InteractorEditor : UserControl
    {
        public InteractorEditor()
        {
            InitializeComponent();
        }

        #region Properties

        public static readonly DependencyProperty SelectedInteractorTypeProperty = DependencyProperty
            .Register("SelectedInteractorType", typeof(string), typeof(InteractorEditor), new PropertyMetadata(default(string)));

        public string SelectedInteractorType
        {
            get { return (string)GetValue(SelectedInteractorTypeProperty); }
            set { SetValue(SelectedInteractorTypeProperty, value); }
        }
        public static List<string> InteractorTypeList = Enum.GetNames(typeof(InteractorTypes)).ToList(); 

        public static List<string> SymbolList = new List<string>() { "" }.Concat(new ResourceDictionary() { Source = new Uri("/OptiKey;component/Resources/Icons/KeySymbols.xaml", UriKind.RelativeOrAbsolute) }.Keys.Cast<string>()).OrderBy(x => x).ToList();

        public static List<string> CompatibilityList = new List<string>() { "", "Any Font", "Persian", "Unicode", "Urdu" };

        #endregion

        #region Methods

        private void SelectType(object sender, SelectionChangedEventArgs e)
        {
            SelectedInteractorType = (sender as ComboBox).SelectedItem as string;
        }

        #endregion

    }
}

