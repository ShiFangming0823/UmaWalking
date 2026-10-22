using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using UmaDesktop;

internal sealed class GroundEditor : Form
{
    internal DesktopGroundConfig Config;
    private readonly Form tools=new Form();
    private readonly ComboBox floors=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=115};
    private readonly Label hint=new Label {AutoSize=true,MaximumSize=new Size(730,0),ForeColor=Color.White,Padding=new Padding(4)};
    private readonly Button save=new Button {Text=UiText.Get("保存并应用"),AutoSize=true,BackColor=Color.WhiteSmoke,ForeColor=Color.Black};
    private readonly HashSet<GroundSurface> entrances=new HashSet<GroundSurface>(),exits=new HashSet<GroundSurface>();
    private readonly List<GroundPoint> pending=new List<GroundPoint>();
    private string operation="Corners";
    private int selected,drag=-1,gateEdge=-1;
    private float gateStart;
    private bool syncing,closing;
    internal GroundEditor(DesktopGroundConfig initial,string mode)
    {
        Config=new JavaScriptSerializer().Deserialize<DesktopGroundConfig>(new JavaScriptSerializer().Serialize(initial??new DesktopGroundConfig()));
        Config.mode=mode;
        if(Config.line==null)Config.line=new GroundPoint[0];if(Config.surfaces==null)Config.surfaces=new GroundSurface[0];
        foreach(var surface in Config.surfaces){entrances.Add(surface);exits.Add(surface);}
        if(mode=="Planes" && Config.surfaces.Length==0)Config.surfaces=new[]{new GroundSurface()};
        Bounds=Screen.PrimaryScreen.Bounds;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;
        BackColor=Color.FromArgb(12,20,31);Opacity=.48;TopMost=true;ShowInTaskbar=false;DoubleBuffered=true;KeyPreview=true;Cursor=Cursors.Cross;
        Text=UiText.Get("UmaDesktop 地面绘制画布");
        tools.Text=UiText.Get("UmaDesktop · 地面编辑（可拖动此工具条）");tools.Font=new Font("Microsoft YaHei UI",10);
        tools.FormBorderStyle=FormBorderStyle.FixedToolWindow;tools.TopMost=true;tools.ShowInTaskbar=false;tools.BackColor=Color.FromArgb(28,38,53);
        tools.ForeColor=Color.White;tools.AutoSize=true;tools.AutoSizeMode=AutoSizeMode.GrowAndShrink;tools.StartPosition=FormStartPosition.Manual;
        tools.Location=new Point(Bounds.Left+30,Bounds.Top+30);tools.KeyPreview=true;
        var layout=new FlowLayoutPanel {AutoSize=true,FlowDirection=FlowDirection.TopDown,Padding=new Padding(10)};tools.Controls.Add(layout);
        var row=new FlowLayoutPanel {AutoSize=true,MaximumSize=new Size(1100,0)};layout.Controls.Add(row);
        if(mode=="Planes")
        {
            row.Controls.Add(floors);row.Controls.Add(Make(UiText.Get("添加地面"),AddFloor));row.Controls.Add(Make(UiText.Get("删除当前地面"),DeleteFloor));
        }
        row.Controls.Add(Make(mode=="Line"?UiText.Get("重画直线"):UiText.Get("重画四点"),()=>Begin("Corners")));
        if(mode=="Planes") {row.Controls.Add(Make(UiText.Get("设置入口"),()=>Begin("Entrance")));row.Controls.Add(Make(UiText.Get("设置出口"),()=>Begin("Exit")));}
        save.Click+=(s,e)=>Commit();row.Controls.Add(save);row.Controls.Add(Make(UiText.Get("取消 / Esc"),()=>{DialogResult=DialogResult.Cancel;Close();}));layout.Controls.Add(hint);
        floors.SelectedIndexChanged+=(s,e)=>{if(syncing)return;selected=Math.Max(0,floors.SelectedIndex);pending.Clear();gateEdge=-1;RefreshHint();Invalidate();};
        Shown+=(s,e)=>{tools.Show(this);tools.Activate();RefreshFloors();RefreshHint();};
        tools.FormClosing+=(s,e)=>{if(!closing){DialogResult=DialogResult.Cancel;Close();}};
        FormClosed+=(s,e)=>{closing=true;tools.Close();tools.Dispose();};
        KeyDown+=KeysPressed;tools.KeyDown+=KeysPressed;
        MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Right){pending.Clear();gateEdge=-1;RefreshHint();Invalidate();return;}if(e.Button!=MouseButtons.Left)return;ClickPoint(e.X,e.Y);};
        MouseMove+=(s,e)=>{if(drag<0 || e.Button!=MouseButtons.Left)return;MoveHandle(e.X,e.Y);};
        MouseUp+=(s,e)=>drag=-1;
        Bounds=Screen.PrimaryScreen.Bounds;
    }
    private Button Make(string text,Action action)
    {var b=new Button {Text=text,AutoSize=true,BackColor=Color.WhiteSmoke,ForeColor=Color.Black,Margin=new Padding(3),Padding=new Padding(4)};b.Click+=(s,e)=>action();return b;}
    private void KeysPressed(object sender,KeyEventArgs e)
    {if(e.KeyCode==Keys.Escape){DialogResult=DialogResult.Cancel;Close();}else if(e.KeyCode==Keys.Enter)Commit();}
    private GroundSurface Current {get{return Config.surfaces[Math.Max(0,Math.Min(selected,Config.surfaces.Length-1))];}}
    private void RefreshFloors()
    {
        syncing=true;floors.Items.Clear();for(int i=0;i<Config.surfaces.Length;i++)floors.Items.Add(UiText.Get("地面 ")+(i+1));
        if(floors.Items.Count>0)floors.SelectedIndex=Math.Min(selected,floors.Items.Count-1);syncing=false;
    }
    internal void AddFloor()
    {
        if(Config.surfaces.Length>=3){hint.Text=UiText.Get("最多可以绘制三块地面。");return;}
        Config.surfaces=Config.surfaces.Concat(new[]{new GroundSurface()}).ToArray();selected=Config.surfaces.Length-1;RefreshFloors();Begin("Corners");
    }
    private void DeleteFloor()
    {
        if(Config.surfaces.Length<=1){hint.Text=UiText.Get("至少保留一块地面；可使用“重画四点”重新绘制。");return;}
        Config.surfaces=Config.surfaces.Where((x,i)=>i!=selected).ToArray();selected=Math.Min(selected,Config.surfaces.Length-1);RefreshFloors();Begin("Corners");
    }
    private void Begin(string value){operation=value;pending.Clear();gateEdge=-1;RefreshHint();Invalidate();}
    private GroundPoint Normalized(int x,int y){return new GroundPoint(Math.Max(0,Math.Min(1,(float)x/ClientSize.Width)),Math.Max(0,Math.Min(1,(float)y/ClientSize.Height)));}
    private PointF Pixel(GroundPoint p){return new PointF(p.x*ClientSize.Width,p.y*ClientSize.Height);}
    internal void ClickPoint(int x,int y)
    {
        var point=Normalized(x,y);
        GroundPoint[] points=Config.mode=="Line"?Config.line:Current.points;
        if(operation=="Corners" && pending.Count==0)
            for(int i=0;i<points.Length;i++){PointF p=Pixel(points[i]);if(Math.Abs(p.X-x)<18&&Math.Abs(p.Y-y)<18){drag=i;return;}}
        if(operation=="Corners")
        {
            pending.Add(point);int count=Config.mode=="Line"?2:4;
            if(pending.Count==count)
            {
                if(Config.mode=="Line")Config.line=pending.ToArray();
                else {Current.points=pending.ToArray();entrances.Remove(Current);exits.Remove(Current);operation="Entrance";}
                pending.Clear();
            }
        }
        else
        {
            if(Current.points.Length!=4){hint.Text=UiText.Get("请先绘制四个角点。");return;}
            int edge;float fraction;
            if(!NearestEdge(x,y,out edge,out fraction)){hint.Text=UiText.Get("请点击地面边缘，入口和出口都需要在同一条边上点两次。");return;}
            if(gateEdge<0){gateEdge=edge;gateStart=fraction;}
            else
            {
                if(edge!=gateEdge){hint.Text=UiText.Get("第二个端点需要位于刚才那条边上。右键可撤销第一个端点。");return;}
                float start=Math.Min(gateStart,fraction),end=Math.Max(gateStart,fraction);
                if(end-start<.025f){hint.Text=UiText.Get("通道太窄，请把两个端点拉开一些。");return;}
                var gate=new GroundGate {edge=edge,start=start,end=end};gateEdge=-1;
                if(operation=="Entrance"){Current.entrance=gate;entrances.Add(Current);operation="Exit";}
                else {Current.exit=gate;exits.Add(Current);operation="Corners";}
            }
        }
        RefreshHint();Invalidate();
    }
    internal void MoveHandle(int x,int y)
    {var pts=Config.mode=="Line"?Config.line:Current.points;if(drag>=0&&drag<pts.Length)pts[drag]=Normalized(x,y);RefreshHint();Invalidate();}
    private bool NearestEdge(int x,int y,out int edge,out float fraction)
    {
        edge=-1;fraction=0;double closest=30*30;
        for(int i=0;i<4;i++)
        {
            PointF a=Pixel(Current.points[i]),b=Pixel(Current.points[(i+1)%4]);double dx=b.X-a.X,dy=b.Y-a.Y;
            double length=dx*dx+dy*dy;if(length<1)continue;
            double t=Math.Max(0,Math.Min(1,((x-a.X)*dx+(y-a.Y)*dy)/length));double d=(x-a.X-dx*t)*(x-a.X-dx*t)+(y-a.Y-dy*t)*(y-a.Y-dy*t);
            if(d<closest){closest=d;edge=i;fraction=(float)t;}
        }
        return edge>=0;
    }
    internal string Validation()
    {
        string result=GroundMap.Validate(Config);if(result!=null)return result;
        if(Config.mode=="Planes" && Config.surfaces.Any(s=>!entrances.Contains(s)||!exits.Contains(s)))return UiText.Get("请为每一块地面设置入口与出口。");
        return null;
    }
    private void RefreshHint()
    {
        string invalid=UiText.Get(Validation());save.Enabled=invalid==null;
        string action=operation=="Entrance"?UiText.Get("入口：在同一条边上点击两个端点（绿色）。"):operation=="Exit"?UiText.Get("出口：在同一条边上点击两个端点（橙色），可以与入口重合。"):Config.mode=="Line"?UiText.Get("直线：依次点击两个端点；已有端点可拖动。"):UiText.Get("四点：依次点击远左、远右、近右、近左；已有角点可拖动。");
        hint.Text=action+"  "+(pending.Count>0?UiText.Get("已点击 ")+pending.Count+UiText.Get(" 点。 "):"")+(gateEdge>=0?UiText.Get("请点击通道的第二个端点。 "):"")+UiText.Get("右键撤销未完成的点；Esc 取消。")+(invalid==null?UiText.Get(" 地面有效，可以保存。"):"\n"+invalid);
    }
    private void Commit(){RefreshHint();if(!save.Enabled)return;DialogResult=DialogResult.OK;Close();}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        if(Config.mode=="Line")DrawPoints(e.Graphics,Config.line,false,Color.Cyan,true);
        else for(int i=0;i<Config.surfaces.Length;i++)
        {
            var surface=Config.surfaces[i];bool active=i==selected;
            DrawPoints(e.Graphics,surface.points,true,active?Color.Cyan:Color.Gray,active);
            if(surface.points.Length==4)
            {
                using(var font=new Font("Microsoft YaHei UI",16,FontStyle.Bold))e.Graphics.DrawString(UiText.Get("地面 ")+(i+1),font,Brushes.White,Pixel(surface.points[0]));
                if(entrances.Contains(surface))DrawGate(e.Graphics,surface,surface.entrance,Color.Lime,UiText.Get("入口"));
                if(exits.Contains(surface))DrawGate(e.Graphics,surface,surface.exit,Color.Orange,UiText.Get("出口"));
            }
        }
        DrawPoints(e.Graphics,pending.ToArray(),false,Color.Yellow,true);
    }
    private void DrawPoints(Graphics g,GroundPoint[] points,bool closed,Color color,bool labels)
    {
        PointF[] p=points.Select(Pixel).ToArray();using(var pen=new Pen(color,4))
        {
            if(p.Length>1){if(closed&&p.Length==4){using(var fill=new SolidBrush(Color.FromArgb(35,color)))g.FillPolygon(fill,p);g.DrawPolygon(pen,p);}else g.DrawLines(pen,p);}
            for(int i=0;i<p.Length;i++){g.FillEllipse(Brushes.White,p[i].X-7,p[i].Y-7,14,14);g.DrawEllipse(pen,p[i].X-9,p[i].Y-9,18,18);if(labels)g.DrawString((i+1).ToString(),SystemFonts.DefaultFont,Brushes.White,p[i].X+12,p[i].Y+12);}
        }
    }
    private void DrawGate(Graphics g,GroundSurface surface,GroundGate gate,Color color,string text)
    {
        PointF a=Pixel(surface.points[gate.edge]),b=Pixel(surface.points[(gate.edge+1)%4]);
        PointF p=new PointF(a.X+(b.X-a.X)*gate.start,a.Y+(b.Y-a.Y)*gate.start),q=new PointF(a.X+(b.X-a.X)*gate.end,a.Y+(b.Y-a.Y)*gate.end);
        using(var pen=new Pen(color,10)){if(text==UiText.Get("出口") && surface.entrance.edge==gate.edge)pen.DashStyle=DashStyle.Dash;g.DrawLine(pen,p,q);}
        using(var brush=new SolidBrush(color))using(var font=new Font("Microsoft YaHei UI",13,FontStyle.Bold))g.DrawString(text,font,brush,(p.X+q.X)/2+12,(p.Y+q.Y)/2+(text==UiText.Get("出口")?10:-32));
    }
    internal static void RunTests(string root)
    {
        int checks=0;var serializer=new JavaScriptSerializer();
        using(var editor=new GroundEditor(new DesktopGroundConfig(),"Planes"))
        {
            editor.Show();Application.DoEvents();
            if(editor.ClientSize!=Screen.PrimaryScreen.Bounds.Size)throw new Exception("Canvas does not cover physical screen");checks++;
            int w=editor.ClientSize.Width,h=editor.ClientSize.Height;
            editor.ClickPoint(w/4,h/2);editor.ClickPoint(w*3/4,h/2);editor.ClickPoint(w*9/10,h*9/10);editor.ClickPoint(w/10,h*9/10);
            if(editor.Validation()==null)throw new Exception("Missing portals accepted");checks++;
            editor.ClickPoint(w*3/10,h/2);editor.ClickPoint(w*4/10,h/2);
            editor.ClickPoint(w*3/10,h/2);editor.ClickPoint(w*4/10,h/2);
            if(editor.Validation()!=null)throw new Exception(editor.Validation());checks++;
            editor.AddFloor();editor.AddFloor();editor.AddFloor();if(editor.Config.surfaces.Length!=3)throw new Exception("Floor limit");checks++;
            if(editor.Validation()==null)throw new Exception("Incomplete surface accepted");checks++;
            if(editor.Opacity>=1 || !editor.TopMost)throw new Exception("Overlay not translucent");checks++;
            using(var bar=new Bitmap(editor.tools.Width,editor.tools.Height)){editor.tools.DrawToBitmap(bar,new Rectangle(0,0,bar.Width,bar.Height));bar.Save(System.IO.Path.Combine(root,"ground-toolbar-preview.png"));}
            using(var image=new Bitmap(w,h)){editor.DrawToBitmap(image,new Rectangle(0,0,w,h));image.Save(System.IO.Path.Combine(root,"ground-editor-preview.png"));}checks++;
        }
        using(var line=new GroundEditor(new DesktopGroundConfig(),"Line"))
        {line.ClickPoint(100,400);line.ClickPoint(800,600);if(line.Validation()!=null)throw new Exception(line.Validation());checks++;}
        System.IO.File.WriteAllText(System.IO.Path.Combine(root,"ground-editor-report.json"),serializer.Serialize(new {Passed=true,Checks=checks,PreviewIsOwnEditorRender=true,PhysicalMouseVerified=false}));
    }
}

