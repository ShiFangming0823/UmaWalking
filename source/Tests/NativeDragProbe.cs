// Manual/system input fixture. Uses the production hook and native overlay;
// it never injects mouse input. Drive its visible window through Computer Use.
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using UmaDesktop;

class NativeDragProbe : Form
{
    [StructLayout(LayoutKind.Sequential)] struct Point {public int x,y;}
    [DllImport("user32.dll")] static extern bool GetCursorPos(out Point p);
    private readonly WindowsOverlay overlay=new WindowsOverlay();
    private readonly Timer clock=new Timer {Interval=16};
    private readonly Label status=new Label {Left=25,Top=35,Width=950,Height=80,Font=new Font("Microsoft YaHei UI",13)};
    private readonly string root;
    private int left,top,grabX,startLeft,downX,downY,upX,upY,upLeft,steps;
    private bool held,dragging,finished;
    private DateTime released;
    [STAThread] static void Main(string[] args)
    {Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new NativeDragProbe(args.Length>0?args[0]:AppDomain.CurrentDomain.BaseDirectory));}
    public NativeDragProbe(string root)
    {
        this.root=root;Directory.CreateDirectory(root);Text="UmaDesktop · 系统拖拽验证";ClientSize=new Size(1000,480);StartPosition=FormStartPosition.CenterScreen;
        BackColor=Color.White;FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;
        status.Text="按住左侧蓝色圆形，沿横线拖到右侧虚线框，再松开。\n验证系统指针正常移动、松开后位置不回弹。";Controls.Add(status);
        Shown+=(s,e)=>StartProbe();clock.Tick+=(s,e)=>TickProbe();FormClosed+=(s,e)=>{clock.Stop();overlay.Dispose();clock.Dispose();};
    }
    private void StartProbe()
    {
        typeof(DesktopEnvironment).GetProperty("Host").SetValue(null,Handle,null);
        typeof(DesktopEnvironment).GetProperty("Available").SetValue(null,true,null);
        var p=PointToScreen(new System.Drawing.Point(140,170));left=startLeft=p.X;top=p.Y;
        overlay.Initialize(128,false,"UmaDesktop native drag probe model");overlay.Move(left,top);
        var pixels=new byte[128*128*4];
        for(int y=0;y<128;y++)for(int x=0;x<128;x++)if((x-64)*(x-64)+(y-64)*(y-64)<=48*48)
        {int i=((127-y)*128+x)*4;pixels[i]=230;pixels[i+1]=150;pixels[i+2]=35;pixels[i+3]=255;}
        overlay.Present(new Unity.Collections.NativeArray<byte>(pixels),false);WindowsOverlay.InputEnabled=true;clock.Start();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);using(var pen=new Pen(Color.LightGray,2))e.Graphics.DrawLine(pen,70,298,930,298);
        using(var pen=new Pen(Color.OrangeRed,3)){pen.DashStyle=System.Drawing.Drawing2D.DashStyle.Dash;e.Graphics.DrawRectangle(pen,700,170,128,128);}
        e.Graphics.DrawString("起点",Font,Brushes.Black,170,325);e.Graphics.DrawString("拖到这里后松开",Font,Brushes.Black,695,325);
    }
    private void TickProbe()
    {
        if(File.Exists(Path.Combine(root,"close-probe"))){Close();return;}
        foreach(var e in overlay.TakePointerEvents())
        {
            if(e.Kind==DesktopPointerKind.Down){held=true;dragging=false;grabX=left-e.X;downX=e.X;downY=e.Y;}
            else if(e.Kind==DesktopPointerKind.Move && held && e.Dragged)
            {
                dragging=true;steps++;int min=PointToScreen(new System.Drawing.Point(40,0)).X,max=PointToScreen(new System.Drawing.Point(830,0)).X;
                left=Math.Max(min,Math.Min(max,e.X+grabX));overlay.Move(left,top);
            }
            else if(e.Kind==DesktopPointerKind.Up)
            {
                held=false;if(!dragging)continue;Point cursor;GetCursorPos(out cursor);upX=cursor.x;upY=cursor.y;upLeft=left;released=DateTime.UtcNow;finished=true;
            }
        }
        if(finished && (DateTime.UtcNow-released).TotalMilliseconds>400)
        {
            int cursorDistance=upX-downX,modelDistance=upLeft-startLeft;
            bool pass=cursorDistance>350 && modelDistance>350 && Math.Abs(cursorDistance-modelDistance)<3 && left==upLeft && overlay.TakeClicks()==0;
            status.Text=(pass?"通过":"未通过")+"：系统指针移动 "+cursorDistance+" 像素，模型移动 "+modelDistance+" 像素。\n松开后位置"+(left==upLeft?"保持稳定。":"发生回弹。");
            var result=new {Passed=pass,SystemMouseEventChainVerified=true,CursorMovementPixels=cursorDistance,ModelMovementPixels=modelDistance,ModelStableAfterRelease=left==upLeft,MovementEvents=steps,CursorDown=new[]{downX,downY},CursorUp=new[]{upX,upY},SystemInputInjectedByFixture=false};
            File.WriteAllText(Path.Combine(root,"system-drag-report.json"),new JavaScriptSerializer().Serialize(result));finished=false;
        }
    }
}
