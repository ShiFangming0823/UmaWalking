using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using UmaDesktop;

// Standalone diagnostic harness. The three production source files are compiled
// unchanged; their host property is overridden ONLY inside this test process.
namespace UnityEngine {
 public struct Rect {public Rect(float x,float y,float w,float h){} }
 public static class Mathf {public static float Clamp01(float v){return Math.Max(0,Math.Min(1,v));}public static int RoundToInt(float v){return (int)Math.Round(v);} }
}
namespace Unity.Collections {
 public struct NativeArray<T>{private T[] v;public NativeArray(T[] v){this.v=v;}public int Length{get{return v.Length;}}public void CopyTo(T[] target){Array.Copy(v,target,v.Length);} }
}
internal static class DesktopHostTests
{
 [StructLayout(LayoutKind.Sequential)] struct POINT {public int x,y;}
 [StructLayout(LayoutKind.Sequential)] struct RECT {public int l,t,r,b;}
 [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h,uint f);
 [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
 [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out RECT r);
 [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h,out RECT r);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr p,IntPtr after,string c,string title);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll")] static extern IntPtr GetWindowDpiAwarenessContext(IntPtr h);
 [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
 static readonly List<object> results=new List<object>();static int checks;
 static string Class(IntPtr h){var s=new StringBuilder(256);GetClassName(h,s,256);return s.ToString();}
 static object Info(IntPtr h){RECT r;GetWindowRect(h,out r);return new{handle=h.ToInt64(),cls=Class(h),visible=IsWindowVisible(h),style="0x"+(GetWindowLongPtr(h,-16).ToInt64()&0xffffffff).ToString("X"),exStyle="0x"+(GetWindowLongPtr(h,-20).ToInt64()&0xffffffff).ToString("X"),rect=new[]{r.l,r.t,r.r,r.b},dpi=GetDpiForWindow(h),dpiContext=GetWindowDpiAwarenessContext(h).ToInt64()};}
 static void Check(bool value,string label){results.Add(new{Passed=value,Check=label});checks++;Console.WriteLine((value?"PASS ":"FAIL ")+label);if(!value)throw new Exception(label);}
 static IntPtr fixtureRoot;
 static void SetHost(IntPtr h){typeof(DesktopEnvironment).GetProperty("ChildHost").SetValue(null,h,null);typeof(DesktopEnvironment).GetProperty("Host").SetValue(null,fixtureRoot,null);typeof(DesktopEnvironment).GetProperty("Available").SetValue(null,h!=IntPtr.Zero,null);typeof(DesktopEnvironment).GetProperty("Covered").SetValue(null,false,null);}
 static IntPtr Callback(int x,int y,int message){IntPtr p=Marshal.AllocHGlobal(32);try{Marshal.StructureToPtr(new POINT{x=x,y=y},p,false);return(IntPtr)typeof(DesktopMouseInput).GetMethod("OnMouse",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{0,new IntPtr(message),p});}finally{Marshal.FreeHGlobal(p);}}
 static byte[] Pixels(byte r,byte g,byte b){var bytes=new byte[128*128*4];for(int y=12;y<116;y++)for(int x=12;x<116;x++){int at=((127-y)*128+x)*4;bytes[at]=b;bytes[at+1]=g;bytes[at+2]=r;bytes[at+3]=255;}return bytes;}
 static void Exercise(Form root,Control parent,string label,Color color)
 {
  SetHost(parent.Handle);var point=parent.PointToScreen(new Point(25,25));var pixels=Pixels(color.R,color.G,color.B);
  using(var overlay=new WindowsOverlay())
  {
   overlay.Initialize(128,false,"Issue1 isolated "+label);overlay.Move(point.X,point.Y);overlay.Present(new Unity.Collections.NativeArray<byte>(pixels),false);Application.DoEvents();
   RECT r;GetWindowRect(overlay.Handle,out r);
   Check(overlay.DesktopAttached&&GetParent(overlay.Handle)==parent.Handle,label+": layered child attaches to chosen parent");
   Check(GetAncestor(overlay.Handle,2)==root.Handle,label+": keeps the same top-level root");
   Check((GetWindowLongPtr(overlay.Handle,-20).ToInt64()&8)==0,label+": does not become topmost");
   Check(Math.Abs(r.l-point.X)<=1&&Math.Abs(r.t-point.Y)<=1,label+": physical coordinates remain correct at nonzero parent origin");
   Check(overlay.FramesPresented>0&&overlay.DesktopVisible,label+": API presentation succeeds (not proof of compositor visibility)");
   DesktopMouseInput.Enabled=true;
   Callback(point.X+30,point.Y+30,0x201);Callback(point.X+30,point.Y+30,0x202);
   Check(overlay.TakeClicks()==1,label+": production pixel-input callback reaches the child");overlay.TakePointerEvents();
   Callback(point.X+30,point.Y+30,0x201);Callback(point.X+95,point.Y+95,0x200);Callback(point.X+105,point.Y+105,0x202);
   var events=overlay.TakePointerEvents();Check(events.Any(e=>e.Kind==DesktopPointerKind.Move&&e.Dragged)&&events.Any(e=>e.Kind==DesktopPointerKind.Up)&&overlay.TakeClicks()==0,label+": drag queue survives nested parenting without an extra click");
   overlay.SetDesktopVisible(false);Check(!IsWindowVisible(overlay.Handle),label+": hiding removes the child");
   overlay.SetDesktopVisible(true);Check(IsWindowVisible(overlay.Handle),label+": resume shows previously submitted alpha frame");
  }
 }
 [STAThread] static int Main(string[] args)
 {
  string dir=args[0];Directory.CreateDirectory(dir);object shell=null;bool passed=false;string error=null;
  try
  {
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   DesktopEnvironment.Poll();IntPtr root=DesktopEnvironment.Host;IntPtr view=FindWindowEx(root,IntPtr.Zero,"SHELLDLL_DefView",null);
   shell=new{Root=Info(root),DefView=Info(view),RootEqualsDefView=root==view,RootNeedsNoRedirectionBitmap=(GetWindowLongPtr(root,-20).ToInt64()&0x200000)!=0,DesktopCompositorVisibilityTested=false};
   Console.WriteLine("SHELL "+new JavaScriptSerializer().Serialize(shell));
   using(var host=new Form{Text="UmaWalking Issue 1 - isolated host diagnostic",Width=900,Height=570,StartPosition=FormStartPosition.CenterScreen,BackColor=Color.White})
   {
    var title=new Label{Text="Issue #1: independent native-window experiment",Font=new Font("Segoe UI",16,FontStyle.Bold),AutoSize=true,Location=new Point(20,15)};host.Controls.Add(title);
    var note=new Label{Text="Production host-selection and overlay code compiled into this fixture. No Explorer restart or desktop settings changes.",AutoSize=true,Location=new Point(20,55)};host.Controls.Add(note);
    var parent=new Panel{Left=210,Top=100,Width=630,Height=260,BackColor=Color.FromArgb(230,237,245)};host.Controls.Add(parent);
    parent.Controls.Add(new Label{Text="Nested host fixture (simulates ChildHost hierarchy only)",AutoSize=true,Location=new Point(175,15)});
    host.Show();host.BringToFront();Application.DoEvents();
    fixtureRoot=host.Handle;
    Check(DesktopEnvironment.SelectHostMode("Auto",0,true)=="Root","Auto retains redirected desktop root");
    Check(DesktopEnvironment.SelectHostMode("Auto",0x200000,true)=="IconView","Auto selects icon view for unredirected desktop");
    Check(DesktopEnvironment.SelectHostMode("Root",0x200000,true)=="Root","Root override wins over heuristic");
    Check(DesktopEnvironment.SelectHostMode("IconView",0,true)=="IconView","IconView override works on legacy shell");
    Check(DesktopEnvironment.SelectHostMode("Auto",0x200000,false)=="Root","Auto requires visible icon view");
    Exercise(host,host,"direct root",Color.Red);Exercise(host,parent,"nested child host",Color.Blue);
    SetHost(parent.Handle);
    using(var overlay=new WindowsOverlay())
    {
     overlay.Initialize(128,false,"Issue1 lifecycle fixture");var p=parent.PointToScreen(new Point(25,25));overlay.Move(p.X,p.Y);overlay.Present(new Unity.Collections.NativeArray<byte>(Pixels(0,150,255)),false);
     IntPtr old=overlay.Handle;parent.Dispose();Application.DoEvents();
     Check(!IsWindow(old),"Destroying only a nested host also destroys its foreign child window");
     var replacement=new Panel{Left=210,Top=100,Width=630,Height=260,BackColor=Color.FromArgb(230,237,245)};host.Controls.Add(replacement);SetHost(replacement.Handle);overlay.SetDesktopVisible(true);overlay.Move(p.X,p.Y);overlay.Present(new Unity.Collections.NativeArray<byte>(Pixels(0,150,255)),false);Application.DoEvents();
     Check(overlay.DesktopAttached&&GetParent(overlay.Handle)==replacement.Handle,"Existing recreation path recovers when the nested host changes");
     Check(GetAncestor(overlay.Handle,2)==host.Handle,"Nested host replacement leaves the root identity unchanged");
     SetHost(IntPtr.Zero);overlay.SetDesktopVisible(false);Check(!IsWindowVisible(overlay.Handle),"Missing desktop host does not create a visible top-level fallback");
    }
    var complete=new Label{Text=checks+" checks passed. Actual Win11 24H2 composition is NOT emulated.",AutoSize=true,Font=new Font("Segoe UI",12,FontStyle.Bold),Location=new Point(20,390)};host.Controls.Add(complete);
    var caveat=new Label{Text="Layered-window APIs succeeding does not prove pixels reach the desktop.\nThis machine runs Windows 10; the reporter's 24H2 visual failure remains unverified here.",AutoSize=true,Location=new Point(20,430)};host.Controls.Add(caveat);passed=true;
    File.WriteAllText(Path.Combine(dir,"native-host-report.json"),new JavaScriptSerializer().Serialize(new{Passed=passed,Checks=checks,Shell=shell,results=results,PhysicalMouseInputInjected=false,ExplorerRestarted=false,FixtureOnly=true,Win11FailureReproduced=false}));
    var stop=DateTime.UtcNow.AddSeconds(0);while(DateTime.UtcNow<stop&&!host.IsDisposed){Application.DoEvents();Thread.Sleep(20);}
   }
  }
  catch(Exception ex){error=ex.ToString();Console.WriteLine(error);File.WriteAllText(Path.Combine(dir,"native-host-error.txt"),error);}
  finally{DesktopEnvironment.Shutdown();}
  return passed?0:1;
 }
}
