using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

namespace UmaDesktop
{
    // Integer screen coordinates; kept independent of Unity for native integration tests.
    public struct DesktopRectangle
    {
        public int Left, Top, Right, Bottom;
        public DesktopRectangle(int l, int t, int r, int b) { Left=l;Top=t;Right=r;Bottom=b; }
        public bool Empty { get { return Right<=Left || Bottom<=Top; } }
    }
    public static class DesktopCoverage
    {
        // Subtract the union of opaque windows from the desktop. A partially visible
        // desktop continues running, including gaps between tiled windows.
        public static bool FullyCovered(DesktopRectangle desktop, IEnumerable<DesktopRectangle> windows)
        {
            if(desktop.Empty) return false;
            var visible=new List<DesktopRectangle> {desktop};
            foreach(var cover in windows)
            {
                var next=new List<DesktopRectangle>();
                foreach(var area in visible)
                {
                    int l=Math.Max(area.Left,cover.Left), t=Math.Max(area.Top,cover.Top);
                    int r=Math.Min(area.Right,cover.Right), b=Math.Min(area.Bottom,cover.Bottom);
                    if(l>=r || t>=b) { next.Add(area);continue; }
                    if(area.Top<t) next.Add(new DesktopRectangle(area.Left,area.Top,area.Right,t));
                    if(b<area.Bottom) next.Add(new DesktopRectangle(area.Left,b,area.Right,area.Bottom));
                    if(area.Left<l) next.Add(new DesktopRectangle(area.Left,t,l,b));
                    if(r<area.Right) next.Add(new DesktopRectangle(r,t,area.Right,b));
                }
                visible=next;
                if(visible.Count==0) return true;
                // Conservatively keep rendering if an unusual window layout exceeds our budget.
                if(visible.Count>4096) return false;
            }
            return false;
        }
    }
    // Keep temporary window-management UI from changing an established pause state.
    public sealed class DesktopCoverageState
    {
        private bool initialized,covered;
        private double pending=double.NaN;
        public bool Evaluate(bool rawCovered,bool windowManagement,double seconds)
        {
            if(!initialized) {initialized=true;covered=rawCovered;return covered;}
            if(windowManagement) {pending=double.NaN;return covered;}
            if(!rawCovered) {covered=false;pending=double.NaN;return false;}
            if(covered) return true;
            if(double.IsNaN(pending)) pending=seconds;
            if(seconds-pending>=0.75) covered=true;
            return covered;
        }
    }
    public static class DesktopTransientWindows
    {
        public static bool Candidate(string cls)
        {
            return cls=="FancyZones_ZonesOverlay" || cls=="MultitaskingViewFrame" || cls=="TaskSwitcherWnd" ||
                cls=="XamlExplorerHostIslandWindow" || cls=="ForegroundStaging" || cls=="Windows.UI.Core.CoreWindow";
        }
        public static bool Matches(string cls,string process,string title,bool switching)
        {
            if(cls=="FancyZones_ZonesOverlay") return string.Equals(process,"PowerToys.FancyZones",StringComparison.OrdinalIgnoreCase);
            bool shell=string.Equals(process,"explorer",StringComparison.OrdinalIgnoreCase);
            if(shell && (cls=="MultitaskingViewFrame" || cls=="TaskSwitcherWnd")) return true;
            if(shell && cls=="XamlExplorerHostIslandWindow")
                return switching || title=="Task Switching" || title=="任务切换";
            if(shell && cls=="ForegroundStaging") return switching;
            return cls=="Windows.UI.Core.CoreWindow" && switching && string.Equals(process,"ShellExperienceHost",StringComparison.OrdinalIgnoreCase);
        }
    }
    public static class DesktopEnvironment
    {
        public static IntPtr Host { get; private set; }
        public static bool Covered { get; private set; }
        public static bool Available { get; private set; }
        public static int CoveringWindows { get; private set; }
        public static bool WindowManagementActive { get; private set; }
        public static bool SwitchEventHookInstalled { get { return switchHook!=IntPtr.Zero; } }
        private static DesktopCoverageState coverageState=new DesktopCoverageState();
        private static readonly Stopwatch clock=Stopwatch.StartNew();
        private static readonly WinEventProc switchCallback=OnSwitchEvent;
        private static IntPtr switchHook;
        private static bool switching,hookAttempted;
        public static DesktopRectangle WorkArea { get; private set; }
        private delegate bool EnumProc(IntPtr h,IntPtr value);

