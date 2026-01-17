// Copyright (c) 2022 OPTIKEY LTD (UK company number 11854839) - All Rights Reserved
using JuliusSweetland.OptiKey.Enums;
using JuliusSweetland.OptiKey.Extensions;
using JuliusSweetland.OptiKey.Models;
using JuliusSweetland.OptiKey.Properties;
using JuliusSweetland.OptiKey.Services;
using JuliusSweetland.OptiKey.Static;
using JuliusSweetland.OptiKey.UI.Utilities;
using JuliusSweetland.OptiKey.UI.ViewModels.Keyboards.Base;
using JuliusSweetland.OptiKey.UI.Views.Keyboards.Common;
using JuliusSweetland.OptiKey.UI.Windows;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ViewModelKeyboards = JuliusSweetland.OptiKey.UI.ViewModels.Keyboards;

namespace JuliusSweetland.OptiKey.UI.Controls
{
    public class KeyboardHost : Canvas
    {
        #region Private member vars

        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private MainWindow mainWindow;
        private IList<Tuple<KeyValue, KeyValue>> keyFamily;
        private IDictionary<string, List<KeyValue>> keyValueByGroup;
        private IDictionary<KeyValue, TimeSpanOverrides> overrideTimesByKey;
        private IWindowManipulationService windowManipulationService;
        private CompositeDisposable currentKeyboardKeyValueSubscriptions = new CompositeDisposable();
        private ContentControl drawerView = new ContentControl();
        private Storyboard storyboard = new Storyboard();
        private bool isUpdating = false;

        #endregion

        #region Ctor

        public KeyboardHost()
        {
            KeyboardView = new ContentControl();
            Children.Add(KeyboardView);
            Settings.Default.OnPropertyChanges(s => s.KeyboardAndDictionaryLanguage).Subscribe(_ => GenerateContent());
            Settings.Default.OnPropertyChanges(s => s.UiLanguage).Subscribe(_ => GenerateContent());
            Settings.Default.OnPropertyChanges(s => s.MouseKeyboardDockSize).Subscribe(_ => GenerateContent());
            Settings.Default.OnPropertyChanges(s => s.ConversationOnlyMode).Subscribe(_ => GenerateContent());
            Settings.Default.OnPropertyChanges(s => s.ConversationConfirmEnable).Subscribe(_ => GenerateContent());
            Settings.Default.OnPropertyChanges(s => s.ConversationConfirmOnlyMode).Subscribe(_ => GenerateContent());
            Settings.Default.OnPropertyChanges(s => s.UseAlphabeticalKeyboardLayout).Subscribe(_ => GenerateContent());
            Settings.Default.OnPropertyChanges(s => s.EnableCommuniKateKeyboardLayout).Subscribe(_ => GenerateContent());
            Settings.Default.OnPropertyChanges(s => s.UseCommuniKateKeyboardLayoutByDefault).Subscribe(_ => GenerateContent());
            Settings.Default.OnPropertyChanges(s => s.UseSimplifiedKeyboardLayout).Subscribe(_ => GenerateContent());
            Settings.Default.OnPropertyChanges(s => s.CommuniKateKeyboardCurrentContext).Subscribe(_ => GenerateContent());
            Settings.Default.OnPropertyChanges(s => s.SimplifiedKeyboardContext).Subscribe(_ => GenerateContent());

            Loaded += OnLoaded;

            this.MouseEnter += this.OnMouseEnter;
        }

        #endregion

        #region Properties

        public static readonly DependencyProperty KeyboardViewProperty =
            DependencyProperty.Register("KeyboardView", typeof(ContentControl), typeof(KeyboardHost),
                new PropertyMetadata(default(ContentControl)));

        public ContentControl KeyboardView
        {
            get { return (ContentControl)GetValue(KeyboardViewProperty);}
            set { SetValue(KeyboardViewProperty, value); }
        }

        public double ViewTop
        {
            get { return GetTop(KeyboardView); }
            set { SetTop(KeyboardView, value);
                LayoutChanged();
            }
        }

        public double ViewLeft
        {
            get { return GetLeft(KeyboardView); }
            set { SetLeft(KeyboardView, value);
                LayoutChanged();
            }
        }

        public double ViewWidth
        {
            get { return KeyboardView.Width; }
            set { KeyboardView.Width = value;
                LayoutChanged();
            }
        }

        public double ViewHeight
        {
            get { return KeyboardView.Height; }
            set { KeyboardView.Height = value;
                LayoutChanged();
            }
        }

