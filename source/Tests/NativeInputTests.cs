using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using UmaDesktop;
namespace UnityEngine {
 public struct Rect {public Rect(float x,float y,float w,float h){} }
 public static class Mathf { public static float Clamp01(float n){return Math.Max(0,Math.Min(1,n));}public static int RoundToInt(float n){return (int)Math.Round(n);} }
}
namespace Unity.Collections {
 public struct NativeArray<T> {private T[] values;public NativeArray(T[] v){values=v;}public int Length {get{return values.Length;}}public void CopyTo(T[] to){Array.Copy(values,to,values.Length);} }
}
class NativeInputTests {
 [StructLayout(LayoutKind.Sequential)] struct Point {public int x,y;}
 [StructLayout(LayoutKind.Sequential)] struct Rect {public int l,t,r,b;}
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);
 [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h,uint flags);
 [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h,uint flags);
 [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr c);
 static int checks;
 static void Check(bool ok,string label) {Console.WriteLine((ok?"PASS ":"FAIL ")+label);if(!ok)throw new Exception(label);checks++;}
 static IntPtr Route(int x,int y,int msg=0x201) {
  IntPtr data=Marshal.AllocHGlobal(32);
  try {Marshal.StructureToPtr(new Point{x=x,y=y},data,false);return (IntPtr)typeof(DesktopMouseInput).GetMethod("OnMouse",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{0,new IntPtr(msg),data});}
  finally{Marshal.FreeHGlobal(data);}
 }
 static void DesktopHost(IntPtr host) {
  typeof(DesktopEnvironment).GetProperty("Host").SetValue(null,host,null);
  typeof(DesktopEnvironment).GetProperty("Available").SetValue(null,true,null);
 }
 [STAThread] static void Main() {try{Run();}catch(Exception e){Console.WriteLine("EXCEPTION "+e.GetType().FullName+" "+e.Message+" STACK "+e.StackTrace);Environment.Exit(1);}} static void Run() {
  SetThreadDpiAwarenessContext(new IntPtr(-4));
  var pixels=new byte[128*128*4];
  for(int y=16;y<64;y++)for(int x=16;x<80;x++){int p=((127-y)*128+x)*4;pixels[p]=255;pixels[p+3]=255;}
  Check(DesktopMouseInput.Opaque(pixels,128,20,20,1),"Top-down mouse coordinate maps to bottom-up DIB alpha");
  Check(!DesktopMouseInput.Opaque(pixels,128,20,100,1),"Transparent pixel passes through");
  Check(!DesktopMouseInput.Opaque(pixels,128,20,20,0.01f),"Invisible fade does not capture input");
  Check(!DesktopMouseInput.Opaque(pixels,128,-1,20,1) && !DesktopMouseInput.Opaque(pixels,128,128,20,1),"Coordinates outside the pet canvas pass through");
  using(var host=new Form{Text="UmaDesktop native input self-test",Width=360,Height=270,StartPosition=FormStartPosition.Manual,Left=80,Top=80,ShowInTaskbar=false}) {
   host.Show();host.BringToFront();Application.DoEvents();DesktopHost(host.Handle);
   var start=host.PointToScreen(new System.Drawing.Point(20,20));
   using(var a=new WindowsOverlay())using(var b=new WindowsOverlay()) {
    a.Initialize(128,false,"UmaDesktop test A");a.Move(start.X,start.Y);a.Present(new Unity.Collections.NativeArray<byte>(pixels),false);
    b.Initialize(128,false,"UmaDesktop test B");b.Move(start.X,start.Y);b.Present(new Unity.Collections.NativeArray<byte>(pixels),false);
    Application.DoEvents();DesktopMouseInput.Enabled=true;
    Check(DesktopMouseInput.Installed,"Real WH_MOUSE_LL hook installed on dedicated message-pump thread");
    Check(GetAncestor(WindowFromPoint(new Point{x=start.X+20,y=start.Y+20}),2)==host.Handle,"Native point belongs to own desktop-host fixture");
    Rect rect;GetWindowRect(a.Handle,out rect);Check(rect.l==start.X && rect.t==start.Y,"Layered child remains at the requested physical screen position");
    IntPtr first=GetWindow(host.Handle,5);WindowsOverlay front=first==a.Handle?a:b,back=front==a?b:a;
    Check(Route(start.X+20,start.Y+20)==new IntPtr(1),"Production hook routes opaque native-window hit despite child-window input routing");
    Check(front.TakeClicks()==0 && back.TakeClicks()==0 && front.TakePointerEvents()[0].Kind==DesktopPointerKind.Down,"Press holds the uppermost model without triggering a special");
    Check(Route(start.X+20,start.Y+20,0x202)==new IntPtr(1),"Matching mouse-up is consumed to avoid desktop icon activation");
    Check(front.TakeClicks()==1 && back.TakeClicks()==0,"Release of a simple click triggers exactly one uppermost-model special");front.TakePointerEvents();
    Route(start.X+20,start.Y+20);Check(Route(start.X+22,start.Y+22,0x200)==IntPtr.Zero,"Small drag movement is forwarded so the system cursor can move");
    Check(front.TakePointerEvents().Length==1,"Small pointer jitter remains a click");
    Check(Route(start.X+100,start.Y+100,0x200)==IntPtr.Zero,"Large drag movement is forwarded instead of freezing the system cursor");var drag=front.TakePointerEvents();
    Check(drag.Length==1 && drag[0].Kind==DesktopPointerKind.Move && drag[0].Dragged,"Drag remains captured outside opaque model pixels");
    Route(start.X+110,start.Y+110,0x202);var release=front.TakePointerEvents();
    Check(release.Length==2 && release[1].Kind==DesktopPointerKind.Up && front.TakeClicks()==0,"Drag release never triggers a special");
    Route(start.X+20,start.Y+20);front.TakePointerEvents();DesktopMouseInput.CancelGesture();
    Check(front.TakePointerEvents()[0].Kind==DesktopPointerKind.Cancel,"Cancel releases a held model");Route(start.X+20,start.Y+20,0x202);
    Route(start.X+20,start.Y+20);front.TakePointerEvents();WindowsOverlay.InputEnabled=false;
    Check(front.TakePointerEvents()[0].Kind==DesktopPointerKind.Cancel,"Global pause cancels the captured gesture without leaving a stuck drag");
    Route(start.X+20,start.Y+20,0x202);Check(front.TakeClicks()==0,"Canceled gesture release does not trigger a special");WindowsOverlay.InputEnabled=true;
    Route(start.X+110,start.Y+110);Check(a.TakeClicks()==0 && b.TakeClicks()==0,"Transparent canvas does not trigger either actor");
    DesktopMouseInput.Enabled=false;Route(start.X+20,start.Y+20);Check(a.TakeClicks()==0 && b.TakeClicks()==0,"Pause gates the pixel input route");DesktopMouseInput.Enabled=true;
    using(var cover=new Form{Text="UmaDesktop coverage self-test",StartPosition=FormStartPosition.Manual,Left=start.X,Top=start.Y,Width=200,Height=200,ShowInTaskbar=false}) {
     cover.Show();cover.BringToFront();Application.DoEvents();
     Check(GetAncestor(WindowFromPoint(new Point{x=start.X+20,y=start.Y+20}),2)==cover.Handle,"Own foreground cover actually occludes the model test point");
     Route(start.X+20,start.Y+20);Check(a.TakeClicks()==0 && b.TakeClicks()==0,"A foreground app window blocks pet clicks even when input is enabled");
    }
    Thread.Sleep(1200);Check(DesktopMouseInput.Installed,"Model-load stall does not block the independent input thread");
    b.SetDesktopVisible(false);a.Resize(192);var large=new byte[192*192*4];large[((191-24)*192+24)*4+3]=255;a.Present(new Unity.Collections.NativeArray<byte>(large),false);
    GetWindowRect(a.Handle,out rect);Check(rect.r-rect.l==192 && rect.b-rect.t==192,"Resize recreates DIB and physical window dimensions");
    Check(a.HitPixel(rect.l+24,rect.t+24),"Resized model alpha uses the new row stride");
    Route(rect.l+24,rect.t+24);Route(rect.l+24,rect.t+24,0x202);Check(a.TakeClicks()==1,"Pixel click routing works after resizing");
    a.SetDesktopVisible(false);b.SetDesktopVisible(false);Route(start.X+20,start.Y+20);Check(a.TakeClicks()==0 && b.TakeClicks()==0,"Hidden desktop pets cannot capture clicks");
   }
   Check(!DesktopMouseInput.Installed,"Last actor disposal unhooks the native input thread");
  }
  Console.WriteLine("Native input checks passed: "+checks+"; actual physical mouse delivery is not simulated by this test.");
 }
}
