using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using JuliusSweetland.OptiKey.Enums;
using JuliusSweetland.OptiKey.Extensions;
using JuliusSweetland.OptiKey.Models;
using JuliusSweetland.OptiKey.Models.ScalingModels;
using JuliusSweetland.OptiKey.Properties;
using JuliusSweetland.OptiKey.Services;
using JuliusSweetland.OptiKey.Static;
using log4net;
using Prism.Mvvm;
using SpeechLib;

namespace JuliusSweetland.OptiKey.UI.ViewModels
{
    public class AxisControl : BindableBase, IOverlayViewModel
    {
        #region Fields

        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private readonly IKeyStateService keyStateService;
        private readonly Action<float, float> updateAction;
        private readonly FunctionKeys functionKey;
        private IScalingModel sensitivityFunction;

        #endregion

        #region Constructor

        public AxisControl(FunctionKeys functionKey, Action<float, float> updateAction, IKeyStateService keyStateService)
        {
            this.functionKey = functionKey;
            this.updateAction = updateAction;
            this.keyStateService = keyStateService;
        }

        #endregion

        #region Properties

        private bool isActive = false;
        public bool IsActive
        {
            get { return isActive; }
            private set { SetProperty(ref isActive, value); }
        }

        private List<Region> contours = new List<Region>();
        public List<Region> Contours
        {
            get { return contours; }
            private set { SetProperty(ref contours, value); }
        }

        public Rect? Bounds { get; private set; }
        public Point? Point { get; private set; }
        public FunctionKeys FunctionKey { get; private set; }
        public KeyValue KeyValue { get; private set; }

        #endregion

        #region Public methods

        public void Enable(KeyValue keyValue, Point? point = null, Rect? bounds = null)
        {
            Log.InfoFormat("Activating axis control: {0}", this.functionKey);
            KeyValue = keyValue;
            Point = point;
            Bounds = bounds;
            sensitivityFunction = CreateFunction();

            contours = sensitivityFunction.GetContours();
            RaisePropertyChanged("Contours");
            IsActive = true;
        }

        public void Disable()
        {
            foreach (var keyValue in keyStateService.KeyDownStates.Keys
                .Where(x => x.FunctionKey.HasValue && x.FunctionKey.Value == functionKey))
                keyStateService.KeyDownStates[keyValue].Value = KeyDownStates.Up;

            if (IsActive)
            {
                Log.InfoFormat("Disabling axis control: {0}", this.functionKey);
                IsActive = false;
                updateAction(0.0f, 0.0f);
            }
        }       

        public void UpdatePosition(Point position)
        {
            if (keyStateService.KeyDownStates[KeyValues.SleepKey].Value == KeyDownStates.Up
                && sensitivityFunction.Contains(position)
                && isActive)
            {
                Log.DebugFormat("Updating control using position: {0}.", position);
                var scrollAmount = sensitivityFunction.CalculateScaling(position);

                Action reinstateModifiers = () => { };
                if (functionKey == FunctionKeys.ScrollJoystick && keyStateService.SimulateKeyStrokes
                    && Settings.Default.SuppressModifierKeysForAllMouseActions)
                    reinstateModifiers = keyStateService.ReleaseModifiers(Log);
                
                updateAction((float)scrollAmount.X, (float)scrollAmount.Y);
                
                reinstateModifiers();
            }
            else
            {
                updateAction(0.0f, 0.0f);
            }
        }
        #endregion

        #region Private methods

