using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using UmaDesktop;

internal sealed partial class ControlPanel
{
    internal static void RunDataSwitch(string root,int parent,string destination)
    {using(var panel=new ControlPanel(root,parent,true)){panel.Shown+=(s,e)=>panel.SwitchData(destination);Application.Run(panel);}}
    internal static void RunOptionsTests(string root)
    {
        int checks=0;Action<bool,string> check=(value,name)=>{if(!value)throw new Exception(name);checks++;};
        using(var panel=new ControlPanel(root,0,true,()=>{}))
        {
            panel.Show();panel.Poll(true);Application.DoEvents();
            check(panel.Text=="UmaWalking","Panel title is UmaWalking");
            check(!panel.avoidanceBox.Checked,"Experimental avoidance defaults unchecked");
            check(panel.actorLimitBox.Minimum==1&&panel.actorLimitBox.Maximum==10,"Model limit range 1 through 10");
            for(int limit=1;limit<=10;limit++)
            {
                panel.actorLimitBox.Value=limit;panel.dirtyActorLimit=true;panel.FlushActorLimit();var cap=panel.LatestCommand();
                check(S(cap,"action")=="actorLimit"&&I(cap,"maxActors")==limit,"Model limit command "+limit);
                panel.state["maxActors"]=limit==10?1:limit+1;panel.PollActorLimit();
                check(Convert.ToInt32(panel.actorLimitBox.Value)==limit,"Old snapshot cannot overwrite requested limit");
                panel.state["maxActors"]=limit;panel.PollActorLimit();check(!panel.awaitingActorLimit,"Applied limit acknowledgment "+limit);
            }
            panel.state["frameRate"]=15;panel.state["msaaSamples"]=1;panel.state["useFxaa"]=false;panel.optionsStateInitialized=false;panel.PollOptions();
            check(Convert.ToInt32(panel.fpsBox.SelectedItem)==15&&!panel.aaBox.Checked,"Initial panel reflects active command-line performance overrides");
            foreach(bool enabled in new[]{true,false}) {panel.avoidanceBox.Checked=enabled;var option=panel.LatestCommand();check(S(option,"action")=="avoidance"&&Convert.ToBoolean(option["avoid3DCollisions"])==enabled,"Avoidance checkbox command");}
            panel.state.Remove("frameRate");panel.state.Remove("msaaSamples");panel.state.Remove("useFxaa");
            check(panel.fpsBox.Items.Cast<int>().SequenceEqual(new[]{15,30,60,120}),"FPS choices");
            foreach(int fps in new[]{15,60,120,30})
            {panel.fpsBox.SelectedItem=fps;panel.SavePerformance();var c=panel.LatestCommand();check(S(c,"action")=="performance"&&I(c,"frameRate")==fps,"FPS command "+fps);}
            foreach(bool aa in new[]{false,true}){panel.aaBox.Checked=aa;panel.SavePerformance();check(Convert.ToBoolean(panel.LatestCommand()["antiAliasing"])==aa,"AA command");}
            foreach(var language in new[]{"en","ja","zh"})
            {
                panel.languageBox.SelectedIndex=language=="ja"?1:language=="en"?2:0;Application.DoEvents();
                check(UiText.Language==language,"Language selection");check(panel.Text=="UmaWalking","Brand does not change with language");check(panel.actorLimitBox.Value==10,"Language preserves numeric model limit");check(panel.avoidanceBox.Text==UiText.Get("三维地面角色避让（实验性）"),"Localized avoidance switch");check(panel.tabs.TabPages[1].Text==UiText.Get("设置与诊断"),"Localized tab");
                check(S(panel.LatestCommand(),"language")==language,"Language persistence command");
                panel.tabs.SelectedIndex=1;Application.DoEvents();panel.PerformLayout();
                check(panel.fpsBox.Width>100&&panel.extractButton.Visible,"Options controls visible");
                check(panel.fpsBox.Text=="30"&&!string.IsNullOrEmpty(panel.languageBox.Text),"Selected option text remains visible");
                using(var image=new Bitmap(panel.Width,panel.Height)){panel.DrawToBitmap(image,new Rectangle(0,0,panel.Width,panel.Height));image.Save(Path.Combine(root,"options-"+language+".png"));}
            }
            panel.SwitchData(Path.Combine(root,"mock-data"));check(S(panel.LatestCommand(),"action")=="dataPath"&&S(panel.LatestCommand(),"requestId").Length==32,"Directory switch uses acknowledgment");panel.pendingRestartId=null;
            string report=panel.DiagnosticText();check(report.Contains("state.json")&&report.Contains("Reproduction")&&report.Contains("SHA256="),"Diagnostics includes state and hashes");
            check(!report.Contains(root)&&!report.Contains(root.Replace("\\","\\\\"))&&!report.Contains(Environment.UserName),"Diagnostics masks paths and username");
            File.WriteAllText(Path.Combine(root,"diagnostic-preview.txt"),report,new UTF8Encoding(false));
        }
        string area=Path.Combine(root,"data-test-"+Guid.NewGuid().ToString("N")),source=Path.Combine(area,"source"),destination=Path.Combine(area,"mini");Directory.CreateDirectory(Path.Combine(source,"master"));
        string hashName=new string('a',32),relative="dat/aa/"+hashName;Directory.CreateDirectory(Path.Combine(source,"dat","aa"));
        File.WriteAllText(Path.Combine(source,"meta"),"fixture index");File.WriteAllText(Path.Combine(source,"master","master.mdb"),"fixture master");File.WriteAllBytes(Path.Combine(source,relative.Replace('/',Path.DirectorySeparatorChar)),Enumerable.Range(0,1048577).Select(i=>(byte)i).ToArray());
        var plan=new DesktopAssetPlan {source=source,characters=1,costumes=1,files=new[]{"meta","master/master.mdb",relative}.Select(p=>new DesktopAssetFile {path=p,bytes=new FileInfo(Path.Combine(source,p.Replace('/',Path.DirectorySeparatorChar))).Length}).ToArray(),metaSha256=DesktopData.Hash(Path.Combine(source,"meta")),masterSha256=DesktopData.Hash(Path.Combine(source,"master","master.mdb"))};
        Action<Action,string> rejects=(action,name)=>{bool rejected=false;try{action();}catch{rejected=true;}check(rejected,name);};
        foreach(string invalid in new[]{"../outside","dat/aa/../../escape","C:/escape","master/other","dat/aa/"+hashName+"/file"})rejects(()=>DesktopData.Resolve(source,invalid),"Unsafe path "+invalid);
        rejects(()=>DesktopData.Extract(plan,Path.Combine(source,"child"),(p,f)=>{},CancellationToken.None),"Nested target rejected");
        rejects(()=>DesktopData.Extract(plan,area,(p,f)=>{},CancellationToken.None),"Parent target rejected");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();rejects(()=>DesktopData.Extract(plan,destination,(p,f)=>{},cancel.Token),"Cancelled extraction");}
        check(!Directory.Exists(destination),"Cancellation preserves empty destination and source");
        int progress=0;DesktopData.Extract(plan,destination,(p,f)=>progress=p,CancellationToken.None);DesktopData.Verify(destination,plan);
        check(progress==100&&File.Exists(Path.Combine(destination,"UmaDesktop-mini-manifest.json")),"Verified manifest and progress");
        check(File.Exists(Path.Combine(source,"meta"))&&Directory.Exists(Path.Combine(source,"dat")),"Extraction never deletes source");
        rejects(()=>DesktopData.Extract(plan,destination,(p,f)=>{},CancellationToken.None),"Nonempty target rejected");
        rejects(()=>DesktopData.DeleteOriginal(source,source,plan),"Deletion of active source as destination rejected");
        File.AppendAllText(Path.Combine(destination,"meta"),"corrupt");rejects(()=>DesktopData.DeleteOriginal(source,destination,plan),"Corrupt target prevents original deletion");
        check(File.Exists(Path.Combine(source,"meta")),"Original survives bad target");File.Copy(Path.Combine(source,"meta"),Path.Combine(destination,"meta"),true);
        File.WriteAllText(Path.Combine(source,"unrelated-settings.txt"),"keep me");DesktopData.DeleteOriginal(source,destination,plan);
        check(!Directory.Exists(Path.Combine(source,"dat"))&&!File.Exists(Path.Combine(source,"meta"))&&File.Exists(Path.Combine(source,"unrelated-settings.txt")),"Confirmed fixture cleanup deletes only assets");DesktopData.Verify(destination,plan);
        File.WriteAllText(Path.Combine(root,"options-selftest-report.json"),new JavaScriptSerializer().Serialize(new {Passed=true,Checks=checks,Scope="Real control handlers and disposable dataset copy/verification/deletion guards. No original game assets deleted."}),new UTF8Encoding(false));
    }
    internal static void RunExtraction(string root,string planFile,string destination)
    {
        var plan=DesktopData.ReadPlan(planFile);DateTime start=DateTime.UtcNow;
        DesktopData.Extract(plan,destination,(p,f)=>{},CancellationToken.None);DesktopData.Verify(destination,plan);
        File.WriteAllText(Path.Combine(root,"extraction-report.json"),new JavaScriptSerializer().Serialize(new {Passed=true,Files=plan.files.Length,Bytes=plan.bytes,Characters=plan.characters,Costumes=plan.costumes,ElapsedSeconds=(DateTime.UtcNow-start).TotalSeconds,OriginalDataDeleted=false,AllCopiedFilesSha256Verified=true}),new UTF8Encoding(false));
    }
}
