using System;
using System.IO;
using UmaDesktop;
internal static class ProfileTests
{
    static void Main()
    {
        string area=Path.Combine(Path.GetTempPath(),"UmaWalking-profile-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(area);
        string prior=Environment.GetEnvironmentVariable("UMA_WALKING_PROFILE");int checks=0;
        Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception(message);checks++;};
        try
        {
            Environment.SetEnvironmentVariable("UMA_WALKING_PROFILE",Path.Combine(area,"profile"));
            string old=Path.Combine(area,"old","DesktopSettings.json"),next=Path.Combine(area,"new","DesktopSettings.json");
            string original="{\"DataPath\":\"Z:/Game Data/Persistent\",\"FrameRate\":60,\"MaxActors\":9,\"Ground\":{\"mode\":\"Planes\"},\"IncludeIds\":[1001,1076]}";
            Func<string,bool> valid=text=>text.StartsWith("{")&&text.Contains("DataPath");
            DesktopProfile.AtomicWrite(old,original);check(DesktopProfile.ReadSettings(old,valid)==original,"Legacy imported without changing bytes");
            DesktopProfile.WriteSettings(old,original);check(File.ReadAllText(DesktopProfile.SettingsFile)==original,"Durable profile saved");
            DesktopProfile.AtomicWrite(next,"{\"DataPath\":\"GameData\"}");check(DesktopProfile.ReadSettings(next,valid)==original,"New-folder defaults cannot overwrite the user's configuration");
            check(File.ReadAllText(next)==original,"New folder mirror contains all settings");
            string changed=original.Replace("60","120");DesktopProfile.WriteSettings(next,changed);check(File.ReadAllText(DesktopProfile.SettingsFile)==changed,"Changes persist");
            check(File.ReadAllText(DesktopProfile.SettingsFile+".bak")==original,"Previous configuration retained as backup");
            File.WriteAllText(DesktopProfile.SettingsFile,"corrupt");check(DesktopProfile.ReadSettings(next,valid)==original,"Valid backup recovers corrupted primary profile");
            File.WriteAllText(DesktopProfile.SettingsFile+".bak","corrupt backup");bool rejected=false;try{DesktopProfile.ReadSettings(next,valid);}catch(InvalidDataException){rejected=true;}check(rejected,"Two damaged copies do not silently reset settings");
            check(File.ReadAllText(DesktopProfile.SettingsFile)=="corrupt","Damaged source preserved for recovery");
            check(DesktopProfile.IsTestRun(new[]{"--desktop-test-mode"})&&DesktopProfile.IsTestRun(new[]{"--desktop-smoke-seconds"}),"Runtime test isolation");
            check(!DesktopProfile.IsTestRun(new[]{"--umawalking-autostart"}),"Normal auto-start retains durable settings");
            DesktopProfile.WriteSettings(next,changed,true);check(File.ReadAllText(DesktopProfile.SettingsFile)=="corrupt","Isolated tests cannot touch durable user settings");
            check(DesktopProfile.IsLegacyPlaceholder("GameData",area),"Relative old placeholder repaired");
            check(DesktopProfile.IsLegacyPlaceholder(Path.Combine(area,"UmaWalking","GameData"),area),"Expanded old placeholder repaired");
            check(!DesktopProfile.IsLegacyPlaceholder(Path.Combine(area,"Chosen-Offline-Data"),area),"Custom missing paths preserved");
            string legitimate=Path.Combine(area,"GameData");Directory.CreateDirectory(Path.Combine(legitimate,"master"));Directory.CreateDirectory(Path.Combine(legitimate,"dat"));File.WriteAllText(Path.Combine(legitimate,"meta"),"fixture");File.WriteAllText(Path.Combine(legitimate,"master","master.mdb"),"fixture");
            check(!DesktopProfile.IsLegacyPlaceholder("GameData",area),"Valid relative GameData preserved");
            check(!DesktopProfile.IsLegacyPlaceholder(legitimate,area),"Valid absolute GameData preserved");
            Console.WriteLine("PASS: "+checks+" upgrade/configuration persistence assertions.");
        }
        finally{Environment.SetEnvironmentVariable("UMA_WALKING_PROFILE",prior);Directory.Delete(area,true);}
    }
}
