using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using UmaDesktop;

internal sealed partial class ControlPanel
{
    internal static void RunReleaseTests(string root)
    {
        string prior=Environment.GetEnvironmentVariable("UMA_WALKING_PROFILE");
        Environment.SetEnvironmentVariable("UMA_WALKING_PROFILE",Path.Combine(root,"isolated-user-profile"));
        int checks=0;Action<bool,string> check=(value,name)=>{if(!value)throw new Exception(name);checks++;};
        try
        {
            var currentVersion=WalkingRelease.ParseVersion(WalkingRelease.Version);check(currentVersion!=null,"Public semantic version");
            string newer=new Version(currentVersion.Major,currentVersion.Minor,currentVersion.Build+1).ToString();
            string newerAgain=new Version(currentVersion.Major,currentVersion.Minor,currentVersion.Build+2).ToString();
            foreach(string tag in new[]{"v1.0.1","1.0.10","v1.10.0","v2.0.0","v1.0.1+build.1"})check(WalkingRelease.ParseVersion(tag)>WalkingRelease.ParseVersion("1.0.0"),"Semantic comparison "+tag);
            foreach(string tag in new[]{"1","v1.0","1.0.0-beta","-1.0.0","v1.0.0.1","v99999999999999.0.0","v1.0.1/evil"})check(WalkingRelease.ParseVersion(tag)==null,"Reject malformed/preview version "+tag);
            foreach(string url in new[]{"http://github.com/ShiFangming0823/UmaWalking/releases/tag/v1.0.1","https://github.com.evil.test/ShiFangming0823/UmaWalking/releases/tag/v1.0.1","https://github.com/other/repo/releases/tag/v1.0.1","https://github.com:444/ShiFangming0823/UmaWalking/releases/tag/v1.0.1","https://user:pass@github.com/ShiFangming0823/UmaWalking/releases/tag/v1.0.1"})check(!WalkingRelease.SafeReleaseUrl(url),"Reject external URL");
            string payload=new JavaScriptSerializer().Serialize(new {tag_name="v"+newer,name="Version 1.0.1",body="Fixed a bug.\n修复问题。\n不具合修正。",html_url=WalkingRelease.Repository+"/releases/tag/v"+newer,draft=false,prerelease=false});
            var release=WalkingRelease.Parse(payload);check(release.Notes.Contains("修复"),"Unicode release notes");
            check(WalkingRelease.Parse(payload.Replace("\"prerelease\":false","\"prerelease\":true"))==null,"Ignore prerelease");
            check(WalkingRelease.Parse(payload.Replace("\"draft\":false","\"draft\":true"))==null,"Ignore draft");
            var prefs=new UpdatePreferences();check(WalkingRelease.ShouldOffer(release,prefs,false),"Default automatic notification");
            prefs.SkippedVersion=newer;check(!WalkingRelease.ShouldOffer(release,prefs,false)&&WalkingRelease.ShouldOffer(release,prefs,true),"Manual bypasses skipped version");
            prefs.AutomaticUpdates=false;check(!WalkingRelease.ShouldOffer(release,prefs,false)&&WalkingRelease.ShouldOffer(release,prefs,true),"Manual bypasses all skips");
            WalkingPreferences.Save(prefs);check(!WalkingPreferences.Load().AutomaticUpdates&&WalkingPreferences.Load().SkippedVersion==newer,"Update preference persistence");
            var next=WalkingRelease.Parse(payload.Replace("v"+newer,"v"+newerAgain));prefs.AutomaticUpdates=true;check(WalkingRelease.ShouldOffer(next,prefs,false),"Skipped version does not suppress the next version");
            var current=WalkingRelease.Parse(payload.Replace("v"+newer,"v"+WalkingRelease.Version));check(!WalkingRelease.ShouldOffer(current,prefs,true),"No current-version update");
            var previous=WalkingRelease.Parse(payload.Replace("v"+newer,"v0.0.0"));check(!WalkingRelease.ShouldOffer(previous,prefs,true),"No downgrade");
            check(WalkingStartup.Command(Path.Combine(root,"path with spaces")).StartsWith("\""),"Quoted startup executable");
            check(WalkingStartup.Command(root).Contains("--umawalking-autostart")&&WalkingStartup.Command(root).Contains("-force-d3d11"),"Startup flags");
            int startupChanges=0,offers=0,fetches=0;bool fakeStartup=false;string opened="";
            using(var panel=new ControlPanel(root,0,true,()=>{}))
            {
                panel.Show();panel.Poll(true);panel.tabs.SelectedIndex=1;Application.DoEvents();
                check(panel.dataBox.Text==DesktopProduct.DefaultDataPath,"Missing or legacy prefill uses generic DMM default");
                panel.state["activeDataPath"]=Path.Combine(root,"GameData");panel.PollOptions();
                check(panel.dataBox.Text==DesktopProduct.DefaultDataPath,"Old runtime placeholder cannot overwrite default UI");
                panel.state["activeDataPath"]=Path.Combine(root,"Chosen-Offline-Data");panel.PollOptions();
                check(panel.dataBox.Text.EndsWith("Chosen-Offline-Data"),"Explicit custom path retained in UI");panel.state.Remove("activeDataPath");
                panel.setStartup=value=>{startupChanges++;fakeStartup=value;};panel.getStartup=()=>fakeStartup;
                panel.syncingRelease=true;panel.startupBox.Checked=false;panel.syncingRelease=false;
                panel.startupBox.Checked=true;check(startupChanges==1&&fakeStartup,"Startup checkbox enables through service");
                panel.startupBox.Checked=false;check(startupChanges==2&&!fakeStartup,"Startup checkbox disables through service");
                panel.setStartup=value=>{throw new UnauthorizedAccessException("fixture");};panel.startupBox.Checked=true;
                check(!panel.startupBox.Checked&&!fakeStartup,"Failed registry change rolls back checkbox");
                panel.openWeb=url=>opened=url;panel.OpenProjectPage(WalkingRelease.Issues);check(opened.EndsWith("/issues"),"Issue target");
                panel.fetchRelease=()=>{fetches++;return Task.FromResult(release);};panel.chooseUpdate=r=>{offers++;return UpdateChoice.SkipVersion;};
                panel.updatePreferences=new UpdatePreferences();panel.CheckForUpdates(false);
                check(offers==1&&panel.updatePreferences.SkippedVersion==newer,"Automatic skip current");
                panel.CheckForUpdates(false);check(offers==1,"Repeated automatic skipped version is suppressed");
                panel.chooseUpdate=r=>{offers++;return UpdateChoice.SkipAll;};panel.CheckForUpdates(true);
                check(offers==2&&!panel.updatePreferences.AutomaticUpdates&&!panel.automaticUpdatesBox.Checked,"Manual check opens skipped release and can disable future checks");
                panel.CheckForUpdates(false);check(offers==2,"All-skipped automatic offer suppressed");
                panel.chooseUpdate=r=>{offers++;return UpdateChoice.Download;};panel.CheckForUpdates(true);
                check(offers==3&&opened==release.Url,"Manual check after skip-all opens release URL");
                panel.automaticUpdatesBox.Checked=true;check(WalkingPreferences.Load().AutomaticUpdates,"Automatic checks can be re-enabled");
                var pending=new TaskCompletionSource<ReleaseInfo>();panel.fetchRelease=()=>{fetches++;return pending.Task;};int before=fetches;
                panel.CheckForUpdates(true);panel.CheckForUpdates(true);check(fetches==before+1&&!panel.updateButton.Enabled,"Concurrent checks coalesced without blocking UI");
                pending.SetException(new IOException("fixture network unavailable"));for(int i=0;i<50&&panel.updateBusy;i++){Application.DoEvents();System.Threading.Thread.Sleep(5);}
                check(!panel.updateBusy&&panel.updateButton.Enabled,"Network failure restores controls");
                panel.fetchRelease=()=>Task.FromResult<ReleaseInfo>(null);panel.CheckForUpdates(true);check(!panel.updateBusy,"404/no-public-release handled");
                foreach(string language in new[]{"zh","en","ja"})
                {
                    UiText.Language=language;panel.ApplyUiLanguage();panel.tabs.SelectedIndex=1;Application.DoEvents();panel.PerformLayout();
                    check(panel.startupBox.Text==UiText.Get("随系统启动"),"Startup translation "+language);
                    check(panel.updateButton.Text==UiText.Get("检查更新"),"Update translation "+language);
                    check(panel.startupBox.Width>100&&panel.updateButton.Width>70,"Release controls visible "+language);
                    using(var image=new Bitmap(panel.Width,panel.Height)){panel.DrawToBitmap(image,new Rectangle(0,0,panel.Width,panel.Height));image.Save(Path.Combine(root,"release-options-"+language+".png"));}
                    using(var dialog=new WalkingUpdateDialog(release)){dialog.Show();dialog.PerformLayout();Application.DoEvents();check(dialog.Controls.Count==1&&dialog.ClientSize.Width>=650,"Update dialog layout "+language);dialog.Close();}
                }
                check(panel.DiagnosticText().Contains("UmaWalking "+WalkingRelease.Version),"Diagnostic public version");
            }
            File.WriteAllText(Path.Combine(root,"release-selftest-report.json"),new JavaScriptSerializer().Serialize(new {Passed=true,Checks=checks,StartupRegistryModified=false,NetworkRequestsSent=false,Scope="Release parsing, semantic versions, update choices, nonblocking checks, injected startup service, localized real WinForms controls."}));
        }
        finally{Environment.SetEnvironmentVariable("UMA_WALKING_PROFILE",prior);}
    }
}
