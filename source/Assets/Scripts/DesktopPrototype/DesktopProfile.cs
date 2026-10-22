using System;
using System.IO;
using System.Linq;
using System.Text;

namespace UmaDesktop
{
    // The live root file remains compatible with older tools; the per-user copy
    // is authoritative on the next launch, including launches from a new folder.
    public static class DesktopProfile
    {
        public static string DirectoryPath
        {
            get { var custom=Environment.GetEnvironmentVariable("UMA_WALKING_PROFILE");
                return Path.GetFullPath(string.IsNullOrWhiteSpace(custom)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"UmaWalking"):custom); }
        }
        public static bool IsTestRun(string[] arguments=null)
        { return (arguments??Environment.GetCommandLineArgs()).Any(a=>a=="--desktop-test-mode"||a=="--desktop-smoke-seconds"||a=="--desktop-expression-audit"||a=="--desktop-pair-spacing-audit"||a=="--portable-settings"); }
        public static string SettingsFile {get{return Path.Combine(DirectoryPath,"DesktopSettings.json");}}
        public static bool IsLegacyPlaceholder(string value,string applicationDirectory)
        {
            if(string.IsNullOrWhiteSpace(value))return true;
            string normalized=value.Replace('/','\\').TrimEnd('\\');
            bool placeholder=normalized.Equals("GameData",StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(@"\UmaWalking\GameData",StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(@"\UmaDesktop\GameData",StringComparison.OrdinalIgnoreCase);
            try {placeholder|=Path.GetFullPath(value).TrimEnd('\\').Equals(Path.GetFullPath(Path.Combine(applicationDirectory,"GameData")).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase);}catch{}
            if(!placeholder)return false;
            string folder=Path.IsPathRooted(value)?value:Path.Combine(applicationDirectory,value);
            return !(File.Exists(Path.Combine(folder,"meta"))&&File.Exists(Path.Combine(folder,"master","master.mdb"))&&Directory.Exists(Path.Combine(folder,"dat")));
        }
        public static void AtomicWrite(string path,string text,bool backup=false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try { File.WriteAllText(temporary,text,new UTF8Encoding(false));
                if(File.Exists(path))File.Replace(temporary,path,backup?path+".bak":null);else File.Move(temporary,path); }
            finally {if(File.Exists(temporary))File.Delete(temporary);}
        }
        public static string ReadSettings(string localFile,Func<string,bool> valid)
        {
            if(!IsTestRun())
            {
                foreach(string file in new[]{SettingsFile,SettingsFile+".bak"})
                {
                    if(!File.Exists(file))continue;
                    string text=File.ReadAllText(file);if(!valid(text))continue;
                    AtomicWrite(localFile,text);return text;
                }
                // Preserve damaged shared files for recovery instead of silently
                // replacing them with the defaults shipped with a new release.
                if(File.Exists(SettingsFile)||File.Exists(SettingsFile+".bak"))
                    throw new InvalidDataException("UmaWalking user settings are damaged. Restore DesktopSettings.json.bak in the user profile before continuing.");
            }
            if(!File.Exists(localFile))return null;
            string local=File.ReadAllText(localFile);
            if(!valid(local))throw new InvalidDataException("DesktopSettings.json is invalid; the original file has been preserved.");
            return local;
        }
        public static void WriteSettings(string localFile,string text,bool? testRun=null)
        {
            // Persist the durable copy first so an interrupted upgrade never
            // restores the previous shipped defaults over the user's choices.
            if(!(testRun??IsTestRun()))AtomicWrite(SettingsFile,text,true);
            AtomicWrite(localFile,text);
        }
    }
}
