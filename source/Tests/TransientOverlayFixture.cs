// Native compatibility fixture. Compile into an isolated test directory under
// the process basename being exercised; never replace a system/PowerToys binary.
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
class TransientOverlayFixture
{
    private delegate IntPtr Proc(IntPtr h,uint m,IntPtr w,IntPtr l);
    private static readonly Proc callback=DefWindowProc;
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct Class
    {public uint cbSize,style;public Proc proc;public int a,b;public IntPtr instance,icon,cursor,brush;public string menu,name;public IntPtr smallIcon;}
    [StructLayout(LayoutKind.Sequential)] private struct Point {public int x,y;}
    [StructLayout(LayoutKind.Sequential)] private struct Msg {public IntPtr h;public uint m;public UIntPtr w;public IntPtr l;public uint time;public Point point;public uint extra;}
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern ushort RegisterClassEx(ref Class c);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr CreateWindowEx(uint ex,string cls,string name,uint style,int x,int y,int w,int h,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr DefWindowProc(IntPtr h,uint m,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Msg m,IntPtr h,uint min,uint max,uint flags);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Msg m);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref Msg m);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int id);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr c);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string n);
    [STAThread] static int Main(string[] args)
    {
        if(args.Length<3) return 2;
        SetThreadDpiAwarenessContext(new IntPtr(-4));string cls=args[0],ready=args[1];int milliseconds=int.Parse(args[2]);
        var c=new Class {cbSize=(uint)Marshal.SizeOf(typeof(Class)),proc=callback,instance=GetModuleHandle(null),name=cls};
        if(RegisterClassEx(ref c)==0) return 3;
        IntPtr window=CreateWindowEx(0x08000088,cls,cls=="XamlExplorerHostIslandWindow"?"Task Switching":"UmaDesktop compatibility fixture",0x80000000,0,0,GetSystemMetrics(0),GetSystemMetrics(1),IntPtr.Zero,IntPtr.Zero,c.instance,IntPtr.Zero);
        if(window==IntPtr.Zero) return 4;
        try
        {
            SetWindowPos(window,new IntPtr(-1),0,0,GetSystemMetrics(0),GetSystemMetrics(1),0x10|0x40);
            File.WriteAllText(ready,window.ToInt64().ToString());var timer=Stopwatch.StartNew();
            while(timer.ElapsedMilliseconds<milliseconds)
            {Msg m;while(PeekMessage(out m,IntPtr.Zero,0,0,1)){TranslateMessage(ref m);DispatchMessage(ref m);}Thread.Sleep(10);}
        }
        finally {DestroyWindow(window);}
        return 0;
    }
}
