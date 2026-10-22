using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace UmaDesktop
{
    // Each actor owns a premultiplied BGRA desktop child window. Explorer may
    // pass child-window mouse input through; DesktopMouseInput routes visible alpha hits.
    public enum DesktopPointerKind {Down,Move,Up,Cancel}
    public struct DesktopPointerEvent {public DesktopPointerKind Kind;public int X,Y;public bool Dragged;}
    public sealed class WindowsOverlay : IDisposable
    {
        public Rect ScreenArea { get; private set; }
        public Rect WorkArea { get; private set; }
        public bool Ready { get; private set; }
        public float Opacity = 1;
        public int Clicks, FramesPresented;
        public void RaiseWithinDesktop() {if(window!=IntPtr.Zero)SetWindowPos(window,IntPtr.Zero,0,0,0,0,0x0001|0x0002|0x0010);}
        public IntPtr Handle { get { return window; } }
        private IntPtr window, memoryDC, bitmap, oldBitmap, bits;
        private int size, x, y;
        private byte[] buffer;
        public static bool InputEnabled { get { return DesktopMouseInput.Enabled; } set { DesktopMouseInput.Enabled=value;if(!value)DesktopMouseInput.CancelGesture(); } }
        public static bool InputInstalled { get { return DesktopMouseInput.Installed; } }
        private readonly object pointerLock=new object();
        private readonly List<DesktopPointerEvent> pointerEvents=new List<DesktopPointerEvent>();
        public DesktopPointerEvent[] TakePointerEvents(){lock(pointerLock){var result=pointerEvents.ToArray();pointerEvents.Clear();return result;}}
        public void RoutePointer(DesktopPointerKind kind,int px,int py,bool dragged){lock(pointerLock){var e=new DesktopPointerEvent {Kind=kind,X=px,Y=py,Dragged=dragged};if(kind==DesktopPointerKind.Move && pointerEvents.Count>0 && pointerEvents[pointerEvents.Count-1].Kind==kind)pointerEvents[pointerEvents.Count-1]=e;else pointerEvents.Add(e);}}
        public int TakeClicks() { return Interlocked.Exchange(ref Clicks,0); }
        private bool desktopVisible, hasPixels;
        private IntPtr desktopParent;
        private string title;
        private static readonly uint processId=(uint)Process.GetCurrentProcess().Id;
        private bool OwnsWindow
        {
            get { uint pid;return window!=IntPtr.Zero && IsWindow(window) && GetWindowThreadProcessId(window,out pid)!=0 && pid==processId; }
        }
        public bool DesktopAttached { get { return Ready && OwnsWindow && GetParent(window)==DesktopEnvironment.Host; } }
        public bool DesktopVisible { get { return DesktopAttached && IsWindowVisible(window); } }
        public long DesktopParent { get { return DesktopAttached ? GetParent(window).ToInt64() : 0; } }
        private static readonly Dictionary<IntPtr, WindowsOverlay> owners = new Dictionary<IntPtr, WindowsOverlay>();
        private static readonly WindowProc procedure = Dispatch;
        private static ushort windowClass;
        private static readonly HashSet<int> pressed = new HashSet<int>();
        private const string ClassName = "UmaDesktopPixelOverlayV3";

        public void SetTitle(string value)
        {
            title=value;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if(OwnsWindow)SetWindowText(window,value);
#endif
        }
        public void Initialize(int side, bool alwaysOnTop, string title)
        {
            size = side; this.title=title;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (windowClass == 0)
            {
                var cls = new WNDCLASSEX { cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                    lpfnWndProc = procedure, hInstance = GetModuleHandle(null), lpszClassName = ClassName,
                    hCursor = LoadCursor(IntPtr.Zero, new IntPtr(32512)) };
                windowClass = RegisterClassEx(ref cls);
                if (windowClass == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            IntPtr screenDC = GetDC(IntPtr.Zero);
            memoryDC = CreateCompatibleDC(screenDC);
            var header = new BITMAPINFO { biSize = 40, biWidth = size, biHeight = size,
                biPlanes = 1, biBitCount = 32, biCompression = 0 };
            bitmap = CreateDIBSection(screenDC, ref header, 0, out bits, IntPtr.Zero, 0);
            ReleaseDC(IntPtr.Zero, screenDC);
            if (bitmap == IntPtr.Zero || memoryDC == IntPtr.Zero) throw new InvalidOperationException("Could not allocate overlay DIB.");
            oldBitmap = SelectObject(memoryDC, bitmap);

#endif
            buffer = new byte[size * size * 4];
            RefreshWorkArea(); Ready = true;
            SetDesktopVisible(!DesktopEnvironment.Covered && DesktopEnvironment.Available);
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            DesktopMouseInput.Add(this);
#endif
        }

        public void Present(Unity.Collections.NativeArray<byte> pixels, bool flipVertical)
        {
            if (!Ready || !desktopVisible || !DesktopAttached || pixels.Length != buffer.Length) return;
            pixels.CopyTo(buffer);
            if (flipVertical)
            {
                int row = size * 4;
                for (int a = 0, b = size - 1; a < b; a++, b--)
                    for (int k = 0; k < row; k++) { byte temp = buffer[a * row + k]; buffer[a * row + k] = buffer[b * row + k]; buffer[b * row + k] = temp; }
            }
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            Marshal.Copy(buffer, 0, bits, buffer.Length);
            var destination = new POINT { x = x, y = y };
            // Layered WS_CHILD destinations use the parent client coordinate space.
            ScreenToClient(desktopParent,ref destination);
            var source = new POINT(); var dimensions = new SIZE { cx = size, cy = size };
            var blend = new BLENDFUNCTION { BlendOp = 0, SourceConstantAlpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(Opacity) * 255), AlphaFormat = 1 };
            if (!UpdateLayeredWindow(window, IntPtr.Zero, ref destination, ref dimensions, memoryDC,
                ref source, 0, ref blend, 2))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "UpdateLayeredWindow failed.");
#endif
            hasPixels=true;
            if(desktopVisible && DesktopAttached && !IsWindowVisible(window)) ShowWindow(window,4); // SW_SHOWNOACTIVATE
            FramesPresented++;
        }

        public void Move(int left, int top)
        {
            x = left; y = top;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Ready && DesktopAttached)
            {
                var point=new POINT {x=x,y=y};ScreenToClient(desktopParent,ref point);
                SetWindowPos(window,IntPtr.Zero,point.x,point.y,0,0,0x0001|0x0004|0x0010);
            }
#endif
        }
        public void Resize(int side)
        {
            if(side==size)return;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            IntPtr screenDC=GetDC(IntPtr.Zero),newBits;
            var header=new BITMAPINFO {biSize=40,biWidth=side,biHeight=side,biPlanes=1,biBitCount=32};
            IntPtr replacement=CreateDIBSection(screenDC,ref header,0,out newBits,IntPtr.Zero,0);
            ReleaseDC(IntPtr.Zero,screenDC);
            if(replacement==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            SelectObject(memoryDC,replacement);DeleteObject(bitmap);bitmap=replacement;bits=newBits;
#endif
            hasPixels=false;size=side;buffer=new byte[side*side*4];
        }
        // Test-only direct invocation exercises the actual native callback branch.
        // It does not inject OS input or click another application.
        public void TestClick() { Dispatch(window, 0x0201, IntPtr.Zero, IntPtr.Zero); }
        public void SetDesktopVisible(bool value)
        {
            desktopVisible=value && DesktopEnvironment.Available;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            IntPtr host=DesktopEnvironment.Host;
            bool valid=OwnsWindow;
            if(valid && GetParent(window)!=host) {ShowWindow(window,0);owners.Remove(window);DestroyWindow(window);window=IntPtr.Zero;valid=false;hasPixels=false;}
            if(!valid && desktopVisible)
            {
                if(window!=IntPtr.Zero) owners.Remove(window);
                desktopParent=host;
                IntPtr previous=DesktopEnvironment.EnterHostDpiContext();
                try
                {
                    // WS_CHILD; no WS_VISIBLE until a valid alpha frame is available.
                    window=CreateWindowEx(0x08080080,ClassName,title,0x40000000,0,0,size,size,host,IntPtr.Zero,GetModuleHandle(null),IntPtr.Zero);
                }
                finally {DesktopEnvironment.RestoreDpiContext(previous);}
                if(window==IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Could not attach pet to desktop.");
                owners[window]=this;hasPixels=false;
                Move(x,y);
            }
            if(OwnsWindow)
            {
                if(!desktopVisible) ShowWindow(window,0);
                else if(hasPixels && !IsWindowVisible(window)) ShowWindow(window,4);
            }
#endif
        }
        public void RefreshWorkArea()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            RECT rect;
            if (!SystemParametersInfo(0x0030, 0, out rect, 0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            WorkArea = new Rect(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
            // The prototype targets the primary monitor, including its physical lower edge.
            ScreenArea = new Rect(0, 0, GetSystemMetrics(0), GetSystemMetrics(1));
#else
            ScreenArea = new Rect(0, 0, 1920, 1080); WorkArea = new Rect(0, 0, 1920, 1040);
#endif
        }
        public static void HideUnityWindow()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            uint processId = (uint)Process.GetCurrentProcess().Id;
            EnumWindows((handle, ignored) => {
                uint owner;GetWindowThreadProcessId(handle, out owner);
                if (owner == processId && IsWindowVisible(handle) && !owners.ContainsKey(handle)) ShowWindow(handle, 0);
                return true;
            }, IntPtr.Zero);
#endif
        }
        public static bool Shortcut(int key)
        {
            bool down = false;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            down = (GetAsyncKeyState(0x11) & 0x8000) != 0 && (GetAsyncKeyState(0x12) & 0x8000) != 0 && (GetAsyncKeyState(key) & 0x8000) != 0;
#endif
            if (!down) { pressed.Remove(key); return false; } return pressed.Add(key);
        }
        private static IntPtr Dispatch(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam)
        {
            WindowsOverlay owner; if (owners.TryGetValue(handle, out owner))
            {
                if (message == 0x0201) { Interlocked.Increment(ref owner.Clicks); return IntPtr.Zero; }
                if (message == 0x0021) return new IntPtr(3); // MA_NOACTIVATE
            }
            return DefWindowProc(handle, message, wParam, lParam);
        }
        public void Dispose()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            DesktopMouseInput.Remove(this);
#endif
            Ready = false;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            owners.Remove(window);
            if (OwnsWindow) DestroyWindow(window);
            if (memoryDC != IntPtr.Zero && oldBitmap != IntPtr.Zero) SelectObject(memoryDC, oldBitmap);
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (memoryDC != IntPtr.Zero) DeleteDC(memoryDC);
#endif
            window = bitmap = memoryDC = bits = IntPtr.Zero; buffer = null;
        }
        // Called on the dedicated input thread. Copy the buffer reference before
        // testing: disposal and presentation must never block the native mouse hook.
        internal bool HitPixel(int screenX,int screenY)
        {
            byte[] pixels=buffer;IntPtr handle=window;RECT rect;
            if(!Ready || !desktopVisible || !hasPixels || pixels==null || !IsWindowVisible(handle) || !GetWindowRect(handle,out rect)) return false;
            int column=screenX-rect.left,row=screenY-rect.top;
            return DesktopMouseInput.Opaque(pixels,size,column,row,Opacity);
        }
        internal IntPtr InputParent {get{return desktopParent;}}
        internal void RouteClick() {Interlocked.Increment(ref Clicks);}
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h,out RECT rect);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr WindowProc(IntPtr h, uint msg, IntPtr w, IntPtr l);
        private delegate bool EnumProc(IntPtr h, IntPtr value);
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] private struct WNDCLASSEX
        {
            public uint cbSize, style; public WindowProc lpfnWndProc; public int cbClsExtra, cbWndExtra;
            public IntPtr hInstance, hIcon, hCursor, hbrBackground;
            public string lpszMenuName, lpszClassName; public IntPtr hIconSm;
        }
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential, Pack=1)] private struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)] private struct BITMAPINFO
        {
            public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount;
            public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant;
        }
        [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern ushort RegisterClassEx(ref WNDCLASSEX value);
        [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr CreateWindowEx(uint ex, string cls, string title, uint style,int x,int y,int w,int h,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr DefWindowProc(IntPtr h,uint msg,IntPtr w,IntPtr l);
        [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
        [DllImport("user32.dll", EntryPoint="SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr value);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string value);
        [DllImport("user32.dll")] private static extern IntPtr LoadCursor(IntPtr instance, IntPtr id);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr h);
        [DllImport("user32.dll",SetLastError=true)] private static extern bool UpdateLayeredWindow(IntPtr h,IntPtr dc,ref POINT dest,ref SIZE size,IntPtr sourceDC,ref POINT source,uint key,ref BLENDFUNCTION blend,uint flags);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr h);
        [DllImport("gdi32.dll",SetLastError=true)] private static extern IntPtr CreateDIBSection(IntPtr dc,ref BITMAPINFO info,uint usage,out IntPtr bits,IntPtr section,uint offset);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc,IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr value);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern bool SetWindowText(IntPtr h,string text);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h,int command);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback,IntPtr value);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h,out uint processId);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr h);
        [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr h,ref POINT point);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int value);
        [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint action,uint parameter,out RECT rect,uint update);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    }
}
