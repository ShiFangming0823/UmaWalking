using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

internal static class DesktopRestart
{
    internal static string Quote(string argument)
    {
        var b=new StringBuilder("\"");int slashes=0;
        foreach(char c in argument)
        {
            if(c=='\\'){slashes++;continue;}
            b.Append('\\',c=='\"'?slashes*2+1:slashes);b.Append(c);slashes=0;
        }
        b.Append('\\',slashes*2);b.Append('"');return b.ToString();
    }
    internal static void Start(string root,int parent,int controller,int percent,int actorLimit)
    {
        if(parent<=0)throw new InvalidOperationException(UiText.Get("没有关联的主程序进程，无法重启。"));
        using(var current=Process.GetCurrentProcess())
        {
            string[] args={"--restart","--root",root,"--parent",parent.ToString(),"--controller",controller.ToString(),
                "--controller-start",current.StartTime.ToUniversalTime().Ticks.ToString(),"--size-percent",percent.ToString(),"--actor-limit",actorLimit.ToString()};
            Process.Start(new ProcessStartInfo(Path.Combine(root,"UmaDesktopController.exe"),string.Join(" ",args.Select(Quote)))
                {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=root});
        }
    }
    private static string Value(string[] args,string key,string fallback="")
    {int index=Array.IndexOf(args,key);return index>=0 && index+1<args.Length?args[index+1]:fallback;}
    internal static void Run(string[] args)
    {
        string root=Path.GetFullPath(Value(args,"--root"));Directory.CreateDirectory(Path.Combine(root,"DesktopLogs"));
        string status=Path.Combine(root,"DesktopLogs","restart-status.json");var json=new JavaScriptSerializer();
        try
        {
            string identity;
            using(var sha=SHA256.Create())identity=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(root.ToUpperInvariant()))).Replace("-","");
            using(var gate=new Mutex(false,"Local\\UmaDesktop.Restart."+identity))
            {
                bool entered=false;
                try {try{entered=gate.WaitOne(0);}catch(AbandonedMutexException){entered=true;}
                    if(!entered)return;
                    int parent=int.Parse(Value(args,"--parent")),owner=int.Parse(Value(args,"--controller","0"));
                    long ownerStart=long.Parse(Value(args,"--controller-start","0"));
                    var launch=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(root,"DesktopLogs","launch.json"),Encoding.UTF8));
                    string executable=Path.Combine(root,"UmaDesktop.exe"),control=Path.Combine(root,"UmaDesktopController.exe");
                    if(Convert.ToInt32(launch["processId"])!=parent || !SamePath(Convert.ToString(launch["executable"]),executable))
                        throw new InvalidOperationException(UiText.Get("重启记录与目标主程序不匹配。"));
                    if(!File.Exists(executable) || !File.Exists(control))throw new FileNotFoundException(UiText.Get("重启所需程序文件缺失。"));
                    string[] arguments=((IEnumerable)launch["arguments"]).Cast<object>().Select(Convert.ToString).ToArray();
                    using(var target=CheckedProcess(parent,executable,Convert.ToInt64(launch["startUtcTicks"])))
                    using(var controller=owner>0?CheckedProcess(owner,control,ownerStart):null)
                    {
                        AtomicWrite(status,json.Serialize(new {stage="Stopping",oldProcessId=parent}));
                        if(target!=null && !target.HasExited){target.Kill();if(!target.WaitForExit(5000))throw new TimeoutException(UiText.Get("旧主程序尚未结束，未启动第二个实例。"));}
                        if(controller!=null && !controller.HasExited){controller.Kill();if(!controller.WaitForExit(5000))throw new TimeoutException(UiText.Get("旧控制面板尚未结束。"));}
                    }
                    // A hung Unity process may not have read the last slider command.
                    // Preserve its requested value after stopping the sole settings writer.
                    string settings=Path.Combine(root,"DesktopSettings.json");
                    var saved=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(settings,Encoding.UTF8));
                    int percent=Math.Max(50,Math.Min(150,int.Parse(Value(args,"--size-percent","100"))));
                    saved["ModelSizeScale"]=percent/100.0;
                    if(args.Contains("--actor-limit"))saved["MaxActors"]=UmaDesktop.DesktopLimits.Clamp(int.Parse(Value(args,"--actor-limit")));
                    UmaDesktop.DesktopProfile.WriteSettings(settings,json.Serialize(saved),UmaDesktop.DesktopProfile.IsTestRun(arguments));
                    var next=Process.Start(new ProcessStartInfo(executable,string.Join(" ",arguments.Select(Quote)))
                        {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=root});
                    AtomicWrite(status,json.Serialize(new {stage="Started",oldProcessId=parent,newProcessId=next.Id,modelSizeScale=percent/100.0}));next.Dispose();
                }
                finally {if(entered)gate.ReleaseMutex();}
            }
        }
        catch(Exception ex){AtomicWrite(status,json.Serialize(new {stage="Failed",error=ex.Message}));throw;}
    }
    private static bool SamePath(string a,string b){return string.Equals(Path.GetFullPath(a),Path.GetFullPath(b),StringComparison.OrdinalIgnoreCase);}
    private static Process CheckedProcess(int id,string executable,long startTicks)
    {
        Process process;try{process=Process.GetProcessById(id);}catch(ArgumentException){return null;}
        try
        {
            if(!SamePath(process.MainModule.FileName,executable) || process.StartTime.ToUniversalTime().Ticks!=startTicks)
                throw new InvalidOperationException(UiText.Get("目标进程身份已改变，已取消强制重启。"));
            return process;
        }
        catch{process.Dispose();throw;}
    }
    private static void AtomicWrite(string path,string text)
    {string temp=path+".restart-tmp";File.WriteAllText(temp,text,new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
}
