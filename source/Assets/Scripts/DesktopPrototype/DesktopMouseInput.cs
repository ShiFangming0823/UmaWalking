using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace UmaDesktop
{
    // A private message-pump thread keeps the low-level callback responsive while
    // Unity loads a model. Only clicks on visible pet pixels are consumed.
    public static class DesktopMouseInput
    {
        public static volatile bool Enabled;
        public static bool Installed {get{return hook!=IntPtr.Zero;}}
        private static volatile WindowsOverlay[] overlays=new WindowsOverlay[0];
        private static IntPtr hook;
        private static Thread thread;
        private static uint threadId;
        private static int installError;
        private static bool swallowUp,dragged;
        private static WindowsOverlay pressedTarget;
        private static int pressX,pressY;
        private static readonly object gestureLock=new object();
        public static void CancelGesture(){lock(gestureLock){if(pressedTarget!=null)pressedTarget.RoutePointer(DesktopPointerKind.Cancel,0,0,dragged);pressedTarget=null;dragged=false;}}
        private static readonly HookProc callback=OnMouse;
        private static readonly ManualResetEvent started=new ManualResetEvent(false);

        internal static void Add(WindowsOverlay overlay)
        {
            var list=new List<WindowsOverlay>(overlays);list.Add(overlay);overlays=list.ToArray();
            if(thread!=null) return;
            started.Reset();installError=0;
            thread=new Thread(Pump) {IsBackground=true,Name="UmaDesktop pixel input"};thread.Start();
            if(!started.WaitOne(3000) || !Installed)
                throw new System.ComponentModel.Win32Exception(installError,"Could not install desktop pet mouse input.");
        }
        internal static void Remove(WindowsOverlay overlay)
        {
            lock(gestureLock){if(pressedTarget==overlay)CancelGesture();}
            var list=new List<WindowsOverlay>(overlays);list.Remove(overlay);overlays=list.ToArray();
            if(list.Count!=0 || thread==null) return;
            Enabled=false;PostThreadMessage(threadId,0x12,IntPtr.Zero,IntPtr.Zero);
            thread.Join(1000);thread=null;
        }
        private static void Pump()
        {
            SetThreadDpiAwarenessContext(new IntPtr(-4)); // physical screen pixels
            threadId=GetCurrentThreadId();MSG message;PeekMessage(out message,IntPtr.Zero,0,0,0);
            hook=SetWindowsHookEx(14,callback,GetModuleHandle(null),0);
            if(hook==IntPtr.Zero) installError=Marshal.GetLastWin32Error();
            started.Set();
            if(hook==IntPtr.Zero) return;
            try {while(GetMessage(out message,IntPtr.Zero,0,0)>0) {TranslateMessage(ref message);DispatchMessage(ref message);}}
            finally {UnhookWindowsHookEx(hook);hook=IntPtr.Zero;swallowUp=false;}
        }
        public static bool Opaque(byte[] bgra,int side,int column,int row,float opacity)
        {
            if(bgra==null || side<=0 || (uint)column>=(uint)side || (uint)row>=(uint)side || bgra.Length!=(long)side*side*4) return false;
            // Positive-height DIB is bottom-up; screen mouse Y is top-down.
            return bgra[((side-1-row)*side+column)*4+3]*opacity>=8;
        }
        private static IntPtr OnMouse(int code,IntPtr message,IntPtr data)
        {
            if(code>=0)
            {
                int msg=message.ToInt32();
                lock(gestureLock)
                {
                    if(msg==0x202 && swallowUp)
                    {
                        swallowUp=false;
                        if(pressedTarget!=null)
                        {
                            POINT p=Marshal.PtrToStructure<POINT>(data);
                            if(dragged)pressedTarget.RoutePointer(DesktopPointerKind.Move,p.x,p.y,true);
                            pressedTarget.RoutePointer(DesktopPointerKind.Up,p.x,p.y,dragged);
                            if(!dragged && Enabled)pressedTarget.RouteClick();
                        }
                        pressedTarget=null;dragged=false;return new IntPtr(1);
                    }
                    if(msg==0x200 && pressedTarget!=null)
                    {
                        if(!Enabled || !pressedTarget.DesktopVisible)CancelGesture();
                        else
                        {
                            POINT p=Marshal.PtrToStructure<POINT>(data);
                            long dx=(long)p.x-pressX,dy=(long)p.y-pressY;
                            if(dx*dx+dy*dy>=36)dragged=true;
                            if(dragged)pressedTarget.RoutePointer(DesktopPointerKind.Move,p.x,p.y,true);
                        }
                        // WH_MOUSE_LL runs before Windows updates the cursor. Suppressing
                        // WM_MOUSEMOVE freezes the cursor and makes every relative device
                        // delta start at the old position; mouse-up then snaps back there.
                        // Keep buttons captured, but let Windows process all movement.
                        return CallNextHookEx(hook,code,message,data);
                    }
                    if(msg==0x201 && Enabled)
                    {
                        try
                        {
                            POINT point=Marshal.PtrToStructure<POINT>(data);
                            IntPtr hit=WindowFromPoint(point),root=GetAncestor(hit,2);
                            var current=overlays;
                            for(int i=0;i<current.Length;i++)
                            {
                                var candidate=current[i];IntPtr parent=candidate.InputParent;
                                if(parent==IntPtr.Zero || root!=GetAncestor(parent,2) || !candidate.HitPixel(point.x,point.y))continue;
                                WindowsOverlay target=candidate;
                                for(IntPtr child=GetWindow(parent,5);child!=IntPtr.Zero;child=GetWindow(child,2))
                                {
                                    WindowsOverlay found=null;
                                    for(int k=0;k<current.Length;k++)if(current[k].Handle==child && current[k].HitPixel(point.x,point.y)){found=current[k];break;}
                                    if(found!=null){target=found;break;}
                                }
                                CancelGesture();pressedTarget=target;pressX=point.x;pressY=point.y;dragged=false;
                                target.RoutePointer(DesktopPointerKind.Down,point.x,point.y,false);swallowUp=true;return new IntPtr(1);
                            }
                        }
                        catch { /* Never propagate an exception into the system input chain. */ }
                    }
                }
            }
            return CallNextHookEx(hook,code,message,data);
        }
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
        [StructLayout(LayoutKind.Sequential)] private struct POINT {public int x,y;}
        [StructLayout(LayoutKind.Sequential)] private struct MSG {public IntPtr hwnd;public uint message;public UIntPtr wParam;public IntPtr lParam;public uint time;public POINT point;public uint lPrivate;}
        [DllImport("user32.dll",SetLastError=true)] private static extern IntPtr SetWindowsHookEx(int id,HookProc proc,IntPtr module,uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr h,int code,IntPtr w,IntPtr l);
        [DllImport("user32.dll")] private static extern int GetMessage(out MSG msg,IntPtr h,uint min,uint max);
        [DllImport("user32.dll")] private static extern bool PeekMessage(out MSG msg,IntPtr h,uint min,uint max,uint remove);
        [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG msg);
        [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG msg);
        [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint id,uint msg,IntPtr w,IntPtr l);
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT point);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr h,uint flags);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr h,uint command);
        [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
    }
}
