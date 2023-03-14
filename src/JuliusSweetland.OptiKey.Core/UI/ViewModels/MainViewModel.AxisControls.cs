// Copyright (c) 2022 OPTIKEY LTD (UK company number 11854839) - All Rights Reserved
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using JuliusSweetland.OptiKey.Enums;
using JuliusSweetland.OptiKey.Extensions;
using JuliusSweetland.OptiKey.Models;
using JuliusSweetland.OptiKey.Native;
using JuliusSweetland.OptiKey.Native.Common.Enums;
using JuliusSweetland.OptiKey.Native.Common.Static;
using JuliusSweetland.OptiKey.Native.Common.Structs;
using JuliusSweetland.OptiKey.Properties;
using JuliusSweetland.OptiKey.Static;

namespace JuliusSweetland.OptiKey.UI.ViewModels
{
    partial class MainViewModel
    {
        public Dictionary<FunctionKeys, AxisControl> AxisControls;

        private void Enable(KeyValue kv)
        {
            Log.Info("Choosing control bounds target.");
            Action<bool, Point?, Rect?> callback = (success, point, bounds) =>
            {
                if (!success)
                {
                    AxisControls[kv.FunctionKey.Value].Disable();
                }
                else
                {
                    if (point.HasValue && Settings.Default.LookToScrollCentreMouseWhenActivated)
                    {
                        Log.Info("Moving mouse to center of deadzone.");
                        Action reinstateModifiers = () => { };
                        if (keyStateService.SimulateKeyStrokes && Settings.Default.SuppressModifierKeysForAllMouseActions)
                            reinstateModifiers = keyStateService.ReleaseModifiers(Log);
                            
                        mouseOutputService.MoveTo(point.Value);
                        reinstateModifiers();
                    }
                    AxisControls[kv.FunctionKey.Value].Enable(kv, point, bounds);
                }
            };

            if (kv.FunctionKey.Value == FunctionKeys.LookToScrollActive && string.IsNullOrWhiteSpace(kv.String))
            {
                switch (Settings.Default.LookToScrollBounds)
                {
                    case LookToScrollBounds.ScreenPoint:
                        ChoosePointBoundsTarget(callback);
                        break;
                    case LookToScrollBounds.ScreenCentred:
                        ChooseScreenBoundsTarget(callback);
                        break;
                    case LookToScrollBounds.Window:
                        ChooseWindowBoundsTarget(callback);
                        break;
                    case LookToScrollBounds.Custom:
                        ChooseCustomBoundsTarget(callback);
                        break;
                }
            }
            else if (kv.String.Contains("point"))
                ChoosePointBoundsTarget(callback);
            else if (kv.String.Contains("window"))
                ChooseWindowBoundsTarget(callback);
            else if (kv.String.Contains("custom"))
                ChooseCustomBoundsTarget(callback);
            else
                AxisControls[kv.FunctionKey.Value].Enable(kv);
        }

        private void ChooseScreenBoundsTarget(Action<bool, Point?, Rect?> callback)
        {
            Log.Info("Will use entire screen as the control bounds.");
            callback(true, new Point(Graphics.PrimaryScreenWidthInPixels / 2, Graphics.PrimaryScreenHeightInPixels / 2), null);
        }

        private void ChoosePointBoundsTarget(Action<bool, Point?, Rect?> callback)
        {
            Log.Info("Choosing point on screen to use for control.");
            SetupFinalClickAction(point =>
            {
                if (point.HasValue)
                {
                    ShowCursor = false;
                    IntPtr hWnd = GetHwndForFrontmostWindowAtPoint(point.Value);

                    if (hWnd == IntPtr.Zero)
                        Log.InfoFormat("No valid window to bring to front at point {0}.", point.Value);
                    else if (!PInvoke.SetForegroundWindow(hWnd))
                        Log.WarnFormat("Could not bring window {0} to front at point {1}.", hWnd, point.Value);
                    else
                        Log.InfoFormat("Brought window {0} to front at point {1}.", hWnd, point.Value);
                }

                ResetAndCleanupAfterMouseAction();
                callback(point.HasValue, point, null);
            }, suppressMagnification: true);
        }

