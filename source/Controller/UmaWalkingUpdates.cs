using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;
using UmaDesktop;

[assembly: AssemblyVersion(DesktopProduct.Version+".0")]
[assembly: AssemblyFileVersion(DesktopProduct.Version+".0")]
[assembly: AssemblyProduct("UmaWalking")]
[assembly: AssemblyInformationalVersion(DesktopProduct.Version)]

internal static class WalkingRelease
{
    internal const string Version=DesktopProduct.Version;
    internal const string Repository="https://github.com/ShiFangming0823/UmaWalking";
    internal const string Issues=Repository+"/issues";
    internal const string Latest=Repository+"/releases/latest";
    internal const string Api="https://api.github.com/repos/ShiFangming0823/UmaWalking/releases/latest";
    internal static Version ParseVersion(string value)
    {
        var match=Regex.Match(value??"",@"^[vV]?(\d+)\.(\d+)\.(\d+)(?:\+[A-Za-z0-9.-]+)?$");
        int major,minor,patch;
        if(!match.Success||!int.TryParse(match.Groups[1].Value,out major)||!int.TryParse(match.Groups[2].Value,out minor)||!int.TryParse(match.Groups[3].Value,out patch))return null;
        return new Version(major,minor,patch);
    }
    internal static bool SafeReleaseUrl(string value)
    {
        Uri uri;return Uri.TryCreate(value,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)&&uri.IsDefaultPort&&string.IsNullOrEmpty(uri.UserInfo)
            &&uri.AbsolutePath.StartsWith("/ShiFangming0823/UmaWalking/releases/tag/",StringComparison.OrdinalIgnoreCase);
    }
    internal static ReleaseInfo Parse(string data)
    {
        var json=new JavaScriptSerializer {MaxJsonLength=2*1024*1024};var item=json.Deserialize<Dictionary<string,object>>(data);
        if(item==null)throw new InvalidDataException("Empty release response.");
        if((item.ContainsKey("draft")&&Convert.ToBoolean(item["draft"]))||(item.ContainsKey("prerelease")&&Convert.ToBoolean(item["prerelease"])))return null;
        Func<string,string> read=key=>item.ContainsKey(key)&&item[key]!=null?Convert.ToString(item[key]):"";
        var version=ParseVersion(read("tag_name"));if(version==null)throw new InvalidDataException("Release tag must use vMAJOR.MINOR.PATCH.");
        string url=read("html_url");if(!SafeReleaseUrl(url))throw new InvalidDataException("Unexpected release URL.");
        return new ReleaseInfo {Tag=read("tag_name"),Version=version,Title=read("name"),Notes=read("body"),Url=url};
    }
    internal static async Task<ReleaseInfo> Fetch()
    {
        ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
        return await Task.Run(()=>{
            var request=(HttpWebRequest)WebRequest.Create(Api);request.UserAgent="UmaWalking/"+Version;
            request.Accept="application/vnd.github+json";request.Headers["X-GitHub-Api-Version"]="2022-11-28";
            request.Timeout=10000;request.ReadWriteTimeout=10000;request.AllowAutoRedirect=false;
            request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
            try
            {
                using(var response=(HttpWebResponse)request.GetResponse())
                using(var stream=response.GetResponseStream())
                using(var reader=new StreamReader(stream,Encoding.UTF8))
                {
                    var result=new StringBuilder();char[] buffer=new char[4096];int length;
                    while((length=reader.Read(buffer,0,buffer.Length))>0){result.Append(buffer,0,length);if(result.Length>2*1024*1024)throw new InvalidDataException("Release response is too large.");}
                    return Parse(result.ToString());
                }
            }
            catch(WebException ex)
            {
                var response=ex.Response as HttpWebResponse;
                if(response!=null){using(response){if(response.StatusCode==HttpStatusCode.NotFound)return null;}}
                throw;
            }
        });
    }
    internal static bool ShouldOffer(ReleaseInfo release,UpdatePreferences preferences,bool manual)
    {return release!=null&&release.Version>ParseVersion(Version)&&(manual||(preferences.AutomaticUpdates&&preferences.SkippedVersion!=release.Version.ToString()));}
}
internal sealed class ReleaseInfo
{internal string Tag,Title,Notes,Url;internal Version Version;}
public sealed class UpdatePreferences
{public bool AutomaticUpdates=true;public string SkippedVersion="";}
internal static class WalkingPreferences
{
    internal static string FilePath {get{return Path.Combine(DesktopProfile.DirectoryPath,"UserPreferences.json");}}
    internal static UpdatePreferences Load()
    {
        foreach(string file in new[]{FilePath,FilePath+".bak"})
        {try{if(File.Exists(file))return new JavaScriptSerializer().Deserialize<UpdatePreferences>(File.ReadAllText(file))??new UpdatePreferences();}catch{}}
        return new UpdatePreferences();
    }
    internal static void Save(UpdatePreferences value)
    {DesktopProfile.AtomicWrite(FilePath,new JavaScriptSerializer().Serialize(value),true);}
}
internal static class WalkingStartup
{
    internal const string RegistryPath=@"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string Name="UmaWalking";
    internal static string Command(string root)
    {return DesktopRestart.Quote(Path.Combine(Path.GetFullPath(root),"UmaDesktop.exe"))+" -force-d3d11 -force-d3d11-bitblt-model --umawalking-autostart -logFile "+DesktopRestart.Quote(Path.Combine(Path.GetFullPath(root),"DesktopLogs","Player.log"));}
    internal static bool Enabled()
    {using(var key=Registry.CurrentUser.OpenSubKey(RegistryPath)) {string value=key==null?"":Convert.ToString(key.GetValue(Name,""));return value.Contains("--umawalking-autostart");}}
    internal static void Set(string root,bool enabled)
    {
        using(var key=Registry.CurrentUser.CreateSubKey(RegistryPath))
        {if(enabled)key.SetValue(Name,Command(root),RegistryValueKind.String);else key.DeleteValue(Name,false);}
    }
}
internal enum UpdateChoice {Later,Download,SkipVersion,SkipAll}
internal sealed class WalkingUpdateDialog : Form
{
    internal UpdateChoice Choice=UpdateChoice.Later;
    internal WalkingUpdateDialog(ReleaseInfo release)
    {
        Text=UiText.Get("发现新版本")+" · UmaWalking "+release.Version;StartPosition=FormStartPosition.CenterParent;
        Font=new Font("Microsoft YaHei UI",9.5f);ClientSize=new Size(790,510);MinimumSize=new Size(650,420);ShowInTaskbar=false;
        var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(18),RowCount=3,ColumnCount=1};
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(layout);
        layout.Controls.Add(new Label {Text="UmaWalking "+WalkingRelease.Version+" → "+release.Version+"\r\n"+release.Title,AutoSize=true,MaximumSize=new Size(730,0),Margin=new Padding(0,0,0,12)},0,0);
        layout.Controls.Add(new TextBox {Text=string.IsNullOrWhiteSpace(release.Notes)?UiText.Get("此版本未提供更新说明。"):release.Notes,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,BackColor=SystemColors.Window},0,1);
        var buttons=new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=true,Margin=new Padding(0,12,0,0)};layout.Controls.Add(buttons,0,2);
        foreach(var pair in new[]{new KeyValuePair<string,UpdateChoice>("前往下载更新",UpdateChoice.Download),new KeyValuePair<string,UpdateChoice>("跳过当前版本",UpdateChoice.SkipVersion),new KeyValuePair<string,UpdateChoice>("跳过所有更新",UpdateChoice.SkipAll),new KeyValuePair<string,UpdateChoice>("稍后再说",UpdateChoice.Later)})
        {var choice=pair.Value;var button=new Button {Text=UiText.Get(pair.Key),AutoSize=true,Padding=new Padding(8,5,8,5),Margin=new Padding(0,0,8,4)};button.Click+=(s,e)=>{Choice=choice;DialogResult=DialogResult.OK;Close();};buttons.Controls.Add(button);}
    }
}
internal sealed partial class ControlPanel
{
    private readonly CheckBox startupBox=new CheckBox {AutoSize=true};
    private readonly CheckBox automaticUpdatesBox=new CheckBox {AutoSize=true};
    private readonly Label updateStatus=new Label {AutoSize=true,MaximumSize=new Size(560,0)};
    private Button updateButton;private UpdatePreferences updatePreferences;private bool updateBusy,syncingRelease;
    private Func<Task<ReleaseInfo>> fetchRelease=WalkingRelease.Fetch;
    private Action<string> openWeb=url=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
    private Action<bool> setStartup;private Func<bool> getStartup=WalkingStartup.Enabled;
    private Func<ReleaseInfo,UpdateChoice> chooseUpdate;
    private void AddReleaseOptions(FlowLayoutPanel layout)
    {
        setStartup=enabled=>WalkingStartup.Set(root,enabled);
        chooseUpdate=release=>{using(var dialog=new WalkingUpdateDialog(release)){ShowPanel();dialog.ShowDialog(this);return dialog.Choice;}};
        layout.Controls.Add(new Label {Text="UmaWalking "+WalkingRelease.Version,AutoSize=true,Font=new Font(Font.FontFamily,14,FontStyle.Bold),Margin=new Padding(0,0,0,12)});
        startupBox.Text="随系统启动";automaticUpdatesBox.Text="启动时自动检查更新";
        syncingRelease=true;try{startupBox.Checked=getStartup();}catch{}updatePreferences=WalkingPreferences.Load();automaticUpdatesBox.Checked=updatePreferences.AutomaticUpdates;syncingRelease=false;
        layout.Controls.Add(startupBox);layout.Controls.Add(automaticUpdatesBox);
        updateButton=Button("检查更新",()=>CheckForUpdates(true));var row=new FlowLayoutPanel {AutoSize=true};row.Controls.Add(updateButton);
        var releasesLink=new LinkLabel {Text="GitHub Releases",AutoSize=true,Margin=new Padding(8,10,0,0)};releasesLink.LinkClicked+=(s,e)=>OpenProjectPage(WalkingRelease.Latest);row.Controls.Add(releasesLink);layout.Controls.Add(row);layout.Controls.Add(updateStatus);
        layout.Controls.Add(new Label {Text="更新只打开下载页面；设置独立保存，换版本后会继承。",AutoSize=true,MaximumSize=new Size(560,0),Margin=new Padding(0,4,0,20)});
        startupBox.CheckedChanged+=(s,e)=>{
            if(syncingRelease)return;try{setStartup(startupBox.Checked);optionsMessage.Text=L("开机启动设置已保存。");}
            catch(Exception ex){syncingRelease=true;try{startupBox.Checked=getStartup();}catch{startupBox.Checked=false;}syncingRelease=false;optionsMessage.Text=L("无法设置开机启动：")+ex.Message;}
        };
        automaticUpdatesBox.CheckedChanged+=(s,e)=>{if(syncingRelease)return;var previous=updatePreferences.AutomaticUpdates;updatePreferences.AutomaticUpdates=automaticUpdatesBox.Checked;
            try{WalkingPreferences.Save(updatePreferences);}catch(Exception ex){updatePreferences.AutomaticUpdates=previous;SyncUpdateOptions();optionsMessage.Text=ex.Message;}};
        if(parent>0)
        {
            // Preserve an enabled startup entry when a user switches to a new
            // release directory; this does not enable startup for a new user.
            try{if(startupBox.Checked)setStartup(true);}catch{}
            Shown+=(s,e)=>{if(updatePreferences.AutomaticUpdates)BeginInvoke((Action)(()=>CheckForUpdates(false)));};
        }
    }
    private void AddIssueLink(FlowLayoutPanel layout)
    {var link=new LinkLabel {Text="GitHub Issues · 提交问题",AutoSize=true,Margin=new Padding(0,8,0,8)};link.LinkClicked+=(s,e)=>OpenProjectPage(WalkingRelease.Issues);layout.Controls.Add(link);}
    private void OpenProjectPage(string url)
    {try{openWeb(url);}catch(Exception ex){optionsMessage.Text=L("无法打开浏览器：")+ex.Message;}}
    private void SyncUpdateOptions()
    {syncingRelease=true;automaticUpdatesBox.Checked=updatePreferences.AutomaticUpdates;syncingRelease=false;}
    private async void CheckForUpdates(bool manual)
    {
        if(updateBusy)return;updateBusy=true;updateButton.Enabled=false;updateStatus.Text=L("正在检查更新…");
        try
        {
            var release=await fetchRelease();if(IsDisposed||Disposing)return;
            if(release==null){updateStatus.Text=L("暂无公开的正式版本，或仓库暂不可访问。");return;}
            if(!WalkingRelease.ShouldOffer(release,updatePreferences,manual))
            {updateStatus.Text=release.Version>WalkingRelease.ParseVersion(WalkingRelease.Version)?L("已按你的偏好跳过更新；可随时手动检查。"):L("当前已是最新版本。");return;}
            updateStatus.Text=L("发现新版本")+" "+release.Version;
            var choice=chooseUpdate(release);ApplyUpdateChoice(release,choice);
        }
        catch(Exception ex){if(!IsDisposed)updateStatus.Text=L("检查更新失败，请稍后重试或打开 Releases 页面。")+(manual?" "+ex.Message:"");}
        finally{updateBusy=false;if(!IsDisposed)updateButton.Enabled=true;}
    }
    private void ApplyUpdateChoice(ReleaseInfo release,UpdateChoice choice)
    {
        if(choice==UpdateChoice.Download){if(!WalkingRelease.SafeReleaseUrl(release.Url))throw new InvalidDataException("Unexpected release URL.");OpenProjectPage(release.Url);return;}
        if(choice!=UpdateChoice.SkipVersion&&choice!=UpdateChoice.SkipAll)return;
        var previous=new UpdatePreferences {AutomaticUpdates=updatePreferences.AutomaticUpdates,SkippedVersion=updatePreferences.SkippedVersion};
        if(choice==UpdateChoice.SkipVersion)updatePreferences.SkippedVersion=release.Version.ToString();else updatePreferences.AutomaticUpdates=false;
        try{WalkingPreferences.Save(updatePreferences);}catch{updatePreferences=previous;throw;}
        SyncUpdateOptions();updateStatus.Text=L("已按你的偏好跳过更新；可随时手动检查。");
    }
}
