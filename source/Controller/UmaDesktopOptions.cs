using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using UmaDesktop;

internal sealed partial class ControlPanel
{
    private readonly ComboBox fpsBox=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=160};
    private readonly CheckBox avoidanceBox=new CheckBox {AutoSize=true};
    private readonly CheckBox aaBox=new CheckBox {AutoSize=true};
    private readonly ComboBox languageBox=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=180};
    private readonly TextBox dataBox=new TextBox {ReadOnly=true,Dock=DockStyle.Top};
    private readonly Label optionsMessage=new Label {AutoSize=true,MaximumSize=new Size(570,0)};
    private readonly ProgressBar extractionProgress=new ProgressBar {Width=560,Height=20};
    private TabControl tabs;private Button extractButton,browseButton;
    private bool syncingOptions,cleanupChecked,extractionBusy,optionsStateInitialized;
    private CancellationTokenSource extractionCancellation;
    private string pendingRestartId;private DateTime pendingRestartAt;
    private readonly Dictionary<Control,string> originalTexts=new Dictionary<Control,string>();
    private readonly Dictionary<ToolStripItem,string> originalMenus=new Dictionary<ToolStripItem,string>();
    private static string L(string text){return UiText.Get(text);}
    private void AddOptions(Control content)
    {
        tabs=new TabControl {Dock=DockStyle.Fill};Controls.Remove(content);Controls.Add(tabs);
        var actorsPage=new TabPage("角色与地面");actorsPage.Controls.Add(content);tabs.TabPages.Add(actorsPage);
        var page=new TabPage("设置与诊断") {AutoScroll=true,Padding=new Padding(20)};tabs.TabPages.Add(page);
        var layout=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(6)};page.Controls.Add(layout);
        AddReleaseOptions(layout);
        layout.Controls.Add(new Label {Text="性能",AutoSize=true,Font=new Font(Font.FontFamily,14,FontStyle.Bold)});
        var row=new FlowLayoutPanel {AutoSize=true};row.Controls.Add(new Label {Text="帧率",AutoSize=true,Padding=new Padding(0,7,12,0)});fpsBox.Items.AddRange(new object[]{15,30,60,120});row.Controls.Add(fpsBox);layout.Controls.Add(row);
        aaBox.Text="抗锯齿（4× MSAA + FXAA）";layout.Controls.Add(aaBox);
        layout.Controls.Add(new Label {Text="默认 30 帧；关闭抗锯齿或降低帧率可减少负载。",AutoSize=true,MaximumSize=new Size(560,0),Margin=new Padding(0,8,0,20)});
        avoidanceBox.Text="三维地面角色避让（实验性）";layout.Controls.Add(avoidanceBox);
        layout.Controls.Add(new Label {Text="提前绕行，空间不足时转向或让行；仅三维地面生效。地面过窄或路线不理想时可关闭。",AutoSize=true,MaximumSize=new Size(560,0),Margin=new Padding(0,4,0,16)});
        layout.Controls.Add(new Label {Text="语言 / Language / 言語",AutoSize=true});languageBox.Items.AddRange(new object[]{"简体中文","日本語","English"});layout.Controls.Add(languageBox);
        layout.Controls.Add(new Label {Text="游戏数据",AutoSize=true,Font=new Font(Font.FontFamily,14,FontStyle.Bold),Margin=new Padding(0,24,0,8)});
        dataBox.Width=560;layout.Controls.Add(dataBox);var paths=new FlowLayoutPanel {AutoSize=true};
        browseButton=Button("选择数据目录…",ChooseData);paths.Controls.Add(browseButton);extractButton=Button("提取迷你角色素材…",ExtractData);paths.Controls.Add(extractButton);layout.Controls.Add(paths);
        layout.Controls.Add(new Label {Text="请选择包含 meta、master 和 dat 的目录。切换目录后自动重启。\n提取保留所有迷你角色服装、动作、表情及依赖，不是通用游戏备份。",AutoSize=true,MaximumSize=new Size(560,0)});
        layout.Controls.Add(extractionProgress);layout.Controls.Add(Button("取消提取",()=>{if(extractionCancellation!=null)extractionCancellation.Cancel();}));
        layout.Controls.Add(new Label {Text="问题诊断",AutoSize=true,Font=new Font(Font.FontFamily,14,FontStyle.Bold),Margin=new Padding(0,24,0,8)});
        layout.Controls.Add(Button("拷贝当前状态与日志",CopyDiagnostics));AddIssueLink(layout);
        layout.Controls.Add(new Label {Text="包含设置、地面、角色动作、硬件与最近日志；默认隐藏本机路径。请同时描述复现步骤。",AutoSize=true,MaximumSize=new Size(560,0)});layout.Controls.Add(optionsMessage);
        var settings=ReadSavedSettings();string language=S(settings,"UiLanguage");
        if(language!="zh"&&language!="ja"&&language!="en"){language=System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;if(language!="ja"&&language!="zh")language="en";}
        UiText.Language=language;syncingOptions=true;
        languageBox.SelectedIndex=language=="ja"?1:language=="en"?2:0;
        int fps=settings.ContainsKey("FrameRate")?I(settings,"FrameRate"):30;fpsBox.SelectedItem=new[]{15,30,60,120}.Contains(fps)?fps:30;
        avoidanceBox.Checked=settings.ContainsKey("Avoid3DCollisions")&&Convert.ToBoolean(settings["Avoid3DCollisions"]);
        aaBox.Checked=!settings.ContainsKey("MsaaSamples")||I(settings,"MsaaSamples")>1;dataBox.Text=S(settings,"DataPath");
        if(DesktopProfile.IsLegacyPlaceholder(dataBox.Text,root))dataBox.Text=DesktopProduct.DefaultDataPath;syncingOptions=false;
        avoidanceBox.CheckedChanged+=(s,e)=>{if(!syncingOptions)Send("avoidance",new Dictionary<string,object>{{"avoid3DCollisions",avoidanceBox.Checked}});};
        fpsBox.SelectedIndexChanged+=(s,e)=>SavePerformance();aaBox.CheckedChanged+=(s,e)=>SavePerformance();
        languageBox.SelectedIndexChanged+=(s,e)=>{if(syncingOptions)return;UiText.Language=languageBox.SelectedIndex==1?"ja":languageBox.SelectedIndex==2?"en":"zh";ApplyUiLanguage();Send("language",new Dictionary<string,object>{{"language",UiText.Language}});Poll(true);};
    }
    private Dictionary<string,object> ReadSavedSettings()
    {try{return json.Deserialize<Dictionary<string,object>>(ReadShared(Path.Combine(root,"DesktopSettings.json")));}catch{return new Dictionary<string,object>();}}
    private void SavePerformance(){if(!syncingOptions)Send("performance",new Dictionary<string,object>{{"frameRate",Convert.ToInt32(fpsBox.SelectedItem)},{"antiAliasing",aaBox.Checked}});}
    private void ApplyUiLanguage()
    {
        Action<Control> visit=null;visit=c=>{string original;if(!originalTexts.TryGetValue(c,out original)){original=c.Text;originalTexts[c]=original;}if(!(c is ComboBox)&&!(c is TextBox)&&!(c is ListBox)&&!(c is NumericUpDown))c.Text=L(original);foreach(Control child in c.Controls)visit(child);};
        visit(this);
        if(tray.ContextMenuStrip!=null)foreach(ToolStripItem item in tray.ContextMenuStrip.Items){string original;if(!originalMenus.TryGetValue(item,out original)){original=item.Text;originalMenus[item]=original;}item.Text=L(original);}
        groundMode.Items[0]=L("二维直线");groundMode.Items[1]=L("三维四点地面");
        actorLimitBox.AccessibleName=L("模型上限数量");tray.Text=L("UmaWalking · 左键控制面板 / 右键菜单");optionsMessage.Text="";sizeLabel.Text=L("模型大小 ")+modelSize.Value+"%";RefreshChoices();UpdateEntryChoices();
    }
    private void PollOptions()
    {
        if(state==null)return;
        if(!optionsStateInitialized&&state.ContainsKey("frameRate"))
        {
            syncingOptions=true;if(state.ContainsKey("avoid3DCollisions"))avoidanceBox.Checked=Convert.ToBoolean(state["avoid3DCollisions"]);int actual=I(state,"frameRate");fpsBox.SelectedItem=new[]{15,30,60,120}.Contains(actual)?actual:30;
            aaBox.Checked=I(state,"msaaSamples")>1||(state.ContainsKey("useFxaa")&&Convert.ToBoolean(state["useFxaa"]));syncingOptions=false;optionsStateInitialized=true;
        }
        if(state.ContainsKey("activeDataPath")){string path=S(state,"activeDataPath");dataBox.Text=DesktopProfile.IsLegacyPlaceholder(path,root)?DesktopProduct.DefaultDataPath:path;}
        if(pendingRestartId!=null)
        {
            if(S(state,"optionsAck")==pendingRestartId){pendingRestartId=null;Restart();return;}
            if(DateTime.UtcNow-pendingRestartAt>TimeSpan.FromSeconds(15)){pendingRestartId=null;optionsMessage.Text=L("保存目录失败；未重启，原数据仍保留。");}
        }
        if(!cleanupChecked&&!extractionBusy&&pendingRestartId==null&&string.IsNullOrEmpty(S(state,"error"))&&File.Exists(Path.Combine(root,"DesktopLogs","asset-plan.json")))CheckCleanup();
    }
    private void SwitchData(string path)
    {
        pendingRestartId=Guid.NewGuid().ToString("N");pendingRestartAt=DateTime.UtcNow;
        Send("dataPath",new Dictionary<string,object>{{"dataPath",path},{"requestId",pendingRestartId}});
        optionsMessage.Text=L("正在保存目录并重启…");
    }
    private void ChooseData()
    {
        if(extractionBusy)return;
        using(var dialog=new FolderBrowserDialog {Description=L("选择包含 meta、master/master.mdb 和 dat 的数据目录"),SelectedPath=dataBox.Text})
        {
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            string path=dialog.SelectedPath;
            if(!File.Exists(Path.Combine(path,"meta"))||!File.Exists(Path.Combine(path,"master","master.mdb"))||!Directory.Exists(Path.Combine(path,"dat")))
            {optionsMessage.Text=L("目录无效：必须包含 meta、master/master.mdb 和 dat。");return;}
            SwitchData(path);
        }
    }
    private void ExtractData()
    {
        if(extractionBusy)return;
        try
        {
            var plan=DesktopData.ReadPlan(Path.Combine(root,"DesktopLogs","asset-plan.json"));
            if(state==null||!string.IsNullOrEmpty(S(state,"error"))||!string.Equals(DesktopData.Full(plan.source),DesktopData.Full(S(state,"activeDataPath")),StringComparison.OrdinalIgnoreCase))throw new IOException(L("请先选择有效数据目录并等待加载完成。"));
            using(var dialog=new FolderBrowserDialog {Description=L("选择空目录，存放精简素材（不要位于原数据目录内）")})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                string dest=dialog.SelectedPath;
                extractionCancellation=new CancellationTokenSource();extractionBusy=true;extractButton.Enabled=browseButton.Enabled=restartButton.Enabled=trayRestart.Enabled=false;extractionProgress.Value=0;
                var worker=new BackgroundWorker();
                worker.DoWork+=(s,e)=>e.Result=DesktopData.Extract(plan,dest,(percent,file)=>{if(!IsDisposed)BeginInvoke((Action)(()=>{extractionProgress.Value=percent;optionsMessage.Text=L("复制并校验素材：")+percent+"% · "+(plan.bytes/1073741824.0).ToString("F2")+" GiB";}));},extractionCancellation.Token);
                worker.RunWorkerCompleted+=(s,e)=>{
                    extractionBusy=false;extractionCancellation.Dispose();extractionCancellation=null;extractButton.Enabled=browseButton.Enabled=restartButton.Enabled=trayRestart.Enabled=true;
                    if(e.Error!=null){optionsMessage.Text=L("提取未完成，原数据未删除：")+e.Error.Message;return;}
                    string receipt=Path.Combine(root,"DesktopLogs","pending-cleanup.json");File.WriteAllText(receipt,json.Serialize(new Dictionary<string,object>{{"destination",dest},{"source",plan.source}}),new UTF8Encoding(false));
                    cleanupChecked=false;SwitchData(dest);
                };worker.RunWorkerAsync();
            }
        }
        catch(Exception ex){optionsMessage.Text=L("无法提取：")+ex.Message;}
    }
    private void CheckCleanup()
    {
        string receipt=Path.Combine(root,"DesktopLogs","pending-cleanup.json");if(!File.Exists(receipt)){cleanupChecked=true;return;}
        try
        {
            var data=json.Deserialize<Dictionary<string,object>>(ReadShared(receipt));string dest=S(data,"destination"),source=S(data,"source");
            if(!string.Equals(DesktopData.Full(S(state,"activeDataPath")),DesktopData.Full(dest),StringComparison.OrdinalIgnoreCase))return;
            var mini=DesktopData.ReadPlan(Path.Combine(dest,"UmaDesktop-mini-manifest.json"));var active=DesktopData.ReadPlan(Path.Combine(root,"DesktopLogs","asset-plan.json"));
            if(active.characters!=mini.characters||active.costumes!=mini.costumes)throw new IOException(L("精简数据加载结果不一致，保留原数据。"));
            cleanupChecked=true;File.Move(receipt,receipt+".offered-"+DateTime.UtcNow.Ticks);ShowPanel();
            string prompt=L("提取及校验完成，程序已从精简目录成功加载。是否删除原全量包的 dat、meta 和 master/master.mdb？其他文件会保留。")+"\n\n"+source+"\n\n"+L("删除后无法用于完整游戏；未来新增模型需要重新下载全量数据包并重新提取。选择“否”会保留原包。");
            if(MessageBox.Show(this,prompt,L("删除原全量数据？"),MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes){optionsMessage.Text=L("已保留原全量数据。");return;}
            // Avoid a long verification/deletion freeze in the UI. There is no automated Yes path.
            extractionBusy=true;extractButton.Enabled=browseButton.Enabled=restartButton.Enabled=trayRestart.Enabled=false;
            var worker=new BackgroundWorker();worker.DoWork+=(s,e)=>DesktopData.DeleteOriginal(source,dest,mini);
            worker.RunWorkerCompleted+=(s,e)=>{extractionBusy=false;extractButton.Enabled=browseButton.Enabled=restartButton.Enabled=trayRestart.Enabled=true;optionsMessage.Text=e.Error==null?L("原全量素材已删除，精简目录已保留。"):L("删除未完成：")+e.Error.Message;};worker.RunWorkerAsync();
        }
        catch(Exception ex){cleanupChecked=true;optionsMessage.Text=L("保留原全量数据：")+ex.Message;}
    }
    internal string DiagnosticText()
    {
        var b=new StringBuilder("UmaWalking "+WalkingRelease.Version+" diagnostic report / 诊断报告\r\nUTC: "+DateTime.UtcNow.ToString("o")+"\r\nReproduction steps / 复现步骤: [please fill in]\r\n");
        foreach(string name in new[]{"DesktopSettings.json","DesktopLogs/state.json","DesktopLogs/restart-status.json"})
        {string path=Path.Combine(root,name);b.AppendLine("\r\n--- "+name+" ---");try{b.AppendLine(ReadShared(path));}catch(Exception ex){b.AppendLine(ex.GetType().Name);}}
        foreach(string name in new[]{"events.log","Player.log","controller-error.log"})
        {
            b.AppendLine("\r\n--- recent "+name+" (up to 64 KiB) ---");
            try{using(var stream=new FileStream(Path.Combine(root,"DesktopLogs",name),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){if(stream.Length>65536)stream.Seek(-65536,SeekOrigin.End);using(var reader=new StreamReader(stream,Encoding.UTF8)){if(stream.Position>0)reader.ReadLine();b.AppendLine(reader.ReadToEnd());}}}catch(Exception ex){b.AppendLine(ex.GetType().Name);}
        }
        b.AppendLine("\r\n--- UI / extraction ---");b.AppendLine(json.Serialize(new {language=UiText.Language,extractionBusy=extractionBusy,extractionPercent=extractionProgress.Value,selectedFPS=fpsBox.SelectedItem,selectedAA=aaBox.Checked,selectedActorLimit=actorLimitBox.Value,selectedAvoidance=avoidanceBox.Checked,applicationVersion=WalkingRelease.Version,startWithWindows=startupBox.Checked,automaticUpdates=automaticUpdatesBox.Checked,skippedVersion=updatePreferences==null?"":updatePreferences.SkippedVersion,updateCheckStatus=updateStatus.Text,controller64Bit=Environment.Is64BitProcess,logicalProcessors=Environment.ProcessorCount}));
        try{var plan=DesktopData.ReadPlan(Path.Combine(root,"DesktopLogs","asset-plan.json"));b.AppendLine("--- data identity ---");b.AppendLine(json.Serialize(new {metaSha256=plan.metaSha256,masterSha256=plan.masterSha256,characters=plan.characters,costumes=plan.costumes,files=plan.files.Length,bytes=plan.bytes,miniDataset=File.Exists(Path.Combine(dataBox.Text,"UmaDesktop-mini-manifest.json"))}));}catch{}
        foreach(string file in new[]{"UmaDesktopController.exe","UmaDesktop_Data/Managed/umamusume.dll"})try{b.AppendLine(file+" SHA256="+DesktopData.Hash(Path.Combine(root,file)));}catch{}
        string text=b.ToString();
        foreach(string path in new[]{root,dataBox.Text,Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)})if(!string.IsNullOrEmpty(path))
        {text=Regex.Replace(text,Regex.Escape(path.Replace("\\","\\\\")),"[local-path]",RegexOptions.IgnoreCase);text=Regex.Replace(text,Regex.Escape(path),"[local-path]",RegexOptions.IgnoreCase);}
        text=Regex.Replace(text,@"[A-Za-z]:[\\/][^\r\n""<>]*","[local-path]");
        if(!string.IsNullOrEmpty(Environment.UserName))text=Regex.Replace(text,Regex.Escape(Environment.UserName),"[user]",RegexOptions.IgnoreCase);
        return text;
    }
    private void CopyDiagnostics()
    {
        try{Poll(true);string report=DiagnosticText();Clipboard.SetText(report);optionsMessage.Text=L("状态与日志已拷贝。提交前请补充复现步骤并检查内容。");}
        catch(Exception ex){try{string path=Path.Combine(root,"DesktopLogs","diagnostic-report.txt");File.WriteAllText(path,DiagnosticText(),new UTF8Encoding(false));optionsMessage.Text=L("剪贴板不可用，诊断已保存到：")+path;}catch{optionsMessage.Text=L("无法导出诊断：")+ex.Message;}}
    }
}