        public static readonly DependencyProperty KeyboardProperty =
            DependencyProperty.Register("Keyboard", typeof(IKeyboard), typeof(KeyboardHost),
                new PropertyMetadata(default(IKeyboard),
                    (o, args) =>
                    {
                        var keyboardHost = o as KeyboardHost;
                        if (keyboardHost != null)
                        {
                            if (keyboardHost.OpenDrawer != "None")
                                keyboardHost.OpenDrawer = "None";
                            keyboardHost.GenerateContent();
                        }
                    }));

        public IKeyboard Keyboard
        {
            get { return (IKeyboard)GetValue(KeyboardProperty); }
            set { SetValue(KeyboardProperty, value); }
        }

        public static readonly DependencyProperty OpenDrawerProperty =
            DependencyProperty.Register("OpenDrawer", typeof(string), typeof(KeyboardHost),
                new PropertyMetadata(default(string),
                    (o, args) =>
                    {
                        var keyboardHost = o as KeyboardHost;
                        if (keyboardHost != null)
                        {
                            if (keyboardHost.OpenDrawer == "Bottom")
                                keyboardHost.ShowDrawer();
                            if (keyboardHost.OpenDrawer == "None")
                                keyboardHost.HideDrawer();
                        }
                    }));

        public string OpenDrawer
        {
            get { return (string)GetValue(OpenDrawerProperty); }
            set { SetValue(OpenDrawerProperty, value); }
        }

        public static readonly DependencyProperty PointToKeyValueMapProperty =
                DependencyProperty.Register("PointToKeyValueMap", typeof(Dictionary<Rect, KeyValue>),
                    typeof(KeyboardHost), new PropertyMetadata(default(Dictionary<Rect, KeyValue>)));

        public Dictionary<Rect, KeyValue> PointToKeyValueMap
        {
            get { return (Dictionary<Rect, KeyValue>)GetValue(PointToKeyValueMapProperty); }
            set { SetValue(PointToKeyValueMapProperty, value); }
        }

        public static readonly DependencyProperty ErrorContentProperty =
            DependencyProperty.Register("ErrorContent", typeof(object), typeof(KeyboardHost), new PropertyMetadata(default(object)));

        public object ErrorContent
        {
            get { return GetValue(ErrorContentProperty); }
            set { SetValue(ErrorContentProperty, value); }
        }

        #endregion

        #region OnLoaded - build key map

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            Log.Debug("KeyboardHost loaded.");

            BuildPointToKeyMap(KeyboardView);

            SubscribeToSizeChanges();

            mainWindow = VisualAndLogicalTreeHelper.FindVisualParent<MainWindow>(this);

            var parentWindow = Window.GetWindow(this);

            if (parentWindow == null)
            {
                var windowException = new ApplicationException(Properties.Resources.PARENT_WINDOW_COULD_NOT_BE_FOUND);

                Log.Error(windowException);

                throw windowException;
            }

            SubscribeToParentWindowMoves(parentWindow);
            SubscribeToParentWindowStateChanges(parentWindow);

            Loaded -= OnLoaded; //Ensure this logic only runs once
        }

        #endregion

