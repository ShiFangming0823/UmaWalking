using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace UmaDesktop
{
    public sealed partial class DesktopPrototype
    {
        [Serializable] private sealed class LaunchInfo
        {public int processId;public long startUtcTicks;public string executable;public string[] arguments;}
        private void WriteLaunchInfo()
        {
            using(var current=Process.GetCurrentProcess())
            {
                var info=new LaunchInfo {processId=current.Id,startUtcTicks=current.StartTime.ToUniversalTime().Ticks,
                    executable=Path.Combine(root,"UmaDesktop.exe"),arguments=Environment.GetCommandLineArgs().Skip(1).ToArray()};
                string target=Path.Combine(logDirectory,"launch.json"),temporary=target+".tmp";
                File.WriteAllText(temporary,JsonUtility.ToJson(info,true));
                if(File.Exists(target))File.Replace(temporary,target,null);else File.Move(temporary,target);
            }
        }
    }
}
