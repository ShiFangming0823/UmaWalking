using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using UmaDesktop;

internal static class Program
{
    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
    [STAThread] private static void Main(string[] args)
    {
        SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        int parent=0;string root=AppDomain.CurrentDomain.BaseDirectory;
        for(int i=0;i+1<args.Length;i++) {if(args[i]=="--parent")int.TryParse(args[i+1],out parent);if(args[i]=="--root")root=args[i+1];}
        try
        {
            if(args.Contains("--restart")) {DesktopRestart.Run(args);return;}
            if(args.Contains("--release-selftest")){ControlPanel.RunReleaseTests(root);return;}
            if(args.Contains("--ground-selftest")) {GroundEditor.RunTests(root);return;}
            if(args.Contains("--desktop-ground-selftest")) {ControlPanel.RunDesktopGroundTests(root);return;}
            if(args.Contains("--switch-data-test")){ControlPanel.RunDataSwitch(root,parent,args[Array.IndexOf(args,"--destination")+1]);return;}
            if(args.Contains("--names-selftest")){ControlPanel.RunNameTests(root);return;}
            if(args.Contains("--options-selftest")){ControlPanel.RunOptionsTests(root);return;}
            if(args.Contains("--extract-test")){ControlPanel.RunExtraction(root,args[Array.IndexOf(args,"--plan")+1],args[Array.IndexOf(args,"--destination")+1]);return;}
            if(args.Contains("--selftest")) {ControlPanel.RunSelfTest(root);return;}
            Application.Run(new ControlPanel(root,parent,args.Contains("--show")));
        }
        catch(Exception ex){Environment.ExitCode=1;Directory.CreateDirectory(Path.Combine(root,"DesktopLogs"));File.WriteAllText(Path.Combine(root,"DesktopLogs","controller-error.log"),ex.ToString());}
    }
}
internal sealed partial class ControlPanel : Form
{
    private readonly string root;private readonly int parent;
    private readonly JavaScriptSerializer json=new JavaScriptSerializer {MaxJsonLength=16*1024*1024};
    private readonly Timer timer=new Timer {Interval=800};
    private readonly Timer sizeTimer=new Timer {Interval=180};
    private readonly TrackBar modelSize=new TrackBar {Minimum=50,Maximum=150,Value=100,TickFrequency=25,SmallChange=5,LargeChange=10,AutoSize=false,Height=35,Dock=DockStyle.Fill,AccessibleName=UiText.Get("模型大小百分比")};
    private readonly Label sizeLabel=new Label {Text=UiText.Get("模型大小 100%"),AutoSize=true,Anchor=AnchorStyles.Left};
    private bool sizeDirty,syncingSize,awaitingSize,restarting;
    private int requestedSize=100;
    private readonly Action restartAction;
    private Button restartButton;
    private ToolStripMenuItem trayRestart;
    private readonly Process parentProcess;
    private readonly NotifyIcon tray=new NotifyIcon();
    private readonly ComboBox actorBox=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,DropDownWidth=640,AccessibleName=UiText.Get("选择屏幕上的马娘")};
    private readonly ComboBox costumeBox=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,AccessibleName=UiText.Get("选择当前马娘的服装")};
    private readonly Label status=new Label {AutoSize=true,ForeColor=Color.FromArgb(65,75,90),MaximumSize=new Size(600,0)};
    private readonly Label detail=new Label {AutoSize=true,MaximumSize=new Size(600,0)};
    private readonly Label selectionCount=new Label {AutoSize=true};
    private readonly Label message=new Label {AutoSize=true,MaximumSize=new Size(600,0),ForeColor=Color.FromArgb(65,75,90)};
    private readonly RadioButton all=new RadioButton {Text=UiText.Get("全随机"),AutoSize=true,AccessibleName=UiText.Get("全随机")};
    private readonly RadioButton include=new RadioButton {Text=UiText.Get("Include · 只选这些"),AutoSize=true,AccessibleName=UiText.Get("Include 只选这些")};
    private readonly RadioButton exclude=new RadioButton {Text=UiText.Get("Exclude · 排除这些"),AutoSize=true,AccessibleName=UiText.Get("Exclude 排除这些")};
    private readonly TextBox search=new TextBox {Dock=DockStyle.Fill,AccessibleName=UiText.Get("搜索角色名称或编号")};
    private readonly CheckedListBox list=new CheckedListBox {Dock=DockStyle.Fill,CheckOnClick=true,IntegralHeight=false,BorderStyle=BorderStyle.FixedSingle,AccessibleName=UiText.Get("可多选的角色列表")};
    private readonly HashSet<int> included=new HashSet<int>(),excluded=new HashSet<int>();
    private List<CharacterItem> catalog=new List<CharacterItem>();
    private Dictionary<string,object> state;
    private bool initialized,rebuilding,quitting;
    private string actorKey="";private int costumeCharacter, costumeToken, costumeOrdinal;
    private long lastCommandTicks;
    private string Mode {get{return include.Checked?"Include":exclude.Checked?"Exclude":"All";}}
    private int SelectedToken {get{return actorBox.SelectedItem is Choice ? ((Choice)actorBox.SelectedItem).Id : 0;}}
    private sealed class Choice {public int Id;public string Label;public override string ToString(){return Label;}}
    private sealed class CharacterItem {public int Id;public string Name;public List<CostumeItem> Costumes;public string DisplayName{get{return DesktopCharacterNames.Get(Id,Name,UiText.Language);}}public override string ToString(){return Id+"  "+DisplayName;}}
    private sealed class CostumeItem {public string Id,Name;public override string ToString(){return Id+"  "+Name;}}

    public ControlPanel(string root,int parent,bool show,Action restartAction=null)
    {
        this.root=root;this.parent=parent;this.restartAction=restartAction??(()=>DesktopRestart.Start(root,parent,Process.GetCurrentProcess().Id,modelSize.Value,Convert.ToInt32(actorLimitBox.Value)));
        if(parent>0) parentProcess=Process.GetProcessById(parent);
        Text="UmaWalking";AccessibleName="UmaWalking";
        Font=new Font("Microsoft YaHei UI",9.5f);ClientSize=new Size(700,950);MinimumSize=new Size(700,900);
        StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(248,250,253);
        try {Icon=Icon.ExtractAssociatedIcon(Path.Combine(root,"UmaDesktop.exe"));}catch{Icon=SystemIcons.Application;}
        var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=1,RowCount=9};
        for(int i=0;i<9;i++)layout.RowStyles.Add(new RowStyle(i==5?SizeType.Percent:SizeType.AutoSize,i==5?100:0));
        Controls.Add(layout);
        var title=new Label {Text=UiText.Get("桌面上的马娘"),AutoSize=true,Font=new Font(Font.FontFamily,17,FontStyle.Bold),Margin=new Padding(0,0,0,5)};
        layout.Controls.Add(title,0,0);layout.Controls.Add(status,0,1);
        var actorGroup=new GroupBox {Text=UiText.Get("当前角色"),Dock=DockStyle.Top,AutoSize=false,Height=435,Padding=new Padding(12),Margin=new Padding(0,14,0,10)};
        var actorLayout=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=2,RowCount=5};
        actorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));actorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actorLayout.Controls.Add(actorBox,0,0);
        var actorButtons=new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill,WrapContents=false};
        retireButton=Button(UiText.Get("立即退场"),()=>Send("retire"));actorButtons.Controls.Add(retireButton);actorButtons.Controls.Add(Button(UiText.Get("更换角色"),()=>Send("next")));actorLayout.Controls.Add(actorButtons,1,0);
        actorLayout.Controls.Add(costumeBox,0,1);actorLayout.Controls.Add(Button(UiText.Get("换成所选服装"),()=>Send("costume",new Dictionary<string,object>{{"costumeId",costumeBox.SelectedItem is CostumeItem?((CostumeItem)costumeBox.SelectedItem).Id:null}})),1,1);
        var actionRow=new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,6,0,0)};
        actionRow.Controls.Add(Button(UiText.Get("播放专属动作"),()=>Send("special")));actionRow.Controls.Add(Button(UiText.Get("随机换装"),()=>Send("costume")));actionRow.Controls.Add(Button(UiText.Get("暂停 / 继续"),()=>Send("pause")));
        actorLayout.Controls.Add(actionRow,0,2);actorLayout.SetColumnSpan(actionRow,2);
        actorLayout.Controls.Add(detail,0,3);actorLayout.SetColumnSpan(detail,2);actorGroup.Controls.Add(actorLayout);layout.Controls.Add(actorGroup,0,2);
        var sizeRow=new TableLayoutPanel {AutoSize=true,Dock=DockStyle.Fill,ColumnCount=2,Margin=new Padding(0,8,0,0)};
        sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,145));sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        sizeRow.Controls.Add(sizeLabel,0,0);sizeRow.Controls.Add(modelSize,1,0);actorLayout.Controls.Add(sizeRow,0,4);actorLayout.SetColumnSpan(sizeRow,2);
        AddGroundControls(actorLayout);AddEntryControls(actorLayout);AddActorLimitControls(actorLayout);
        modelSize.ValueChanged+=(s,e)=>{sizeLabel.Text=UiText.Get("模型大小 ")+modelSize.Value+"%";if(syncingSize)return;sizeDirty=true;sizeTimer.Stop();sizeTimer.Start();};
        modelSize.MouseUp+=(s,e)=>FlushModelSize();sizeTimer.Tick+=(s,e)=>FlushModelSize();
        var modes=new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill};modes.Controls.AddRange(new Control[]{all,include,exclude});layout.Controls.Add(modes,0,3);
        var find=new TableLayoutPanel {AutoSize=true,Dock=DockStyle.Fill,ColumnCount=2,Margin=new Padding(0,8,0,6)};
        find.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));find.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        find.Controls.Add(new Label {Text=UiText.Get("搜索角色"),AutoSize=true,Anchor=AnchorStyles.Left},0,0);find.Controls.Add(search,1,0);layout.Controls.Add(find,0,4);
        layout.Controls.Add(list,0,5);
        var tools=new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,6,0,5)};
        tools.Controls.Add(Button(UiText.Get("全选搜索结果"),()=>SetVisibleChecks(true)));tools.Controls.Add(Button(UiText.Get("清空搜索结果"),()=>SetVisibleChecks(false)));tools.Controls.Add(selectionCount);layout.Controls.Add(tools,0,6);
        var apply=new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill};
        var save=Button(UiText.Get("应用角色范围"),ApplyFilter);save.BackColor=Color.FromArgb(48,110,200);save.ForeColor=Color.White;save.FlatStyle=FlatStyle.Flat;
        apply.Controls.Add(save);apply.Controls.Add(Button(UiText.Get("隐藏面板"),()=>Hide()));restartButton=Button(UiText.Get("强制重启"),Restart);apply.Controls.Add(restartButton);
        var exit=Button(UiText.Get("退出程序"),()=>{message.Text=UiText.Get("正在退出…");Send("exit");});exit.ForeColor=Color.FromArgb(175,40,40);apply.Controls.Add(exit);layout.Controls.Add(apply,0,7);
        layout.Controls.Add(message,0,8);
        all.CheckedChanged+=(s,e)=>RefreshChoices();include.CheckedChanged+=(s,e)=>RefreshChoices();exclude.CheckedChanged+=(s,e)=>RefreshChoices();
        search.TextChanged+=(s,e)=>RefreshChoices();
        list.ItemCheck+=(s,e)=>{if(rebuilding || Mode=="All")return;var item=(CharacterItem)list.Items[e.Index];var set=Mode=="Include"?included:excluded;if(e.NewValue==CheckState.Checked)set.Add(item.Id);else set.Remove(item.Id);BeginInvoke((Action)UpdateCount);};
        actorBox.SelectedIndexChanged+=(s,e)=>UpdateCostumes();
        var trayMenu=new ContextMenuStrip();
        trayMenu.Items.Add(UiText.Get("打开控制面板"),null,(s,e)=>ShowPanel());trayMenu.Items.Add(UiText.Get("播放选中角色专属动作"),null,(s,e)=>Send("special"));
        trayMenu.Items.Add(UiText.Get("选中角色立即退场"),null,(s,e)=>Send("retire"));trayMenu.Items.Add(UiText.Get("更换选中角色"),null,(s,e)=>Send("next"));trayMenu.Items.Add(UiText.Get("随机更换当前衣服"),null,(s,e)=>Send("costume"));
        trayMenu.Items.Add(UiText.Get("暂停 / 继续"),null,(s,e)=>Send("pause"));trayMenu.Items.Add(new ToolStripSeparator());trayRestart=(ToolStripMenuItem)trayMenu.Items.Add(UiText.Get("强制重启"),null,(s,e)=>Restart());trayMenu.Items.Add(UiText.Get("退出程序"),null,(s,e)=>Send("exit"));
        tray.Icon=Icon;tray.Text=UiText.Get("UmaWalking · 左键控制面板 / 右键菜单");tray.Visible=true;tray.ContextMenuStrip=trayMenu;
        tray.MouseClick+=(s,e)=>{if(e.Button==MouseButtons.Left)ShowPanel();};
        FormClosing+=(s,e)=>{if(!quitting){e.Cancel=true;Hide();}};
        timer.Tick+=(s,e)=>Poll();timer.Start();
        // Keep a normal taskbar entry as well as the notification-area icon.
        AddOptions(layout);ApplyUiLanguage();
        WindowState=show||!File.Exists(Path.Combine(dataBox.Text,"meta"))||!File.Exists(Path.Combine(dataBox.Text,"master","master.mdb"))?FormWindowState.Normal:FormWindowState.Minimized;Shown+=(s,e)=>Poll();
    }
    private Button Button(string text,Action action)
    {var b=new Button {Text=text,AutoSize=true,Padding=new Padding(8,3,8,3),Margin=new Padding(2,3,6,3),AccessibleName=text};b.Click+=(s,e)=>action();return b;}
    private void ShowPanel(){Show();WindowState=FormWindowState.Normal;Poll(true);Activate();}
    private static IEnumerable<object> ArrayOf(object value){var items=value as IEnumerable;return items==null?Enumerable.Empty<object>():items.Cast<object>();}
    private static string S(Dictionary<string,object> d,string key){return d.ContainsKey(key)&&d[key]!=null?Convert.ToString(d[key]):"";}
    private static int I(Dictionary<string,object> d,string key){return d.ContainsKey(key)?Convert.ToInt32(d[key]):0;}
    private void Poll(bool force=false)
    {
        if(parentProcess!=null){try{if(parentProcess.HasExited){Quit();return;}}catch{Quit();return;}}
        if(!force && (!Visible || WindowState==FormWindowState.Minimized) && pendingRestartId==null && !File.Exists(Path.Combine(root,"DesktopLogs","pending-cleanup.json"))) return;
        try
        {
            state=json.Deserialize<Dictionary<string,object>>(ReadShared(Path.Combine(root,"DesktopLogs","state.json")));
            if(parent>0 && I(state,"processId")!=parent)return;
            PollOptions();
            if(catalog.Count==0 && File.Exists(Path.Combine(root,"DesktopLogs","costume-catalog.json")))
            {
                var data=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(root,"DesktopLogs","costume-catalog.json"),Encoding.UTF8));
                catalog=ArrayOf(data["characters"]).Select(o=>{
                    var c=(Dictionary<string,object>)o;return new CharacterItem {Id=I(c,"id"),Name=S(c,"name"),Costumes=ArrayOf(c["costumes"]).Select(x=>{var d=(Dictionary<string,object>)x;return new CostumeItem{Id=S(d,"id"),Name=S(d,"name")};}).ToList()};
                }).ToList();
            }
            state=json.Deserialize<Dictionary<string,object>>(ReadShared(Path.Combine(root,"DesktopLogs","state.json")));
            if(!initialized)
            {
                foreach(var id in ArrayOf(state["includeIds"]))included.Add(Convert.ToInt32(id));foreach(var id in ArrayOf(state["excludeIds"]))excluded.Add(Convert.ToInt32(id));
                string mode=S(state,"mode");include.Checked=mode=="Include";exclude.Checked=mode=="Exclude";all.Checked=mode!="Include"&&mode!="Exclude";
                if(state.ContainsKey("ground") && state["ground"]!=null)groundMode.SelectedIndex=S((Dictionary<string,object>)state["ground"],"mode")=="Desktop"?2:S((Dictionary<string,object>)state["ground"],"mode")=="Planes"?1:0;
                initialized=true;RefreshChoices();
            }
            int actualSize=state.ContainsKey("modelSizeScale")?(int)Math.Round(Convert.ToDouble(state["modelSizeScale"])*100):100;
            if(awaitingSize && actualSize==requestedSize)awaitingSize=false;
            if(!sizeDirty && !awaitingSize && !modelSize.Capture)
            {syncingSize=true;modelSize.Value=Math.Max(50,Math.Min(150,actualSize));syncingSize=false;}
            PollActorLimit();UpdateEntryChoices();
            var actors=ArrayOf(state["actors"]).Cast<Dictionary<string,object>>().ToList();
            string key=UiText.Language+"|"+string.Join(",",actors.Select(a=>I(a,"token").ToString()));
            if(key!=actorKey)
            {
                int selected=SelectedToken;actorKey=key;actorBox.BeginUpdate();actorBox.Items.Clear();
                foreach(var a in actors)actorBox.Items.Add(new Choice{Id=I(a,"token"),Label=ActorLabel(a)});
                int index=FindActorIndex(actors,selected,costumeOrdinal,costumeCharacter);
                actorBox.SelectedIndex=actorBox.Items.Count==0?-1:Math.Max(0,index);actorBox.EndUpdate();
            }
            status.Text=UiText.Get("屏幕上 ")+I(state,"count")+" / "+I(state,"targetCount")+UiText.Get(" 只 · ")+RunStatus(state)+" · "+(S(state,"populationState")=="WaitingForExit"?UiText.Get("等待角色离场"):UiText.Get("按计划出入场"));
            message.Text=string.IsNullOrEmpty(S(state,"error"))?LocalizeRuntimeMessage(S(state,"message")):UiText.Get("运行异常，请查看 DesktopLogs；仍可点击退出程序。");
            var current=actors.FirstOrDefault(a=>I(a,"token")==SelectedToken);
            if(current!=null)
            {detail.Text=S(current,"costumeName")+" · "+TranslateState(S(current,"phase"),S(current,"state"));UpdateCostumes();}
            else detail.Text=UiText.Get("尚无角色。Include 未勾选任何角色时，桌面会保持空白。");
        }
        catch(IOException){}catch(ArgumentException){}catch(InvalidOperationException){}
    }
    private static string RunStatus(Dictionary<string,object> value)
    {
        if(value.ContainsKey("desktopPaused") && Convert.ToBoolean(value["desktopPaused"]))
            return value.ContainsKey("desktopAvailable") && !Convert.ToBoolean(value["desktopAvailable"]) ? UiText.Get("等待桌面，已自动暂停") : UiText.Get("桌面被遮挡，已自动暂停");
        if(value.ContainsKey("groundEditing") && Convert.ToBoolean(value["groundEditing"]))return UiText.Get("正在编辑地面，角色已暂停");
        return Convert.ToBoolean(value["paused"]) ? UiText.Get("手动暂停") : UiText.Get("运行中");
    }
    private static string ReadShared(string path)
    {
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
        using(var reader=new StreamReader(stream,Encoding.UTF8)) return reader.ReadToEnd();
    }
    private static string TranslateState(string phase,string behavior)
    {if(phase=="Entering")return UiText.Get("从边缘入场");if(phase=="Exiting")return UiText.Get("前往边缘离场");return behavior=="Walk"?UiText.Get("步行"):behavior=="Run"?UiText.Get("奔跑"):behavior=="Interaction"?UiText.Get("双人互动"):UiText.Get("专属动作");}
    private static string LocalizeRuntimeMessage(string raw)
    {
        string text=DesktopCharacterNames.LocalizeMessage(raw,UiText.Language)??"";
        int separator=text.IndexOf(" · ",StringComparison.Ordinal);
        return separator<0?L(text):L(text.Substring(0,separator))+text.Substring(separator);
    }
    private static int FindActorIndex(List<Dictionary<string,object>> actors,int token,int ordinal,int character)
    {
        int index=actors.FindIndex(a=>I(a,"token")==token);
        if(index<0&&ordinal>0)index=actors.FindIndex(a=>I(a,"ordinal")==ordinal);
        if(index<0)index=actors.FindIndex(a=>I(a,"characterId")==character);
        return index;
    }
    private static string ActorLabel(Dictionary<string,object> actor)
    {return DesktopCharacterNames.Get(I(actor,"characterId"),S(actor,"name"),UiText.Language)+" · #"+I(actor,"token")+" · "+S(actor,"costumeName")+" ["+S(actor,"costumeId")+"]";}
    private void UpdateCostumes()
    {
        if(state==null)return;
        var current=ArrayOf(state["actors"]).Cast<Dictionary<string,object>>().FirstOrDefault(a=>I(a,"token")==SelectedToken);
        if(current==null){costumeBox.Items.Clear();costumeCharacter=0;return;}
        int id=I(current,"characterId");
        if(id==costumeCharacter && I(current,"token")==costumeToken)return;costumeCharacter=id;costumeToken=I(current,"token");costumeOrdinal=I(current,"ordinal");costumeBox.Items.Clear();
        var character=catalog.FirstOrDefault(c=>c.Id==id);if(character==null)return;
        foreach(var costume in character.Costumes)costumeBox.Items.Add(costume);
        costumeBox.SelectedIndex=Math.Max(0,character.Costumes.FindIndex(c=>c.Id==S(current,"costumeId")));
    }
    private void RefreshChoices()
    {
        if(rebuilding)return;rebuilding=true;list.BeginUpdate();list.Items.Clear();
        string query=search.Text.Trim();var set=Mode=="Include"?included:excluded;
        foreach(var character in catalog.Where(c=>query.Length==0 || DesktopCharacterNames.Matches(c.Id,c.Name,query)))
            list.Items.Add(character,Mode!="All"&&set.Contains(character.Id));
        list.Enabled=Mode!="All";list.EndUpdate();rebuilding=false;UpdateCount();
    }
    private void UpdateCount(){selectionCount.Text=Mode=="All"?UiText.Get("使用全部可用角色"):(Mode=="Include"?UiText.Get("已包含 "):UiText.Get("已排除 "))+(Mode=="Include"?included.Count:excluded.Count)+UiText.Get(" 名");}
    private void SetVisibleChecks(bool value){if(Mode=="All")return;for(int i=0;i<list.Items.Count;i++)list.SetItemChecked(i,value);}
    private void FlushModelSize()
    {
        sizeTimer.Stop();if(!sizeDirty)return;sizeDirty=false;awaitingSize=true;requestedSize=modelSize.Value;
        Send("scale",new Dictionary<string,object>{{"modelSizeScale",modelSize.Value/100.0}});
    }
    private void Restart()
    {
        if(restarting||extractionBusy)return;
        try{Poll(true);FlushModelSize();FlushActorLimit();restarting=true;restartButton.Enabled=trayRestart.Enabled=false;message.Text=UiText.Get("正在强制重启…");restartAction();}
        catch(Exception ex){restarting=false;restartButton.Enabled=trayRestart.Enabled=true;message.Text=UiText.Get("无法重启：")+ex.Message;}
    }
    private void ApplyFilter(){Send("filter",new Dictionary<string,object>{{"mode",Mode},{"includeIds",included.OrderBy(x=>x).ToArray()},{"excludeIds",excluded.OrderBy(x=>x).ToArray()}});message.Text=UiText.Get("已应用；不在范围内的角色会从边缘离场。");}
    private void Send(string action,Dictionary<string,object> values=null)
    {
        try
        {
            var command=values??new Dictionary<string,object>();command["action"]=action;command["token"]=SelectedToken;
            string dir=Path.Combine(root,"DesktopCommands");Directory.CreateDirectory(dir);
            // The Windows clock may return the same tick for consecutive commands.
            // Preserve their order instead of ordering equal-time files by random GUID.
            lastCommandTicks=Math.Max(DateTime.UtcNow.Ticks,lastCommandTicks+1);
            string name=lastCommandTicks.ToString("D19")+"-"+Guid.NewGuid().ToString("N");
            string temp=Path.Combine(dir,name+".tmp");File.WriteAllText(temp,json.Serialize(command),new UTF8Encoding(false));File.Move(temp,Path.Combine(dir,name+".json"));
        }
        catch(Exception ex){message.Text=UiText.Get("操作未完成：")+ex.Message;}
    }
    public static void RunSelfTest(string root)
    {
        string reportPath=Path.Combine(root,"controller-selftest-report.json");
        if(File.Exists(reportPath))File.Delete(reportPath);
        int restarts=0;UiText.Language="zh";
        using(var panel=new ControlPanel(root,0,true,()=>restarts++))
        {
            IntPtr ensureHandle=panel.Handle;panel.Poll(true);panel.Show();panel.Update();
            if(panel.catalog.Count<100 || !panel.initialized || panel.actorBox.Items.Count!=1)throw new Exception("Controller fixture/catalog did not initialize.");
            panel.include.Checked=true;panel.list.SetItemChecked(0,true);panel.list.SetItemChecked(1,true);panel.ApplyFilter();
            var first=panel.LatestCommand();
            if(S(first,"mode")!="Include" || ArrayOf(first["includeIds"]).Count()!=2)throw new Exception("Include multi-select command failed.");
            panel.exclude.Checked=true;panel.list.SetItemChecked(2,true);panel.ApplyFilter();
            var second=panel.LatestCommand();
            if(S(second,"mode")!="Exclude" || ArrayOf(second["excludeIds"]).Count()!=1 || ArrayOf(second["includeIds"]).Count()!=2)throw new Exception("Separate Include/Exclude lists failed.");
            panel.all.Checked=true;panel.ApplyFilter();if(S(panel.LatestCommand(),"mode")!="All")throw new Exception("All mode failed.");
            foreach(string action in new[]{"special","next","costume","exit"})
            {panel.Send(action);var command=panel.LatestCommand();if(S(command,"action")!=action || I(command,"token")!=88)throw new Exception("Actor command failed: "+action);}
            panel.retireButton.PerformClick();var retire=panel.LatestCommand();
            if(S(retire,"action")!="retire" || I(retire,"token")!=88)throw new Exception("Explicit retire selected the wrong actor.");
            if(panel.entryBox.Items.Count!=panel.catalog.Count)throw new Exception("Manual entry catalog incomplete.");
            panel.entryBox.SelectedIndex=1;panel.enterButton.PerformClick();var enter=panel.LatestCommand();
            if(S(enter,"action")!="enter" || I(enter,"characterId")!=panel.catalog[1].Id)throw new Exception("Manual entry selection command failed.");
            if(EntryQueueText(new Dictionary<string,object>())!=UiText.Get("上场队列：无；达到数量上限时等待空位。"))throw new Exception("Empty queue display failed.");
            var queueFixture=new Dictionary<string,object>{{"pendingEntries",new object[]{new Dictionary<string,object>{{"name","A"}},new Dictionary<string,object>{{"name","B"}}}}};
            if(EntryQueueText(queueFixture)!=UiText.Get("等待上场（按顺序）：A → B"))throw new Exception("FIFO queue display failed.");
            panel.modelSize.Value=125;panel.FlushModelSize();var scale=panel.LatestCommand();
            if(S(scale,"action")!="scale" || Math.Abs(Convert.ToDouble(scale["modelSizeScale"])-1.25)>.0001)throw new Exception("Size slider command failed.");
            panel.Restart();if(restarts!=1)throw new Exception("Panel restart handler failed.");
            panel.restarting=false;panel.trayRestart.Enabled=true;panel.trayRestart.PerformClick();if(restarts!=2)throw new Exception("Tray restart handler failed.");
            if(DesktopRestart.Quote("plain")!="\"plain\"" || DesktopRestart.Quote("C:\\a b\\")!="\"C:\\a b\\\\\"")throw new Exception("Restart argument quoting failed.");
            var pauseState=new Dictionary<string,object>{{"paused",false},{"desktopPaused",false},{"desktopAvailable",true}};
            if(RunStatus(pauseState)!=UiText.Get("运行中"))throw new Exception("Running status failed.");
            pauseState["paused"]=true;if(RunStatus(pauseState)!=UiText.Get("手动暂停"))throw new Exception("Manual pause status failed.");
            pauseState["desktopPaused"]=true;if(RunStatus(pauseState)!=UiText.Get("桌面被遮挡，已自动暂停"))throw new Exception("Automatic pause status failed.");
            pauseState["desktopAvailable"]=false;if(RunStatus(pauseState)!=UiText.Get("等待桌面，已自动暂停"))throw new Exception("Desktop unavailable status failed.");
            if(panel.groundMode.Items.Count!=3)throw new Exception("Ground mode choices missing.");
            panel.Send("ground",new Dictionary<string,object>{{"ground",new UmaDesktop.DesktopGroundConfig()}});
            if(S(panel.LatestCommand(),"action")!="ground")throw new Exception("Ground command missing.");
            pauseState["desktopPaused"]=false;pauseState["groundEditing"]=true;
            if(RunStatus(pauseState)!=UiText.Get("正在编辑地面，角色已暂停"))throw new Exception("Ground editor pause status failed.");
            panel.WindowState=FormWindowState.Normal;panel.Show();Application.DoEvents();panel.PerformLayout();
            if(panel.list.Height<100)throw new Exception("Character selection list has insufficient height: "+panel.list.Height);
            var entryBottom=panel.PointToClient(panel.entryQueue.Parent.PointToScreen(panel.entryQueue.Bounds.Location)).Y+panel.entryQueue.Height;
            var entryGroup=panel.entryQueue.Parent.Parent;
            int queueBottom=entryGroup.PointToClient(panel.entryQueue.Parent.PointToScreen(panel.entryQueue.Location)).Y+panel.entryQueue.Height;
            if(entryBottom>=panel.ClientSize.Height-100 || queueBottom>entryGroup.ClientSize.Height-6 || panel.entryBox.Width<120)throw new Exception("Entry controls clipped or too narrow.");
            if(panel.retireButton.Width<=0 || panel.enterButton.Width<=0)throw new Exception("Entry/exit buttons have no visible layout.");
            using(var preview=new Bitmap(panel.Width,panel.Height)){panel.DrawToBitmap(preview,new Rectangle(0,0,panel.Width,panel.Height));preview.Save(Path.Combine(root,"controller-preview.png"));}
            var cloneA=new Dictionary<string,object>{{"name","A"},{"token",88},{"costumeId","00"},{"costumeName","Race"}};var cloneB=new Dictionary<string,object>{{"name","A"},{"token",89},{"costumeId","40"},{"costumeName","Other"}};
            if(ActorLabel(cloneA)==ActorLabel(cloneB)||!ActorLabel(cloneA).Contains("#88")||!ActorLabel(cloneB).Contains("[40]"))throw new Exception("Repeated character labels are ambiguous.");
            cloneA["ordinal"]=7;cloneA["characterId"]=1001;cloneB["ordinal"]=8;cloneB["characterId"]=1001;
            if(FindActorIndex(new List<Dictionary<string,object>>{cloneB,cloneA},999,7,1001)!=1)throw new Exception("Costume replacement selected another copy.");
            var report=new Dictionary<string,object>{{"Passed",true},{"Checks",29},{"CatalogCount",panel.catalog.Count},{"Scope","Controller state and command handlers; physical pointer input and screenshot layout are not verified by this test."}};
            File.WriteAllText(Path.Combine(root,"controller-selftest-report.json"),panel.json.Serialize(report),new UTF8Encoding(false));
        }
    }
    private Dictionary<string,object> LatestCommand()
    {
        string path=Directory.GetFiles(Path.Combine(root,"DesktopCommands"),"*.json").OrderBy(p=>p,StringComparer.Ordinal).Last();
        return json.Deserialize<Dictionary<string,object>>(File.ReadAllText(path,Encoding.UTF8));
    }
    private void Quit(){quitting=true;timer.Stop();tray.Visible=false;tray.Dispose();Close();}
    protected override void Dispose(bool disposing){if(disposing){tray.Visible=false;tray.Dispose();timer.Dispose();sizeTimer.Dispose();actorLimitTimer.Dispose();if(parentProcess!=null)parentProcess.Dispose();}base.Dispose(disposing);}
}