        private void ChooseWindowBoundsTarget(Action<bool, Point?, Rect?> callback)
        {
            Log.Info("Choosing a window to use as control bounds.");
            SetupFinalClickAction(point =>
            {
                IntPtr hWnd = IntPtr.Zero;
                Rect? bounds = null;
                if (point.HasValue)
                {
                    ShowCursor = false;
                    hWnd = GetHwndForFrontmostWindowAtPoint(point.Value);
                    bounds = GetWindowBounds(hWnd);

                    if (hWnd == IntPtr.Zero || !bounds.HasValue)
                    {
                        Log.InfoFormat("No valid window to bring to front at point {0}.", point.Value);
                        audioService.PlaySound(Settings.Default.ErrorSoundFile, Settings.Default.ErrorSoundVolume);
                    }
                    else if (!PInvoke.SetForegroundWindow(hWnd))
                    {
                        Log.WarnFormat("Could not bring window {0} to front at point {1}.", hWnd, point.Value);
                        audioService.PlaySound(Settings.Default.ErrorSoundFile, Settings.Default.ErrorSoundVolume);
                        hWnd = IntPtr.Zero; // Make callback fail
                    }
                    else
                    {
                        Log.InfoFormat("Brought window {0} to front at point {1}.", hWnd, point.Value);
                    }
                }

                ResetAndCleanupAfterMouseAction();
                callback(hWnd != IntPtr.Zero, point, bounds);
            }, suppressMagnification: true);
        }

        private IntPtr GetHwndForFrontmostWindowAtPoint(Point point)
        {
            IntPtr shellWindow = PInvoke.GetShellWindow();

            Func<IntPtr, bool> criteria = hWnd => 
            {
                // Exclude the shell and Optikey windows
                if (hWnd == shellWindow || hWnd == mainWindowManipulationService.WindowHandle)
                    return false;

                // Exclude windows that aren't visible or that have been minimized.
                if (!PInvoke.IsWindowVisible(hWnd) || PInvoke.IsIconic(hWnd))
                    return false;

                // Exclude popup windows that have neither a frame like those used for regular windows
                // nor a frame like those used for dialog windows. This is intended to filter out things
                // like the lock screen, Start screen, and desktop wallpaper manager without filtering out
                // legitimate popup windows like "Open" and "Save As" dialogs as well as UWP apps.
                var style = Static.Windows.GetWindowStyle(hWnd);
                if ((style & WindowStyles.WS_POPUP) != 0 && 
                    (style & WindowStyles.WS_THICKFRAME) == 0 && 
                    (style & WindowStyles.WS_DLGFRAME) == 0)
                    return false;

                // Exclude transparent windows.
                var exStyle = Static.Windows.GetExtendedWindowStyle(hWnd);
                if (exStyle.HasFlag(ExtendedWindowStyles.WS_EX_TRANSPARENT))
                    return false;

                // Only include windows that contain the point.
                Rect? bounds = GetWindowBounds(hWnd);
                return bounds.HasValue && bounds.Value.Contains(point);
            };

            // Find the front-most top-level window that matches our criteria (expanding UWP apps into their CoreWindows).
            List<IntPtr> windows = Static.Windows.GetHandlesOfTopLevelWindows();
            windows = Static.Windows.ReplaceUWPTopLevelWindowsWithCoreWindowChildren(windows);
            windows = windows.Where(criteria).ToList();
            return Static.Windows.GetFrontmostWindow(windows);
        }

        private void ChooseCustomBoundsTarget(Action<bool, Point?, Rect?> callback)
        {
            Log.Info("Choosing a rectangular region of screen to use as control bounds.");
            Rect? bounds = null;
            Point? point = null;
            Action finishUp = () => 
            {
                ResetAndCleanupAfterMouseAction();
                callback(!bounds.HasValue, point, bounds);
            };

            SetupFinalClickAction(firstCorner => 
            {
                if (firstCorner.HasValue)
                {
                    Log.InfoFormat("User chose {0} as first corner.", firstCorner.Value);
                    SetupFinalClickAction(secondCorner => 
                    {
                        if (secondCorner.HasValue)
                        {
                            Log.InfoFormat("User chose {0} as second corner.", secondCorner.Value);
                            bounds = new Rect(firstCorner.Value, secondCorner.Value);

                            if (bounds.Value.Width > Settings.Default.LookToScrollHorizontalDeadzone
                                && bounds.Value.Height > Settings.Default.LookToScrollVerticalDeadzone)
                            {
                                Log.InfoFormat("Selected rect {0} as control target.", bounds);
                                point = bounds.Value.CalculateCentre();
                            }
                            else
                            {
                                Log.Warn("Chosen rectangle is not large enough to accomodate deadzone.");
                                audioService.PlaySound(Settings.Default.ErrorSoundFile, Settings.Default.ErrorSoundVolume);
                            }
                        }

                        finishUp();
                    }, suppressMagnification: true);
                }
                else
                {
                    finishUp();
                }
            }, finalClickInSeries: false, suppressMagnification: true);
        }