        private void HideDrawer()
        {
            drawerView.RenderTransform = new TranslateTransform() { Y = 0 };
            DoubleAnimation flyInAnimation = new DoubleAnimation
            {
                To = 150,
                BeginTime = TimeSpan.FromSeconds(0.3),
                Duration = TimeSpan.FromSeconds(0.5),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            storyboard = new Storyboard();
            storyboard.Children.Add(flyInAnimation);
            Storyboard.SetTargetProperty(flyInAnimation, new PropertyPath("RenderTransform.Y"));
            storyboard.Completed += (OnHideCompleted);
            storyboard.Begin(drawerView);
        }

        private void OnHideCompleted(object sender, EventArgs e)
        {
            BuildPointToKeyMap(KeyboardView);
            if (Children.Contains(drawerView))
                Children.Remove(drawerView);

            storyboard.Completed -= OnHideCompleted;
        }

        private void ShowDrawer()
        {
            if (!Children.Contains(drawerView))
                Children.Add(drawerView);
            if (OpenDrawer == "Bottom")
            {
                var drawer = new ViewModelKeyboards.DrawerBottom();
                drawerView.Width = 600;
                drawerView.Height = 150;
                drawerView.Content = drawer.GetContent();
                drawerView.RenderTransform = new TranslateTransform() { Y = 150 };
                SetLeft(drawerView, 500);
                SetTop(drawerView, 1050);

                DoubleAnimation flyInAnimation = new DoubleAnimation
                {
                    To = 0,
                    Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                    EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                };

                storyboard = new Storyboard();
                storyboard.Children.Add(flyInAnimation);
                Storyboard.SetTargetProperty(flyInAnimation, new PropertyPath("RenderTransform.Y"));
                storyboard.Completed += OnShowCompleted;
                storyboard.Begin(drawerView);
            }
        }

        private void OnShowCompleted(object sender, EventArgs e)
        {
            PointToKeyValueMap = null;
            BuildPointToKeyMap(drawerView);
            storyboard.Completed -= OnShowCompleted;
        }

        #region Generate Content

        private void GenerateContent()
        {
            Log.DebugFormat("GenerateContent called. Keyboard language is '{0}' and Keyboard type is '{1}'",
                Settings.Default.KeyboardAndDictionaryLanguage, Keyboard != null ? Keyboard.GetType() : null);

            //Clear out point to key map
            PointToKeyValueMap = null;

            mainWindow = mainWindow ?? VisualAndLogicalTreeHelper.FindVisualParent<MainWindow>(this);

            //Clear any potential main window color overrides
            if (mainWindow != null)
            {
                keyFamily = mainWindow.KeyFamily;
                keyValueByGroup = mainWindow.KeyValueByGroup;
                overrideTimesByKey = mainWindow.OverrideTimesByKey;
                windowManipulationService = mainWindow.WindowManipulationService;

                //Clear the dictionaries
                keyFamily?.Clear();
                keyValueByGroup?.Clear();
                overrideTimesByKey?.Clear();

                //https://github.com/OptiKey/OptiKey/pull/715
                //Fixing issue where navigating between dynamic and conversation keyboards causing sizing problems:
                //https://github.com/AdamRoden: "I think that because we use a dispatcher to apply the saved size and position,
                //we get in a situation where the main thread maximizes the window before it gets resized by the dispatcher thread.
                //My fix basically says, "don't try restoring the persisted state if we're navigating a maximized keyboard.""
                if (!(Keyboard is ViewModelKeyboards.DynamicKeyboard)
                        && !(Keyboard is ViewModelKeyboards.DrawerHide)
                        && !(Keyboard is ViewModelKeyboards.ConversationAlpha1)
                        && !(Keyboard is ViewModelKeyboards.ConversationAlpha2)
                        && !(Keyboard is ViewModelKeyboards.ConversationConfirm)
                        && !(Keyboard is ViewModelKeyboards.ConversationNumericAndSymbols))
                {
                    windowManipulationService.RestorePersistedState();
                }
            }

            object newContent = ErrorContent;

            if (Keyboard is ViewModelKeyboards.DynamicKeyboard)
            {
                var kb = Keyboard as ViewModelKeyboards.DynamicKeyboard;
                newContent = new DynamicKeyboard(kb.Link, keyFamily, keyValueByGroup, overrideTimesByKey, windowManipulationService) { DataContext = Keyboard };
            }
            else
            {
                newContent = Keyboard.GetContent();
            }

            KeyboardView.Content = newContent;

            LayoutChanged();
        }

        #endregion

        #region Content Change Handler

        private void LayoutChanged()
        {
            if (isUpdating)
                return;

            isUpdating = true;
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                isUpdating = false;
                BuildPointToKeyMap(KeyboardView);
            }));
        }

        private void OnMouseEnter(object sender, System.EventArgs e)
        {
            if (Settings.Default.PointsSource == PointsSources.MousePosition &&
                Settings.Default.PointsMousePositionHideCursor)
            {
                this.Cursor = System.Windows.Input.Cursors.None;
            }
            else
            {
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        #endregion

        #region Build Point To Key Map

        private void BuildPointToKeyMap(FrameworkElement view)
        {
            Log.Info("Building PointToKeyMap.");

            if (currentKeyboardKeyValueSubscriptions != null)
            {
                Log.Debug("Disposing of currentKeyboardKeyValueSubscriptions.");
                currentKeyboardKeyValueSubscriptions.Dispose();
            }
            currentKeyboardKeyValueSubscriptions = new CompositeDisposable();

            if (view != null)
            {
                if (view.IsLoaded)
                {
                    TraverseAllKeysAndBuildPointToKeyValueMap(view);
                }
                else
                {
                    RoutedEventHandler loaded = null;
                    loaded = (sender, args) =>
                    {
                        TraverseAllKeysAndBuildPointToKeyValueMap(view);
                        view.Loaded -= loaded;
                    };
                    view.Loaded += loaded;
                }
            }
        }

        private void TraverseAllKeysAndBuildPointToKeyValueMap(FrameworkElement view)
        {
            var allKeys = VisualAndLogicalTreeHelper.FindVisualChildren<Key>(view).ToList();
            var pointToKeyValueMap = new Dictionary<Rect, KeyValue>();
            var topLeftPoint = new Point(0, 0);

            foreach (var key in allKeys)
            {
                if (key.IsVisible
                    && PresentationSource.FromVisual(key) != null
                    && key.Value != null
                    && key.Value.HasContent())
                {
                    var rect = new Rect
                    {
                        Location = key.PointToScreen(topLeftPoint),
                        Size = (Size)key.GetTransformToDevice().Transform((Vector)key.RenderSize)
                    };

                    if (key is KeyPopup)
                    {
                        rect = Graphics.DipsToPixels(new Rect()
                        {
                            Location = new Point(key.GazeRegion.Left * SystemParameters.VirtualScreenWidth,
                                key.GazeRegion.Top * SystemParameters.VirtualScreenHeight),
                            Size = new Size(key.GazeRegion.Width * SystemParameters.VirtualScreenWidth,
                                key.GazeRegion.Height * SystemParameters.VirtualScreenHeight)
                        });
                    }

                    if (rect.Size.Width != 0 && rect.Size.Height != 0)
                    {
                        if (pointToKeyValueMap.ContainsKey(rect))
                        {
                            // In Release, just log error
                            KeyValue existingKeyValue = pointToKeyValueMap[rect];
                            Log.ErrorFormat("Overlapping keys {0} and {1}, cannot add {1} to map", existingKeyValue, key.Value);

                            Debug.Assert(!pointToKeyValueMap.ContainsKey(rect));
                        }
                        else
                        {
                            pointToKeyValueMap.Add(rect, key.Value);
                        }
                    }

                    var keyValueChangedSubscription = key.OnPropertyChanges<KeyValue>(Key.ValueProperty).Subscribe(kv =>
                    {
                        KeyValue mapValue;
                        if (pointToKeyValueMap.TryGetValue(rect, out mapValue))
                        {
                            pointToKeyValueMap[rect] = kv;
                        }
                    });
                    currentKeyboardKeyValueSubscriptions.Add(keyValueChangedSubscription);
                }
            }

            Log.InfoFormat("PointToKeyValueMap rebuilt with {0} keys.", pointToKeyValueMap.Keys.Count);

            //Add in menu drawer areas
            var bounds = mainWindow.GetScreen().Bounds;
            pointToKeyValueMap.Add(new Rect(.42 * bounds.Width, bounds.Height, .16 * bounds.Width, .16 * bounds.Height), KeyValues.DrawerBottomKey);
            pointToKeyValueMap.Add(new Rect(-.16 * bounds.Width, .42 * bounds.Height, .16 * bounds.Width, .16 * bounds.Height), KeyValues.DrawerLeftKey);
            pointToKeyValueMap.Add(new Rect(bounds.Width, .42 * bounds.Height, .16 * bounds.Width, .16 * bounds.Height), KeyValues.DrawerRightKey);
            pointToKeyValueMap.Add(new Rect(.42 * bounds.Width, -.16 * bounds.Height, .16 * bounds.Width, .16 * bounds.Height), KeyValues.DrawerTopKey);

            PointToKeyValueMap = pointToKeyValueMap;
        }

        #endregion

        #region Subscribe To Size Changes

        private void SubscribeToSizeChanges()
        {
            Observable.FromEventPattern<SizeChangedEventHandler, SizeChangedEventArgs>
                (h => SizeChanged += h,
                h => SizeChanged -= h)
                .Throttle(TimeSpan.FromSeconds(0.1))
                .ObserveOnDispatcher()
                .Subscribe(ep =>
                {
                    Log.Info($"KeyboardHost SizeChanged event detected from {ep.EventArgs.PreviousSize} to {ep.EventArgs.NewSize}.");
                    LayoutChanged();
                });
        }

        #endregion

        #region Subscribe To Parent Window Moves

        private void SubscribeToParentWindowMoves(Window parentWindow)
        {
            Observable.FromEventPattern<EventHandler, EventArgs>
                (h => parentWindow.LocationChanged += h,
                h => parentWindow.LocationChanged -= h)
                .Throttle(TimeSpan.FromSeconds(0.1))
                .ObserveOnDispatcher()
                .Subscribe(ep =>
                {
                    var window = ep.Sender as Window;
                    Log.Info($"Window's LocationChanged event detected. New window left:{window?.Left}, right:{(window?.Left ?? 0) + (window?.Width ?? 0)}, top:{window?.Top}, bottom:{(window?.Top ?? 0) + (window?.Height ?? 0)}.");
                    LayoutChanged();
                });
        }

        #endregion

        #region Subscribe To Parent Window State Changes

        private void SubscribeToParentWindowStateChanges(Window parentWindow)
        {
            Observable.FromEventPattern<EventHandler, EventArgs>
                (h => parentWindow.StateChanged += h,
                h => parentWindow.StateChanged -= h)
                .Throttle(TimeSpan.FromSeconds(0.1))
                .ObserveOnDispatcher()
                .Subscribe(_ =>
                {
                    Log.Info($"Window's StateChange event detected. New state: {parentWindow.WindowState}.");
                    LayoutChanged();
                });
        }

        #endregion
    }
}