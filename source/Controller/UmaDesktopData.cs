using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using UmaDesktop;

internal static class DesktopData
{
    internal static string Hash(string path)
    {using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
    internal static string Full(string path){return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);}
    internal static bool Inside(string parent,string child){return string.Equals(Full(parent),Full(child),StringComparison.OrdinalIgnoreCase)||Full(child).StartsWith(Full(parent)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
    internal static void NoLinks(string path)
    {
        for(string current=Full(path);!string.IsNullOrEmpty(current);current=Path.GetDirectoryName(current))
            if((Directory.Exists(current)||File.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("Symbolic links/junctions are not supported: "+current);
    }
    internal static string Resolve(string root,string relative)
    {
        if(relative!="meta"&&relative!="master/master.mdb"&&!System.Text.RegularExpressions.Regex.IsMatch(relative??"","^dat/[A-Za-z0-9]{2}/[A-Za-z0-9]{32}$"))throw new InvalidDataException("Unsafe asset path: "+relative);
        string result=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
        if(!Inside(root,result))throw new InvalidDataException("Path escapes dataset");NoLinks(result);return result;
    }
    internal static DesktopAssetPlan ReadPlan(string path)
    {return new JavaScriptSerializer{MaxJsonLength=64*1024*1024}.Deserialize<DesktopAssetPlan>(File.ReadAllText(path,Encoding.UTF8));}
    internal static DesktopAssetPlan Extract(DesktopAssetPlan plan,string destination,Action<int,string> progress,CancellationToken cancel)
    {
        if(plan==null||plan.schemaVersion!=1||plan.files==null||plan.files.Length<3)throw new InvalidDataException("No extraction plan. Load a valid full dataset first.");
        string source=Full(plan.source),dest=Full(destination);NoLinks(source);NoLinks(dest);
        if(Inside(source,dest)||Inside(dest,source))throw new InvalidDataException("The extraction directory and source must be separate, non-nested directories.");
        if(Directory.Exists(dest)&&Directory.EnumerateFileSystemEntries(dest).Any())throw new IOException("Choose an empty destination directory.");
        if(plan.files.Select(f=>f.path.ToUpperInvariant()).Distinct().Count()!=plan.files.Length)throw new InvalidDataException("Duplicate asset paths");
        foreach(var file in plan.files){cancel.ThrowIfCancellationRequested();string path=Resolve(source,file.path);if(!File.Exists(path)||new FileInfo(path).Length!=file.bytes)throw new IOException("Source missing or changed: "+file.path);Resolve(dest,file.path);}
        if(Hash(Resolve(source,"meta"))!=plan.metaSha256||Hash(Resolve(source,"master/master.mdb"))!=plan.masterSha256)throw new IOException("Source index changed; restart and re-extract.");
        long total=plan.files.Sum(f=>f.bytes);var drive=new DriveInfo(Path.GetPathRoot(dest));if(drive.AvailableFreeSpace<total+64L*1024*1024)throw new IOException("Insufficient free space");
        Directory.CreateDirectory(dest);string marker=Path.Combine(dest,"extraction-in-progress.txt");File.WriteAllText(marker,"Incomplete extraction; do not delete original dataset.");
        long done=0;int last=-1;
        foreach(var file in plan.files)
        {
            cancel.ThrowIfCancellationRequested();string from=Resolve(source,file.path),to=Resolve(dest,file.path);Directory.CreateDirectory(Path.GetDirectoryName(to));
            // Hash the source bytes actually copied, then independently read the destination.
            string hash;
            using(var sha=SHA256.Create())using(var input=new FileStream(from,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var output=new FileStream(to,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {
                byte[] buffer=new byte[1024*1024];int count;long copied=0;
                while((count=input.Read(buffer,0,buffer.Length))>0){cancel.ThrowIfCancellationRequested();output.Write(buffer,0,count);sha.TransformBlock(buffer,0,count,buffer,0);copied+=count;int percent=(int)Math.Min(99,100*(done+copied)/Math.Max(1,total));if(percent!=last){last=percent;progress(percent,file.path);}}
                sha.TransformFinalBlock(new byte[0],0,0);output.Flush(true);hash=BitConverter.ToString(sha.Hash).Replace("-","");
                if(copied!=file.bytes)throw new IOException("Source changed while copying");
            }
            if(Hash(to)!=hash)throw new IOException("Copy verification failed: "+file.path);file.sha256=hash;done+=file.bytes;
        }
        cancel.ThrowIfCancellationRequested();
        if(Hash(Resolve(source,"meta"))!=plan.metaSha256||Hash(Resolve(source,"master/master.mdb"))!=plan.masterSha256)throw new IOException("Game data updated during extraction; original must be kept.");
        if(plan.files.First(f=>f.path=="meta").sha256!=plan.metaSha256||plan.files.First(f=>f.path=="master/master.mdb").sha256!=plan.masterSha256)throw new IOException("Copied database changed during extraction");
        var json=new JavaScriptSerializer{MaxJsonLength=64*1024*1024};File.WriteAllText(Path.Combine(dest,"UmaDesktop-mini-manifest.json"),json.Serialize(plan),new UTF8Encoding(false));
        File.Delete(marker);progress(100,"Verified");return plan;
    }
    internal static void Verify(string dest,DesktopAssetPlan plan)
    {
        if(File.Exists(Path.Combine(dest,"extraction-in-progress.txt")))throw new IOException("Incomplete extraction");
        foreach(var file in plan.files)if(string.IsNullOrEmpty(file.sha256)||Hash(Resolve(dest,file.path))!=file.sha256)throw new IOException("Extracted asset verification failed: "+file.path);
    }
    // Called only after a fresh Yes confirmation in the restarted UI, never by the extraction worker.
    internal static void DeleteOriginal(string source,string dest,DesktopAssetPlan plan)
    {
        source=Full(source);dest=Full(dest);NoLinks(source);NoLinks(dest);
        if(!string.Equals(source,Full(plan.source),StringComparison.OrdinalIgnoreCase)||Inside(source,dest)||Inside(dest,source)||source==Path.GetPathRoot(source).TrimEnd('\\'))throw new IOException("Unsafe deletion target");
        Verify(dest,plan);
        if(Hash(Resolve(source,"meta"))!=plan.metaSha256||Hash(Resolve(source,"master/master.mdb"))!=plan.masterSha256)throw new IOException("Original data updated; deletion cancelled.");
        string dat=Path.Combine(source,"dat");NoLinks(dat);
        // Inspect each level before traversing so junctions cannot redirect recursive deletion.
        var pending=new Stack<string>();pending.Push(dat);
        while(pending.Count>0){string directory=pending.Pop();NoLinks(directory);foreach(string entry in Directory.EnumerateFileSystemEntries(directory)){NoLinks(entry);if(Directory.Exists(entry))pending.Push(entry);}}
        Directory.Delete(dat,true);File.Delete(Resolve(source,"meta"));File.Delete(Resolve(source,"master/master.mdb"));
    }
}
