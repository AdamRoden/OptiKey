// Copyright (c) 2022 OPTIKEY LTD (UK company number 11854839) - All Rights Reserved
using System;
using JuliusSweetland.OptiKey.Enums;

namespace JuliusSweetland.OptiKey.Services
{
    public interface IWindowManipulationService : INotifyErrors
    {
        event EventHandler SizeAndPositionInitialised;

        bool SizeAndPositionIsInitialised { get; }
        IntPtr WindowHandle { get; }
        WindowStates WindowState { get; }

        void ChangeState(WindowStates state, DockEdges dockPosition);
        void Expand(ExpandToDirections direction, double amountInPx);
        double GetOpacity();
        bool GetPersistedState();
        void Hide();
        void IncrementOrDecrementOpacity(bool increment);
        void InvokeMoveWindow(string parameterString);
        void Maximise();
        void Minimise();
        void Move(MoveToDirections direction, double? amountInPx);
        void PersistSizeAndPosition();
        void ResizeDockToCollapsed();
        void ResizeDockToFull();
        void Restore();
        void SetOpacity(double opacity);
        void Shrink(ShrinkFromDirections direction, double amountInPx);
        void OverridePersistedState(bool inPersistNewState, string inWindowState, string inPosition, string inDockSize, string inWidth, string inHeight, string inHorizontalOffset, string inVerticalOffset);
        void RestorePersistedState();
        void DisableResize();
        void SetResizeState();
    }
}
