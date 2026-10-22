using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UmaDesktop
{
    public static class DesktopRules
    {
        public static string NameKey(string value) { return (value ?? "").Trim().Normalize(NormalizationForm.FormKC); }
        public static bool Allowed(string mode, int id, IEnumerable<int> included, IEnumerable<int> excluded)
        {
            if (mode == "Include") return included.Contains(id);
            if (mode == "Exclude") return !excluded.Contains(id);
            return true;
        }
        public static bool CanMeet(bool leftWalking, bool rightWalking, int leftDirection, int rightDirection,
            double previousSeparation, double currentSeparation, double threshold)
        {
            return leftWalking && rightWalking && leftDirection > 0 && rightDirection < 0 &&
                previousSeparation > threshold && currentSeparation <= threshold && currentSeparation >= 0;
        }
    }

    public static class DesktopLimits
    {
        public const int Maximum=10,Default=6,LayerStart=20;
        public static int Clamp(int limit){return Math.Max(1,Math.Min(Maximum,limit));}
        public static bool AllowVariants(int selectedNames,int limit){return selectedNames>0&&selectedNames<Clamp(limit);}
        public static int Target(int limit,int manualCount,int availableRandomSlots)
        {return Math.Min(Clamp(limit),Math.Max(0,manualCount)+Math.Max(0,availableRandomSlots));}
        public static int Excess(int activeCount,int target){return Math.Max(0,activeCount-Math.Max(0,target));}
        public static int FreeLayer(IEnumerable<int> occupied)
        {
            var used=new HashSet<int>(occupied);
            for(int layer=LayerStart;layer<LayerStart+Maximum;layer++)if(!used.Contains(layer))return layer;
            throw new InvalidOperationException("No free character render layer.");
        }
    }
    public enum PopulationAction { None, Add, RetireOldest }
    public enum ManualEntryResult { Added, AlreadyPresent, AlreadyQueued }
    public sealed class ManualEntryRequest
    {
        public readonly int CharacterId;public readonly string Name;
        public ManualEntryRequest(int id,string name){CharacterId=id;Name=DesktopRules.NameKey(name);}
    }
    public sealed class ManualEntryQueue
    {
        private readonly Queue<ManualEntryRequest> items=new Queue<ManualEntryRequest>();
        public int Count {get{return items.Count;}}
        public IEnumerable<int> Ids {get{return items.Select(x=>x.CharacterId);}}
        public bool ContainsName(string name){return items.Any(x=>x.Name==DesktopRules.NameKey(name));}
        public ManualEntryResult Request(int id,string name,IEnumerable<string> present)
        {
            string key=DesktopRules.NameKey(name);
            if(present.Any(n=>DesktopRules.NameKey(n)==key))return ManualEntryResult.AlreadyPresent;
            if(ContainsName(key))return ManualEntryResult.AlreadyQueued;
            items.Enqueue(new ManualEntryRequest(id,key));return ManualEntryResult.Added;
        }
        public ManualEntryRequest Next(int count,int limit=DesktopLimits.Default){return count<DesktopLimits.Clamp(limit) && items.Count>0?items.Peek():null;}
        public bool Complete(int id){if(items.Count==0 || items.Peek().CharacterId!=id)return false;items.Dequeue();return true;}
    }
    // Departure is a transaction: never fill its slot until the actor is fully
    // off screen, destroyed, and the explicit empty interval has elapsed.
    public sealed class PopulationClock
    {
        private readonly Random random;
        private readonly double spawnMin, spawnMax, holdMin, holdMax, gapMin, gapMax;
        public double NextAt { get; private set; }
        public bool AwaitingExit { get; private set; }
        private bool preserveManualTimer;
        public PopulationClock(Random random, double spawnMin, double spawnMax, double holdMin, double holdMax, double gapMin, double gapMax)
        {
            this.random = random; this.spawnMin = spawnMin; this.spawnMax = spawnMax;
            this.holdMin = holdMin; this.holdMax = holdMax; this.gapMin = gapMin; this.gapMax = gapMax;
        }
        public PopulationAction Decide(double now, int count, int limit)
        {
            if (AwaitingExit || limit <= 0 || now < NextAt) return PopulationAction.None;
            return count < limit ? PopulationAction.Add : PopulationAction.RetireOldest;
        }
        public void Added(double now, int count, int limit)
        {
            NextAt = now + Between(count >= limit ? holdMin : spawnMin, count >= limit ? holdMax : spawnMax);
        }
        public void ManualAdded(double now,int count,int limit,bool departuresInProgress)
        {Added(now,count,limit);preserveManualTimer=departuresInProgress;}
        public void Retiring() { AwaitingExit = true; }
        public void Removed(double now,int count=-1,int limit=-1)
        {
            AwaitingExit = false;
            if(preserveManualTimer){preserveManualTimer=false;return;}
            NextAt = now + (limit>0 && count>=limit?Between(holdMin,holdMax):Between(gapMin, gapMax));
        }
        public void RetryAfter(double now, double delay) { NextAt = now + delay; }
        private double Between(double min, double max) { return min + random.NextDouble() * (max - min); }
    }
}