        private IScalingModel CreateFunction()
        {
            var regions = new List<Region>();
            var defaultOpacity = .2;
            var scrWidth = Graphics.PrimaryScreenWidthInPixels;
            var scrHeight = Graphics.PrimaryScreenHeightInPixels;
            var scrRatio = scrWidth / scrHeight;
            double width, height, amountX, amountY, centerX, centerY, curve, opacity;
            if (string.IsNullOrWhiteSpace(KeyValue.String))
            {
                var center = Point.HasValue ? new Point(Point.Value.X / scrWidth, Point.Value.Y / scrHeight) : new Point(.5, .5);
                var bounds = Bounds.HasValue ? new Rect(Bounds.Value.X / scrWidth, Bounds.Value.Y / scrHeight,
                    Bounds.Value.Width / scrWidth, Bounds.Value.Height / scrHeight) : new Rect(0, 0, 1, 1);
                //l, t, w, h, r, ax, ay, o
                regions.Add(new Region(center.X - .05, center.Y - .05 * scrRatio, .1, .1 * scrRatio, 1, 0, 0, defaultOpacity));
                regions.Add(new Region(center.X - .45 * bounds.Width, center.Y - .45 * bounds.Height, .9 * bounds.Width, .9 * bounds.Height, 0, 1, 1, 0));
                regions.Add(new Region(center.X - .5 * bounds.Width, center.Y - .5 * bounds.Height, bounds.Width, bounds.Height, 0, 1, 1, defaultOpacity));
                return new RegionScaling(regions);
            }
            var inputString = KeyValue.String.RemoveWhitespace().ToLower();

            //one of the following for each region:
            //(width, amountX)
            //(width, height, amountX)
            //(width, height, amountX, amountY)
            //(width, height, amountX, amountY, centerX, centerY, curve, opacity)
            //(width,height,amount,,,,,opacity)
            //defaults: height=width, amountY=amountX, centerX=.5, centerY=.5, curve=0, opacity=0 unless first or last region
            var regex = new Regex(@"rings([\d.,()]*)");
            var regex1 = new Regex(@"point([\d.,()]*)"); //center chosen by gaze
            var regex2 = new Regex(@"window([\d.,()]*)"); //center and outer bounds chosen by gaze
            var regex3 = new Regex(@"custom([\d.,()]*)"); //center and outer bounds chosen by gaze
            regex = regex.IsMatch(inputString) ? regex
                : regex1.IsMatch(inputString) ? regex1
                : regex2.IsMatch(inputString) ? regex2
                : regex3.IsMatch(inputString) ? regex3 : null;
            if (regex != null)
            {
                Match m = regex.Match(inputString);
                if (m.Success)
                {
                    var inputData = m.Groups[1].Captures[0].ToString();
                    var inputList = Regex.Split(inputData, @"[\s \)\(]").Where(s => s != string.Empty);
                    if (inputList.Any())
                    {
                        int i = 0;
                        int iMax = inputList.Count();
                        foreach (var input in inputList)
                        {
                            i++;
                            var nums = (from split in string.Concat(input, ",,,,,,,").Split(',')
                                        select split.ToNullDouble()).ToArray();
                            if (nums[0].HasValue && nums[1].HasValue)
                            {
                                width = i == iMax && Bounds.HasValue ? Bounds.Value.Width
                                    : nums[1].HasValue && nums[2].HasValue ? nums[0].Value : nums[0].Value / scrRatio;
                                height = i == iMax && Bounds.HasValue ? Bounds.Value.Height
                                    : nums[1].HasValue && nums[2].HasValue ? nums[1].Value : width * scrRatio;
                                amountX = nums[2] ?? nums[1].Value;
                                amountY = nums[3] ?? amountX;
                                centerX = Point.HasValue ? Point.Value.X : nums[4] ?? .5;
                                centerY = Point.HasValue ? Point.Value.Y : nums[5] ?? .5;
                                curve = nums[6] ?? (nums[1].HasValue && nums[2].HasValue ? 0 : 1);
                                opacity = nums[7] ?? (1 < i && i < iMax ? 0 : defaultOpacity);

                                regions.Add(new Region(centerX - .5 * width, centerY - .5 * height,
                                    width, height, curve, amountX, amountY, opacity));
                            }
                        }
                    }
                    if (regions.Count > 1)
                        return new RegionScaling(regions);
                }
            }

            //fallback - basic 
            return CreateFunction();
        }
        #endregion
    }
}