        public static void Poll()
        {
#if UNITY_EDITOR
            Available=true;Covered=false;
#else
            if(!hookAttempted) {hookAttempted=true;switchHook=SetWinEventHook(0x14,0x15,IntPtr.Zero,switchCallback,0,0,2);}
            // Use the Explorer window which owns the desktop icons. Pets become child
            // windows of this desktop layer, never top-level or topmost app windows.
            // No undocumented Explorer messages, wallpaper changes or shell restarts.
            IntPtr shell=GetShellWindow(); uint shellPid=0;
            if(shell!=IntPtr.Zero) GetWindowThreadProcessId(shell,out shellPid);
            IntPtr found=IntPtr.Zero;
            EnumWindows(delegate(IntPtr h,IntPtr unused) {
                if(FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null)==IntPtr.Zero) return true;
                uint pid;GetWindowThreadProcessId(h,out pid);
                if(shellPid!=0 && pid==shellPid) {found=h;return false;} return true;
            },IntPtr.Zero);
            Host=found;
            RECT work;
            Available=Host!=IntPtr.Zero && IsWindowVisible(Host) && SystemParametersInfo(0x30,0,out work,0);
            // Definite assignment also makes failure leave no stale screen bounds.
            if(!Available) {Covered=true;WindowManagementActive=false;CoveringWindows=0;WorkArea=new DesktopRectangle();coverageState=new DesktopCoverageState();return;}
            SystemParametersInfo(0x30,0,out work,0);
            WorkArea=new DesktopRectangle(work.left,work.top,work.right,work.bottom);
            var covers=new List<DesktopRectangle>();
            bool switchSignal=switching && (GetAsyncKeyState(0x12)&0x8000)!=0;
            bool transient=false;
            EnumWindows(delegate(IntPtr h,IntPtr unused) {
                // Show Desktop can raise Explorer above unminimized windows. Windows
                // below the desktop cannot cover it, so stop at its actual Z position.
                if(h==Host) return false;
                if(!IsWindowVisible(h) || IsIconic(h)) return true;
                int cloaked;
                if(DwmGetWindowAttribute(h,14,out cloaked,4)==0 && cloaked!=0) return true;
                var name=new StringBuilder(256);GetClassName(h,name,name.Capacity);
                string cls=name.ToString();
                if(DesktopTransientWindows.Candidate(cls))
                {
                    uint pid;GetWindowThreadProcessId(h,out pid);string process="";
                    try {using(var owner=Process.GetProcessById((int)pid)) process=owner.ProcessName;} catch(ArgumentException) {} catch(System.ComponentModel.Win32Exception) {} catch(InvalidOperationException) {}
                    var title=new StringBuilder(256);
                    if(cls=="XamlExplorerHostIslandWindow") GetWindowText(h,title,title.Capacity);
                    if(DesktopTransientWindows.Matches(cls,process,title.ToString(),switchSignal)) {transient=true;return true;}
                }
                long ex=GetWindowLongPtr(h,-20).ToInt64();
                // Only count a layered window when its global alpha proves it opaque.
                // A per-pixel alpha overlay can have screen-sized invisible bounds.
                if((ex & 0x00080000L)!=0)
                {
                    uint key,flags;byte alpha;
                    if(!GetLayeredWindowAttributes(h,out key,out alpha,out flags) || (flags&1)!=0 || (flags&2)!=0 && alpha<255) return true;
                }
                else if((ex & 0x00000020L)!=0) return true;
                if(cls=="Shell_TrayWnd" || cls=="Shell_SecondaryTrayWnd" || cls=="Progman" || cls=="WorkerW" || cls.StartsWith("UmaDesktopPixelOverlay",StringComparison.Ordinal)) return true;
                RECT rect;
                if(DwmGetWindowAttribute(h,9,out rect,Marshal.SizeOf(typeof(RECT)))!=0 && !GetWindowRect(h,out rect)) return true;
                var candidate=new DesktopRectangle(rect.left,rect.top,rect.right,rect.bottom);
                if(!candidate.Empty && candidate.Right>work.left && candidate.Left<work.right && candidate.Bottom>work.top && candidate.Top<work.bottom) covers.Add(candidate);
                return true;
            },IntPtr.Zero);
            if(switching && !switchSignal && !transient) switching=false; // recover from a missed SWITCHEND
            WindowManagementActive=transient || switchSignal;
            CoveringWindows=covers.Count;
            Covered=coverageState.Evaluate(DesktopCoverage.FullyCovered(WorkArea,covers),WindowManagementActive,clock.Elapsed.TotalSeconds);
#endif
        }
        private static void OnSwitchEvent(IntPtr hook,uint ev,IntPtr window,int obj,int child,uint thread,uint time)
        {if(ev==0x14) switching=true;else if(ev==0x15) switching=false;}
        public static void Shutdown()
        {
            if(switchHook!=IntPtr.Zero) UnhookWinEvent(switchHook);
            switchHook=IntPtr.Zero;hookAttempted=false;switching=false;WindowManagementActive=false;coverageState=new DesktopCoverageState();
        }
        private delegate void WinEventProc(IntPtr hook,uint ev,IntPtr window,int obj,int child,uint thread,uint time);
        [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinEventProc callback,uint process,uint thread,uint flags);
        [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr h,StringBuilder text,int count);
        public static IntPtr EnterHostDpiContext()
        {
            return Host==IntPtr.Zero ? IntPtr.Zero : SetThreadDpiAwarenessContext(GetWindowDpiAwarenessContext(Host));
        }
        public static void RestoreDpiContext(IntPtr previous)
        { if(previous!=IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
        [StructLayout(LayoutKind.Sequential)] private struct RECT {public int left,top,right,bottom;}
        [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr p,IntPtr after,string cls,string title);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback,IntPtr value);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] private static extern bool GetLayeredWindowAttributes(IntPtr h,out uint key,out byte alpha,out uint flags);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr h,StringBuilder name,int size);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h,out RECT rect);
        [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint a,uint p,out RECT r,uint flags);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr h,int attr,out RECT rect,int size);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr h,int attr,out int value,int size);
        [DllImport("user32.dll")] private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr h);
        [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    }
}