        private Rect? GetWindowBounds(IntPtr hWnd)
        {
            if (!PInvoke.IsWindow(hWnd))
            {
                Log.WarnFormat("{0} does not exist or no longer points to a valid window.", hWnd);
                return null;
            }

            RECT rawRect;

            if (PInvoke.DwmGetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.ExtendedFrameBounds, out rawRect, Marshal.SizeOf<RECT>()) != 0)
            {
                Log.WarnFormat("Failed to get bounds of window {0} using DwmGetWindowAttribute. Falling back to GetWindowRect.", hWnd);

                if (!PInvoke.GetWindowRect(hWnd, out rawRect))
                {
                    Log.WarnFormat("Failed to get bounds of window {0} using GetWindowRect.", hWnd);
                    return null;
                }
            }

            return new Rect
            {
                X = rawRect.Left,
                Y = rawRect.Top,
                Width = rawRect.Right - rawRect.Left,
                Height = rawRect.Bottom - rawRect.Top
            };
        }

        private Action SuspendWhileChoosingPointForMouse()
        {
            Action resumeAction = () => { };
            var activeControls = GetActiveControls();
            foreach (var control in activeControls)
                AxisControls[control.FunctionKey].Disable();

            // If configured to resume afterwards, reapply the original state of controls
            if (Settings.Default.LookToScrollResumeAfterChoosingPointForMouse)
            {
                Log.Info("Axis controls have been suspended.");
                resumeAction = async () =>
                {
                    await Task.Delay(300);
                    Point? point = null;
                    foreach (var control in activeControls)
                    {
                        keyStateService.KeyDownStates[control.KeyValue].Value = KeyDownStates.LockedDown;
                        AxisControls[control.FunctionKey].Enable(control.KeyValue, control.Point, control.Bounds);
                        if (control.Point.HasValue)
                            point = control.Point.Value;
                    }

                    if (point.HasValue && Settings.Default.LookToScrollCentreMouseWhenActivated)
                    {
                        Log.Info("Moving mouse to center of deadzone.");
                        Action reinstateModifiers = () => { };
                        if (keyStateService.SimulateKeyStrokes && Settings.Default.SuppressModifierKeysForAllMouseActions)
                            reinstateModifiers = keyStateService.ReleaseModifiers(Log);

                        mouseOutputService.MoveTo(point.Value);
                        reinstateModifiers();
                    }
                    Log.Info("Axis controls have resumed.");
                };
            }
            else
                Log.Info("Axis controls have been suspended and will not automatically resume.");
            return resumeAction;
        }

        private void DeactivateLookToScrollUponSwitchingKeyboards()
        {
            if (Settings.Default.LookToScrollDeactivateUponSwitchingKeyboards)
            {
                if (AxisControls != null && AxisControls.ContainsKey(FunctionKeys.LookToScrollActive))
                    AxisControls[FunctionKeys.LookToScrollActive].Disable();

                Log.Info("Look to scroll is no longer active.");
            }
        }

        private IEnumerable<AxisControl> GetActiveControls()
        {
            return AxisControls.Where(x => x.Value.IsActive).Select(x => x.Value);
        }

        private void DisableAll()
        {
            foreach (var functionKey in AxisControls.Keys)
                AxisControls[functionKey].Disable();
        }

        private void ToggleAxisControl(KeyValue keyValue)
        {
            var functionKey = keyValue.FunctionKey.Value;
            if (keyStateService.KeyDownStates[keyValue].Value != KeyDownStates.Up)
            {
                // the following sets are mutually exclusive: (legacy, left) and (legacy, right)
                var fKeys = new[] { FunctionKeys.LeftJoystick, FunctionKeys.RightJoystick }.Contains(functionKey)
                    ? new[] { functionKey, FunctionKeys.LegacyJoystick } : functionKey == FunctionKeys.LegacyJoystick
                    ? new[] { functionKey, FunctionKeys.LeftJoystick, FunctionKeys.RightJoystick }
                    : new[] { functionKey };

                foreach (var kv in keyStateService.KeyDownStates.Keys.Where(x => x.FunctionKey.HasValue
                    && fKeys.Contains(x.FunctionKey.Value) && x != keyValue))
                    keyStateService.KeyDownStates[kv].Value = KeyDownStates.Up;
                
                Enable(keyValue);
            }
            else
            {
                AxisControls[functionKey].Disable();
            }
        }
    }
}
