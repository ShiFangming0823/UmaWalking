using System;
using System.Linq;
using UmaDesktop;
class PopulationLimitTests
{
    static int checks;
    static void Check(bool v,string name){checks++;if(!v)throw new Exception(name);}
    static void Main()
    {
        try
        {
            Check(DesktopLimits.Clamp(-99)==1&&DesktopLimits.Clamp(999)==10,"Out-of-range limits are bounded");
            Check(DesktopLimits.Target(3,8,100)==3,"Manual choices share the global cap");
            Check(DesktopLimits.Target(10,0,0)==0,"Empty Include stays empty");
            Check(DesktopLimits.Target(10,2,0)==2,"Manual choices survive empty random pool");
            Check(DesktopLimits.Target(10,0,4)==4,"All/Exclude cannot fabricate duplicate names");
            for(int limit=1;limit<=10;limit++)
            {
                var queue=new ManualEntryQueue();queue.Request(1001,"A",new string[0]);
                for(int count=0;count<=10;count++)Check((queue.Next(count,limit)!=null)==(count<limit),"Queue admission at limit "+limit);
                var clock=new PopulationClock(new Random(1),2,2,4,4,1,1);
                Check(clock.Decide(0,limit,limit)==PopulationAction.RetireOldest,"Natural full rotation "+limit);
                Check(clock.Decide(0,limit-1,limit)==PopulationAction.Add,"Available slot "+limit);
                clock.Retiring();Check(clock.Decide(9999,limit-1,limit)==PopulationAction.None,"Wait for real departure "+limit);
                clock.Removed(10,limit-1,limit);Check(clock.Decide(10.99,limit-1,limit)==PopulationAction.None&&clock.Decide(11,limit-1,limit)==PopulationAction.Add,"Keep empty gap "+limit);
                for(int names=0;names<=12;names++)Check(DesktopLimits.AllowVariants(names,limit)==(names>0&&names<limit),"Include threshold follows chosen limit");
                for(int manual=0;manual<=10;manual++)for(int free=0;free<=15;free++)
                    Check(DesktopLimits.Target(limit,manual,free)<=limit&&DesktopLimits.Target(limit,manual,free)<=manual+free,"Total target bounded "+limit);
            }
            var occupied=new System.Collections.Generic.List<int>();
            for(int i=0;i<10;i++){int layer=DesktopLimits.FreeLayer(occupied);Check(layer>=0&&layer<32&&!occupied.Contains(layer),"Distinct valid render bit "+i);occupied.Add(layer);}
            bool rejected=false;try{DesktopLimits.FreeLayer(occupied);}catch(InvalidOperationException){rejected=true;}Check(rejected,"No eleventh render layer");
            occupied.RemoveAt(3);Check(DesktopLimits.FreeLayer(occupied)==23,"Released render layer reused");
            // Ten to three: seven exits are requested once. Those seven still
            // occupy slots on the following frame and must not retire the survivors.
            int active=10,exiting=0;
            int excess=DesktopLimits.Excess(active,3);active-=excess;exiting+=excess;
            Check(active==3&&exiting==7&&DesktopLimits.Excess(active,3)==0,"Lowering never repeatedly drains survivors");
            var pending=new ManualEntryQueue();pending.Request(1002,"B",new string[0]);
            Check(pending.Next(active+exiting,3)==null,"Shrinking keeps new entries queued");
            exiting=0;Check(pending.Next(active,3)==null,"New cap remains binding after exits");
            Check(pending.Next(active,10)!=null,"Raising immediately releases waiting request");
            Check(DesktopLimits.Excess(10,1)==9&&DesktopLimits.Excess(1,10)==0,"Extreme transitions 10 to 1 to 10");
            Console.WriteLine("Population limit tests passed: "+checks);
        }
        catch(Exception e){Console.WriteLine(e);Environment.Exit(1);}
    }
}