internal sealed partial class ControlPanel
{
    private readonly ComboBox groundMode=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=115};
    private void AddGroundControls(TableLayoutPanel layout)
    {
        var row=new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill};groundMode.Items.AddRange(new object[]{UiText.Get("二维直线"),UiText.Get("三维四点地面")});groundMode.SelectedIndex=0;
        row.Controls.Add(new Label {Text=UiText.Get("地面"),AutoSize=true,Padding=new Padding(0,8,0,0)});row.Controls.Add(groundMode);
        row.Controls.Add(Button(UiText.Get("绘制 / 编辑地面"),EditGround));row.Controls.Add(Button(UiText.Get("恢复屏幕底部"),()=>Send("ground",new Dictionary<string,object>{{"ground",new DesktopGroundConfig()}})));
        layout.RowCount=6;layout.Controls.Add(row,0,5);layout.SetColumnSpan(row,2);
    }
    private void EditGround()
    {
        Poll(true);FlushModelSize();DesktopGroundConfig config=new DesktopGroundConfig();
        if(state!=null && state.ContainsKey("ground"))config=json.Deserialize<DesktopGroundConfig>(json.Serialize(state["ground"]));
        Send("groundEditBegin");Hide();
        try
        {
            using(var editor=new GroundEditor(config,groundMode.SelectedIndex==0?"Line":"Planes"))
                if(editor.ShowDialog()==DialogResult.OK)Send("ground",new Dictionary<string,object>{{"ground",editor.Config}});
        }
        finally{Send("groundEditEnd");ShowPanel();}
    }
}
