// Copyright (c) 2022 OPTIKEY LTD (UK company number 11854839) - All Rights Reserved
using JuliusSweetland.OptiKey.Enums;
using JuliusSweetland.OptiKey.Extensions;
using JuliusSweetland.OptiKey.Models;
using JuliusSweetland.OptiKey.Properties;
using JuliusSweetland.OptiKey.UI.Controls;
using JuliusSweetland.OptiKey.UI.Utilities;
using JuliusSweetland.OptiKey.UI.ViewModels.Keyboards;
using log4net;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using System.Xml.Serialization;
using Key = JuliusSweetland.OptiKey.UI.Controls.Key;

namespace JuliusSweetland.OptiKey.UI.ViewModels.Management
{
    public class LayoutViewModel : INotifyPropertyChanged
    {
        protected static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public LayoutViewModel()
        {
            OpenFileCommand = new DelegateCommand(OpenFile);
            SaveFileCommand = new DelegateCommand(SaveFile);
            AddLayoutCommand = new DelegateCommand(AddLayout);
            AddBuiltInCommand = new DelegateCommand(AddBuiltIn);
            AddProfileCommand = new DelegateCommand(AddProfile);
            DeleteProfileCommand = new DelegateCommand(DeleteProfile);
            AddInteractorCommand = new DelegateCommand(AddInteractor);
            CloneInteractorCommand = new DelegateCommand(CloneInteractor);
            DeleteInteractorCommand = new DelegateCommand(DeleteInteractor);
            InteractorCommand = new DelegateCommand<object>(SelectInteractor);

            XmlKeyboards = new ObservableCollection<XmlKeyboard>();
            Profiles = new ObservableCollection<InteractorProfile>();
            Interactors = new ObservableCollection<Interactor>();
            descendantPropertyChanged = new PropertyChangedEventHandler(DescendantPropertyChanged);

            XmlKeyboards.CollectionChanged += delegate (object sender, NotifyCollectionChangedEventArgs e)
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (INotifyPropertyChanged propertyChanged in e.NewItems)
                        propertyChanged.PropertyChanged += descendantPropertyChanged;
                }
                else if (e.Action == NotifyCollectionChangedAction.Remove)
                {
                    foreach (INotifyPropertyChanged propertyChanged in e.OldItems)
                        propertyChanged.PropertyChanged -= descendantPropertyChanged;
                }
            };
            Profiles.CollectionChanged += delegate (object sender, NotifyCollectionChangedEventArgs e)
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (INotifyPropertyChanged propertyChanged in e.NewItems)
                        propertyChanged.PropertyChanged += descendantPropertyChanged;
                }
                else if (e.Action == NotifyCollectionChangedAction.Remove)
                {
                    foreach (INotifyPropertyChanged propertyChanged in e.OldItems)
                        propertyChanged.PropertyChanged -= descendantPropertyChanged;
                }
            };
            Interactors.CollectionChanged += delegate (object sender, NotifyCollectionChangedEventArgs e)
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (INotifyPropertyChanged propertyChanged in e.NewItems)
                        propertyChanged.PropertyChanged += descendantPropertyChanged;
                }
                else if (e.Action == NotifyCollectionChangedAction.Remove)
                {
                    foreach (INotifyPropertyChanged propertyChanged in e.OldItems)
                        propertyChanged.PropertyChanged -= descendantPropertyChanged;
                }
            };
            Load();
            AddLayout();
        }

        #region Properties

        public static List<string> KeyboardList = new List<string>()
        {
            "Alpha1", "ConversationAlpha1", "ConversationNumericAndSymbols", "Currencies1",
            "Diacritics1", "Language", "Menu", "Mouse", "NumericAndSymbols1",  "NumericAndSymbols2", "PhysicalKeys",
            "SimplifiedAlpha", "SimplifiedConversationAlpha", "SizeAndPosition", "WebBrowsing"
        };

        public static List<string> WindowStates = new List<string>() { { Enums.WindowStates.Docked.ToString() }, { Enums.WindowStates.Floating.ToString() }, { Enums.WindowStates.Maximised.ToString() } };

        public static List<string> PositionList = Enum.GetNames(typeof(MoveToDirections)).ToList();
        
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        private PropertyChangedEventHandler descendantPropertyChanged;
        protected void DescendantPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            CreateViewbox();
        }

        public DelegateCommand OpenFileCommand { get; private set; }
        public DelegateCommand SaveFileCommand { get; private set; }
        public DelegateCommand AddLayoutCommand { get; private set; }
        public DelegateCommand AddBuiltInCommand { get; private set; }
        public DelegateCommand AddProfileCommand { get; private set; }
        public DelegateCommand DeleteProfileCommand { get; private set; }
        public DelegateCommand AddInteractorCommand { get; private set; }
        public DelegateCommand CloneInteractorCommand { get; private set; }
        public DelegateCommand DeleteInteractorCommand { get; private set; }
        public ICommand InteractorCommand { get; private set; }

        public string KeyboardFile { get; set; }
        
        public ObservableCollection<XmlKeyboard> XmlKeyboards { get; private set; }
        
        private XmlKeyboard xmlKeyboard = new XmlKeyboard();
        public XmlKeyboard XmlKeyboard
        {
            get { return xmlKeyboard; }
            set
            {
                xmlKeyboard = value;
                XmlKeyboards.Clear();
                XmlKeyboards.Add(value);
                Profiles.Clear();
                Profiles.AddRange(XmlKeyboard.Profiles);
                Profile = Profiles[0];
                Interactors.Clear();
                Interactors.AddRange(XmlKeyboard.Interactors);
                CreateViewbox();
                OnPropertyChanged();
            }
        }

        private ObservableCollection<InteractorProfile> profiles;
        public ObservableCollection<InteractorProfile> Profiles
        { get { return profiles; } set { profiles = value; OnPropertyChanged(); } }
        private bool isHelpOpen;
        public bool IsHelpOpen
        { get { return isHelpOpen; } set { isHelpOpen = value; OnPropertyChanged(); } }


        private InteractorProfile profile = new InteractorProfile();
        public InteractorProfile Profile
        { get { return profile; } set { profile = value; OnPropertyChanged(); } }

        private ObservableCollection<Interactor> interactors;
        public ObservableCollection<Interactor> Interactors
        { get { return interactors; } set { interactors = value; OnPropertyChanged(); } }

        private Interactor interactor;
        public Interactor Interactor
        {
            get { return interactor; }
            set
            {
                if (interactor != null && interactor.Key != null)
                {
                    interactor.Key.IsCurrent = false;
                    if (interactor is DynamicPopup)
                    {
                        canvas.Children.Remove(gazeRegion);
                        interactor.Key.Margin = new Thickness(ScreenOffset + interactor.GazeLeft * ScreenWidth, ScreenOffset + interactor.GazeTop * ScreenHeight, 0, 0);
                    }
                }
                interactor = value;
                if (interactor != null)
                {
                    if (interactor.Key != null)
                    {
                        interactor.Key.IsCurrent = true;
                        if (interactor is DynamicPopup)
                        {
                            gazeRegion.Margin = interactor.Key.Margin;
                            gazeRegion.Width = interactor.Key.Width;
                            gazeRegion.Height = interactor.Key.Height;
                            canvas.Children.Add(gazeRegion);
                            interactor.Key.Margin = new Thickness((ScreenOffset + interactor.GazeLeft * ScreenWidth).Clamp(ScreenOffset, ScreenOffset + ScreenWidth - interactor.GazeWidth * ScreenWidth), (ScreenOffset + interactor.GazeTop * ScreenHeight).Clamp(ScreenOffset, ScreenOffset + ScreenHeight - interactor.GazeHeight * ScreenHeight), 0, 0);
                        }
                    }
                    SelectedInteractorType = interactor.TypeAsString;
                }
                OnPropertyChanged();
            }
        }

        private InteractorTypes newType;
        private InteractorTypes selectedInteractorType;
        public string SelectedInteractorType
        {
            get { return selectedInteractorType.ToString(); }
            set
            {
                selectedInteractorType = Enum.TryParse(value, out InteractorTypes type) ? type : InteractorTypes.Key;
                if (Interactor != null && Interactor.TypeAsString != value)
                    ReplaceInteractor();
            }
        }

        private string keyboardName;
        public string KeyboardName { get { return keyboardName; } set { keyboardName = value; OnPropertyChanged(); } }

        private Viewbox viewbox;
        public Viewbox Viewbox
        {
            get { return viewbox; }
            set { viewbox = value; OnPropertyChanged(); }
        }

        #endregion

        #region Methods

        public void ApplyChanges()
        {
            Settings.Default.KeyboardFile = KeyboardFile;
        }

        private void Load()
        {
            KeyboardFile = Settings.Default.KeyboardFile;
        }

        private void OpenFile()
        {
            var fileDialog = new System.Windows.Forms.OpenFileDialog() { FileName = Path.GetFileName(KeyboardFile) };
            var result = fileDialog.ShowDialog();
            string tempFilename;
            switch (result)
            {
                case System.Windows.Forms.DialogResult.OK:
                    tempFilename = fileDialog.FileName; // we will commit it if loading is okay
                    break;
                case System.Windows.Forms.DialogResult.Cancel:
                default:
                    return;
            }
            try
            {
                XmlKeyboard = XmlKeyboard.ReadFromFile(tempFilename);
            }
            catch (Exception e)
            {
                Log.Error($"Error reading from Layout file: {tempFilename} :");
                Log.Info(e.ToString());
                return;
            }
            KeyboardFile = tempFilename;
        }

        private void SaveFile()
        {
            var fp = Settings.Default.DynamicKeyboardsLocation;
            var fn = XmlKeyboard.Name + ".xml";
            try
            {
                fp = Path.GetDirectoryName(KeyboardFile);
                fn = Path.GetFileName(KeyboardFile);
            }
            catch { }
            var saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            saveFileDialog.InitialDirectory = fp;
            saveFileDialog.FileName = fn;
            saveFileDialog.Title = "Save File";
            saveFileDialog.CheckFileExists = false;
            saveFileDialog.CheckPathExists = true;
            saveFileDialog.DefaultExt = "xml";
            saveFileDialog.Filter = "Xml files (*.xml)|*.xml|All files (*.*)|*.*";
            saveFileDialog.FilterIndex = 2;
            saveFileDialog.RestoreDirectory = true;
            var result = saveFileDialog.ShowDialog();
            switch (result)
            {
                case System.Windows.Forms.DialogResult.OK:
                    KeyboardFile = saveFileDialog.FileName;
                    XmlKeyboard.WriteToFile(KeyboardFile);
                    break;
                case System.Windows.Forms.DialogResult.Cancel:
                default:
                    break;
            }
        }

        private void AddBuiltIn()
        {
            var newKeyboard = new XmlKeyboard() { Name = KeyboardName };
            var profile = new InteractorProfile() { Name = "All" };
            newKeyboard.Profiles = new List<InteractorProfile> { profile };

            DependencyObject content = (DependencyObject)new Alpha1().GetContent();

            switch (KeyboardName)
            {
                case "ConversationAlpha1":
                    content = (DependencyObject)new ConversationAlpha1(null).GetContent();
                    break;
                case "ConversationNumericAndSymbols":
                    content = (DependencyObject)new ConversationNumericAndSymbols(null).GetContent();
                    break;
                case "Currencies1":
                    content = (DependencyObject)new Currencies1().GetContent();
                    break;
                case "Diacritics1":
                    content = (DependencyObject)new Diacritics1().GetContent();
                    break;
                case "Language":
                    content = (DependencyObject)new Language(null).GetContent();
                    break;
                case "Menu":
                    content = (DependencyObject)new Keyboards.Menu(null).GetContent();
                    break;
                case "Mouse":
                    content = (DependencyObject)new Keyboards.Mouse(null).GetContent();
                    break;
                case "NumericAndSymbols1":
                    content = (DependencyObject)new NumericAndSymbols1().GetContent();
                    break;
                case "NumericAndSymbols2":
                    content = (DependencyObject)new NumericAndSymbols2().GetContent();
                    break;
                case "PhysicalKeys":
                    content = (DependencyObject)new PhysicalKeys().GetContent();
                    break;
                case "SimplifiedAlpha":
                    content = (DependencyObject)new SimplifiedAlpha(null).GetContent();
                    break;
                case "SimplifiedConversationAlpha":
                    content = (DependencyObject)new SimplifiedConversationAlpha(null).GetContent();
                    break;
                case "SizeAndPosition":
                    content = (DependencyObject)new SizeAndPosition(null).GetContent();
                    break;
                case "WebBrowsing":
                    content = (DependencyObject)new WebBrowsing().GetContent();
                    break;
            }
            var symbols = new ResourceDictionary() { Source = new Uri("/OptiKey;component/Resources/Icons/KeySymbols.xaml", UriKind.RelativeOrAbsolute) };

            var symbolValues = symbols.Values.OfType<Geometry>().Select(x => x.ToString()).ToList();
            var symbolKeys = symbols.Keys.OfType<string>().ToList();
            var allKeys = VisualAndLogicalTreeHelper.FindLogicalChildren<Key>(content).ToList();
            var outputList = VisualAndLogicalTreeHelper.FindLogicalChildren<Output>(content).ToList();
            var outputRows = outputList.Any() ? 2 : 0;
            var maxRow = 0d;
            var maxCol = 0d;
            foreach (Key key in allKeys.Where(x => x is Key && VisualAndLogicalTreeHelper.FindLogicalParent<Output>(x) == null))
            {
                var i = new DynamicKey() { Label = key.ShiftUpText, ShiftDownLabel = key.ShiftDownText, ColN = Grid.GetColumn(key), RowN = Grid.GetRow(key) - outputRows >= 0 ? Grid.GetRow(key) - outputRows : Grid.GetRow(key), WidthN = Grid.GetColumnSpan(key), HeightN = Grid.GetRowSpan(key) };
                if (key.Value != null)
                {
                    var kv = key.Value;
                    if (kv.FunctionKey.HasValue)
                        i.Commands.Add(new ActionCommand() { Value = kv.FunctionKey.Value.ToString() });
                    else if (kv.String != null && string.IsNullOrWhiteSpace(kv.String))
                    {
                        if (kv.String == ((char)32).ToString())
                            i.Commands.Add(new TextCommand() { Value = "&#32;" });
                        else if (kv.String == ((char)9).ToString())
                            i.Commands.Add(new TextCommand() { Value = "&#9;" });
                        else
                            i.Commands.Add(new TextCommand() { Value = "&#10;" });
                    }
                    else
                        i.Commands.Add(new TextCommand() { Value = kv.String });
                }
                if (key.SymbolGeometry != null)
                    i.Symbol = symbolKeys[symbolValues.IndexOf(key.SymbolGeometry.ToString())];
                i.SharedSizeGroup = key.SharedSizeGroup;
                i.Profiles.Add(new InteractorProfileMap(profile, true));
                newKeyboard.Interactors.Add(i);
                maxCol = Math.Max(maxCol, i.ColN + i.WidthN);
                maxRow = Math.Max(maxRow, i.RowN + i.HeightN);
            }
            newKeyboard.ShowOutputPanel = outputRows > 0 ? "True" : null;
            newKeyboard.Rows = (int)maxRow;
            newKeyboard.Cols = (int)maxCol;
            XmlKeyboard = newKeyboard;
        }


        private bool[,] _cellOccupancy;
        private int _rowCount;
        private int _columnCount;

        private void AddLayout()
        {
            XmlKeyboard = new XmlKeyboard() {
                Name = "NewKeyboard",
                Grid = new XmlGrid() { Cols = 16, Rows = 14 },
                Profiles = new List<InteractorProfile> { new InteractorProfile() { Name = "All" } } };

            InitializeOccupancyArray();
        }

        private void InitializeOccupancyArray()
        {
            _rowCount = XmlKeyboard.Rows;
            _columnCount = XmlKeyboard.Cols;
            _cellOccupancy = new bool[_rowCount, _columnCount];

            // Mark existing controls' spaces as occupied
            foreach (var interactor in XmlKeyboard.Interactors)
            {
                MarkOccupied(interactor.RowN, interactor.ColN, interactor.HeightN, interactor.WidthN, true);
            }
        }

        private void MarkOccupied(int startRow, int startCol, int rowSpan, int colSpan, bool isOccupied)
        {
            for (int r = startRow; r < startRow + rowSpan; r++)
            {
                for (int c = startCol; c < startCol + colSpan; c++)
                {
                    if (r >= 0 && r < _rowCount && c >= 0 && c < _columnCount)
                    {
                        _cellOccupancy[r, c] = isOccupied;
                    }
                }
            }
        }

        private Tuple<int, int> FindEmptySpace(int neededRowSpan, int neededColumnSpan, int? row = null, int? col = null)
        {
            var startRow = row.HasValue ? row.Value : 0; 
            var endRow = row.HasValue ? row.Value + neededRowSpan - 1 : _rowCount - neededRowSpan;
            var startCol = col.HasValue ? col.Value : 0;
            var endCol = col.HasValue ? col.Value + neededColumnSpan - 1 : _columnCount - neededColumnSpan;
            
            for (int r = startRow; r <= endRow; r++)
            {
                for (int c = startCol; c <= endCol; c++)
                {
                    if (CheckAvailability(r, c, neededRowSpan, neededColumnSpan))
                    {
                        return new Tuple<int, int>(r, c);
                    }
                }
            }
            return null; // No suitable space found
        }

        private bool CheckAvailability(int startRow, int startCol, int rowSpan, int colSpan)
        {
            for (int r = startRow; r < startRow + rowSpan; r++)
            {
                for (int c = startCol; c < startCol + colSpan; c++)
                {
                    if (r >= _rowCount || c >= _columnCount || _cellOccupancy[r, c])
                    {
                        return false; // Cell is outside bounds or already occupied
                    }
                }
            }
            return true; // All cells in the range are available
        }

        private void AddProfile()
        {
            if (Profiles == null) { return; }

            var name = "Profile";
            for (int i = 1; i <= Profiles.Count() + 1; i++)
            {
                name = "Profile" + i.ToString();
                if (Profiles.Where(x => x.Name == name).Count() == 0)
                    break;
            }
            Profile = new InteractorProfile() { Name = name };
            Profiles.Add(Profile);
            XmlKeyboard.Profiles = Profiles.ToList();
            foreach (var i in Interactors)
            {
                i.Profiles.Add(new InteractorProfileMap(profile, false));
            }
        }

        private void DeleteProfile()
        {
            if (Profiles == null) { return; }
            
            var index = Profiles.IndexOf(Profile);
            if (index > 0)
            {
                foreach (var i in Interactors)
                {
                    var map = i.Profiles.Where(x => x.Profile == Profile).FirstOrDefault();
                    if (map != null)
                        i.Profiles.Remove(map);
                }
                Profiles.RemoveAt(index);
                XmlKeyboard.Profiles = Profiles.ToList();
                Profile = Profiles[index - 1];
            }
            CreateViewbox();
        }

        private void AddInteractor()
        {
            if (Interactors == null) { return; }

            InitializeOccupancyArray();
            var minKeyWidth = 1;
            var minKeyHeight = 1;
            if (Interactors.Where(x => x is DynamicKey).Any())
            {
                minKeyWidth = Interactors.Where(x => x is DynamicKey).Select(k => k.WidthN).Min();
                minKeyHeight = Interactors.Where(x => x is DynamicKey).Select(k => k.HeightN).Min();
            }

            var newInteractor = new Interactor();
            
            switch (newType)
            {
                case InteractorTypes.Key:
                    newInteractor = new DynamicKey() { RowN = 0, ColN = 0, WidthN = minKeyWidth, HeightN = minKeyHeight };
                    break;
                case InteractorTypes.Popup:
                    newInteractor = new DynamicPopup() { RowN = 0, ColN = 0 };
                    break;
                case InteractorTypes.OutputPanel:
                    newInteractor = new DynamicOutputPanel() { RowN = 0, ColN = 0, WidthN = XmlKeyboard.Cols, HeightN = 2 * minKeyHeight };
                    break;
                case InteractorTypes.Scratchpad:
                    newInteractor = new DynamicScratchpad() { RowN = 0, ColN = 0, WidthN = 8 * minKeyWidth, HeightN = minKeyHeight};
                    break;
                case InteractorTypes.SuggestionRow:
                    newInteractor = new DynamicSuggestionRow() { RowN = 0, ColN = 0, WidthN = 8 * minKeyWidth, HeightN = minKeyHeight };
                    break;
                case InteractorTypes.SuggestionColumn:
                    newInteractor = new DynamicSuggestionCol() { RowN = 0, ColN = 0, HeightN = 4 * minKeyHeight};
                    break;
            }

            foreach (var p in Profiles)
            {
                newInteractor.Profiles.Add(new InteractorProfileMap(p, p.Name == "All"));
            }

            var index = Interactor != null ? Interactors.IndexOf(Interactor) + 1 : 0;
            var cell = FindEmptySpace(newInteractor.HeightN, newInteractor.WidthN);
            if (cell != null)
            {
                newInteractor.RowN = cell.Item1;
                newInteractor.ColN = cell.Item2;
            }

            Interactors.Insert(index, newInteractor);
            XmlKeyboard.Interactors = Interactors.ToList();
            CreateViewbox();
            Interactor = newInteractor;
        }

        private void CloneInteractor()
        {
            if (Interactor == null) { return; }

            InitializeOccupancyArray();
            var serializer = new XmlSerializer(Interactor.GetType());
            var sw = new StringWriter();
            var xmlWriter = XmlWriter.Create(sw, new XmlWriterSettings());
            serializer.Serialize(xmlWriter, Interactor, new XmlSerializerNamespaces());
            var newInteractor = (Interactor)serializer.Deserialize(new StringReader(sw.ToString()));
            foreach (var p in Interactor.Profiles)
            {
                newInteractor.Profiles.Add(new InteractorProfileMap(p.Profile, p.IsMember));
            }
            var index = Interactors.IndexOf(Interactor) + 1;
            var cell = FindEmptySpace(newInteractor.HeightN, newInteractor.WidthN);
            if (cell != null)
            {
                newInteractor.RowN = cell.Item1;
                newInteractor.ColN = cell.Item2;
            }

            Interactors.Insert(index, newInteractor);
            XmlKeyboard.Interactors = Interactors.ToList();
            CreateViewbox();
            Interactor = newInteractor;
        }

        private void DeleteInteractor()
        {
            if (Interactor == null) { return; }

            Interactors.Remove(interactor);
            XmlKeyboard.Interactors = Interactors.ToList();
            CreateViewbox();
            Interactor = null;
        }

        private void ReplaceInteractor()
        {
            newType = selectedInteractorType;
            var index = Interactors.IndexOf(Interactor);
            Interactors.RemoveAt(index);
            if (index > 0)
                Interactor = Interactors[index - 1];
            AddInteractor();
        }

        private void SelectInteractor(object obj)
        {
            Interactor = (Interactor)obj;
        }

        private Canvas canvas;
        private Border gazeRegion = new Border() { Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#FB6043"), BorderThickness = new Thickness(5), Child = new Viewbox() { Stretch = Stretch.Uniform,
        StretchDirection = StretchDirection.Both, Child = new TextBlock() { Text = "Gaze\nHere", TextAlignment=TextAlignment.Center, Foreground = Brushes.White } } };
        public double ScreenWidth { get { return 1200; } }
        public double ScreenHeight { get { return 800; } }
        public double ScreenOffset { get { return .15 * ScreenWidth; } }
        public Thickness Margin { get { return new Thickness(Left, Top, 0, 0); } }
        public double Width { get { return XmlKeyboard.WidthN ?? ScreenWidth; } }
        public double Height { get { return XmlKeyboard.HeightN ?? ScreenHeight; } }
        public double Left
        {
            get
            {
                var offset = XmlKeyboard.HorizontalOffsetN ?? 0;
                return Enum.TryParse(XmlKeyboard.Position, out MoveToDirections newMovePosition)
                    ? newMovePosition == MoveToDirections.Left || newMovePosition == MoveToDirections.TopLeft || newMovePosition == MoveToDirections.BottomLeft
                        ? ScreenOffset + offset
                        : newMovePosition == MoveToDirections.Right || newMovePosition == MoveToDirections.TopRight || newMovePosition == MoveToDirections.BottomRight
                        ? ScreenOffset + ScreenWidth - Width + offset
                        : ScreenOffset + ScreenWidth / 2 - Width / 2 + offset
                    : ScreenOffset + offset;
            }
        }
        public double Top
        {
            get
            {
                var offset = XmlKeyboard.VerticalOffsetN ?? 0;
                return Enum.TryParse(XmlKeyboard.Position, out MoveToDirections newMovePosition)
                    ? newMovePosition == MoveToDirections.Top || newMovePosition == MoveToDirections.TopLeft || newMovePosition == MoveToDirections.TopRight
                        ? ScreenOffset + offset
                        : (newMovePosition == MoveToDirections.Bottom || newMovePosition == MoveToDirections.BottomLeft || newMovePosition == MoveToDirections.BottomRight)
                        ? ScreenOffset + ScreenHeight - Height + offset
                        : ScreenOffset + ScreenHeight / 2 - Height / 2 + offset
                    : ScreenOffset + offset;
            }
        }

        private void CreateViewbox()
        {
            Viewbox = null;

            var thickness = 40;
            var gazeTop = ScreenOffset + ScreenHeight + thickness + 2;
            var gazeCenterX = ScreenOffset + .5 * ScreenWidth;
            var gazeLeft = gazeCenterX - .45 * ScreenWidth;
            var gazeRight = gazeCenterX + .45 * ScreenWidth;
            if (canvas != null && canvas.Children != null)
                canvas.Children.Clear();

            var radialGradientBrush = new RadialGradientBrush()
            {
                GradientOrigin = new Point(.2, 1),
                Center = new Point(.2, 1),
                RadiusX = 4,
                RadiusY = 3
            };

            radialGradientBrush.GradientStops.Add(new GradientStop(Colors.Black, 0));
            radialGradientBrush.GradientStops.Add(new GradientStop(Colors.DarkGray, 1));

            canvas = new Canvas()
            {
                Background = radialGradientBrush,
                ClipToBounds = true,
                Width = 2 * ScreenOffset + ScreenWidth,
                Height = 2 * ScreenOffset + ScreenHeight,
            };
            canvas.Children.Add(new System.Windows.Shapes.Rectangle()
            {
                Fill = Brushes.Silver,
                Margin = new Thickness(ScreenOffset - thickness, ScreenOffset - thickness, 0, 0),
                Width = ScreenWidth + 2 * thickness,
                Height = ScreenHeight + 2 * thickness,
                RadiusX = 10,
                RadiusY = 10
            });
            canvas.Children.Add(new System.Windows.Shapes.Rectangle()
            {
                Fill = radialGradientBrush,
                Margin = new Thickness(ScreenOffset, ScreenOffset, 0, 0),
                Width = ScreenWidth,
                Height = ScreenHeight
            });
            canvas.Children.Add(new System.Windows.Shapes.Rectangle()
            {
                Fill = Brushes.LightGray,
                Margin = new Thickness(gazeLeft, gazeTop, 0, 0),
                Width = gazeRight - gazeLeft,
                Height = 40,
                RadiusX = 20,
                RadiusY = 20
            });
            canvas.Children.Add(new System.Windows.Shapes.Rectangle()
            {
                Fill = Brushes.Black,
                Margin = new Thickness(gazeLeft + 2, gazeTop + 2, 0, 0),
                Width = 80,
                Height = 36,
                RadiusX = 18,
                RadiusY = 18
            });
            canvas.Children.Add(new System.Windows.Shapes.Rectangle()
            {
                Fill = Brushes.Black,
                Margin = new Thickness(gazeRight - 82, gazeTop + 2, 0, 0),
                Width = 80,
                Height = 36,
                RadiusX = 18,
                RadiusY = 18
            });
            canvas.Children.Add(new System.Windows.Shapes.Ellipse()
            {
                Fill = Brushes.Red,
                Margin = new Thickness(gazeLeft + 24, gazeTop + 6, 0, 0),
                Width = 28,
                Height = 28
            });
            canvas.Children.Add(new System.Windows.Shapes.Ellipse()
            {
                Fill = Brushes.Red,
                Margin = new Thickness(gazeRight - 52, gazeTop + 6, 0, 0),
                Width = 28,
                Height = 28
            });
            canvas.Children.Add(new System.Windows.Shapes.Rectangle()
            {
                Fill = Brushes.Black,
                Margin = new Thickness(gazeCenterX - 60, gazeTop + 2, 0, 0),
                Width = 120, Height = 36,
                RadiusX = 18, RadiusY = 18
            });
            canvas.Children.Add(new System.Windows.Shapes.Ellipse()
            {
                Fill = Brushes.Red,
                Margin = new Thickness(gazeCenterX - 36, gazeTop + 12, 0, 0),
                Width = 16,
                Height = 16
            });
            canvas.Children.Add(new System.Windows.Shapes.Ellipse()
            {
                Fill = Brushes.Red,
                Margin = new Thickness(gazeCenterX + 20, gazeTop + 12, 0, 0),
                Width = 16,
                Height = 16
            });

            var dynamicKeyboard = new Views.Keyboards.Common.DynamicKeyboard(XmlKeyboard);
            dynamicKeyboard.SetBinding(FrameworkElement.WidthProperty, new Binding("Width") { Source = this });
            dynamicKeyboard.SetBinding(FrameworkElement.HeightProperty, new Binding("Height") { Source = this });
            dynamicKeyboard.SetBinding(FrameworkElement.MarginProperty, new Binding("Margin") { Source = this });
            canvas.Children.Add(dynamicKeyboard);

            foreach (var i in Interactors.Where(x => x.Key != null))
            {
                i.Key.InputBindings.Add(new MouseBinding()
                {
                    MouseAction = MouseAction.LeftClick,
                    Command = InteractorCommand,
                    CommandParameter = i
                });

                if (i is DynamicPopup)
                {
                    var parent = VisualAndLogicalTreeHelper.FindVisualParent<Grid>(i.Key);
                    if (parent != null)
                        parent.Children.Remove(i.Key);
                    i.Key.Margin = new Thickness(ScreenOffset + i.Key.GazeRegion.Left * ScreenWidth, ScreenOffset + i.Key.GazeRegion.Top * ScreenHeight, 0, 0);
                    i.Key.Width = i.Key.GazeRegion.Width * ScreenWidth;
                    i.Key.Height = i.Key.GazeRegion.Height * ScreenHeight;
                    canvas.Children.Add(i.Key);
                }
                if (i is DynamicOutputPanel)
                {
                    foreach (var item in VisualAndLogicalTreeHelper.FindLogicalChildren<Output>(dynamicKeyboard.MainGrid)
                        .Where(x => Grid.GetRow(x) == i.RowN && Grid.GetColumn(x) == i.ColN))
                    {
                        item.InputBindings.Add(new MouseBinding()
                        {
                            MouseAction = MouseAction.LeftClick,
                            Command = InteractorCommand,
                            CommandParameter = i
                        });
                    }
                }
                if (i is DynamicScratchpad)
                {
                    foreach (var item in VisualAndLogicalTreeHelper.FindLogicalChildren<Scratchpad>(dynamicKeyboard.MainGrid)
                        .Where(x => Grid.GetRow(x) == i.RowN && Grid.GetColumn(x) == i.ColN))
                    {
                        item.InputBindings.Add(new MouseBinding()
                        {
                            MouseAction = MouseAction.LeftClick,
                            Command = InteractorCommand,
                            CommandParameter = i
                        });
                    }
                }
                if (i is DynamicSuggestionRow || i is DynamicSuggestionCol)
                {
                    foreach (var grid in VisualAndLogicalTreeHelper.FindLogicalChildren<Grid>(dynamicKeyboard.MainGrid)
                        .Where(x => Grid.GetRow(x) == i.RowN && Grid.GetColumn(x) == i.ColN))
                    {
                        foreach(var item in VisualAndLogicalTreeHelper.FindLogicalChildren<Key>(grid))
                        {
                            item.InputBindings.Add(new MouseBinding()
                            {
                                MouseAction = MouseAction.LeftClick,
                                Command = InteractorCommand,
                                CommandParameter = i
                            });
                        }
                    }
                }
                if (i == Interactor)
                {
                    Interactor = null;
                    Interactor = i;
                }
            }
            Viewbox = new Viewbox();
            Viewbox.Width = 1 * SystemParameters.VirtualScreenWidth;
            Viewbox.Height = 1 * SystemParameters.VirtualScreenHeight;
            Viewbox.Stretch = Stretch.Fill;
            Viewbox.StretchDirection = StretchDirection.Both;
            Viewbox.Child = canvas;
        }

        #endregion

    }
}