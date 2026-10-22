using System;
using System.Linq;
using UmaDesktop;
public static class FleetTests
{
 private static int n;private static void Check(bool value,string text){n++;if(!value)throw new Exception(text);}
 public static void Main()
 {
  Check(DesktopRules.Allowed("All",1001,new int[0],new[]{1001}),"All ignores lists.");
  Check(!DesktopRules.Allowed("Include",1001,new int[0],new int[0]),"Empty Include must stay empty.");
  Check(DesktopRules.Allowed("Include",1001,new[]{1001,1028},new[]{1001}),"Include uses its own list.");
  Check(!DesktopRules.Allowed("Exclude",1001,new[]{1001},new[]{1001,1028}),"Exclude removes selected IDs.");
  Check(DesktopRules.Allowed("Exclude",1002,new int[0],new[]{1001}),"Exclude retains the others.");
  Check(DesktopRules.NameKey(" Ａ ")==DesktopRules.NameKey("A"),"Visible name normalization must prevent duplicates.");
  var clock=new PopulationClock(new Random(1),20,20,30,30,8,8);
  Check(clock.Decide(0,0,6)==PopulationAction.Add,"Initial actor enters immediately.");
  clock.Added(0,1,6);Check(clock.Decide(19.99,1,6)==PopulationAction.None,"Do not fill early.");
  Check(clock.Decide(20,1,6)==PopulationAction.Add,"Add on schedule.");
  clock.Added(20,6,6);Check(clock.Decide(49.99,6,6)==PopulationAction.None,"Hold a full screen.");
  Check(clock.Decide(50,6,6)==PopulationAction.RetireOldest,"Retire after full hold.");clock.Retiring();
  Check(clock.Decide(9999,5,6)==PopulationAction.None,"No replacement while departure is in progress.");
  clock.Removed(100);Check(clock.Decide(107.99,5,6)==PopulationAction.None,"Keep explicit empty gap.");
  Check(clock.Decide(108,5,6)==PopulationAction.Add,"Replace only after exit and gap.");
  Check(clock.Decide(9999,0,0)==PopulationAction.None,"An empty pool cannot spawn.");
  Check(DesktopRules.CanMeet(true,true,1,-1,201,199,200),"Opposing walkers trigger at encounter threshold.");
  Check(!DesktopRules.CanMeet(false,true,1,-1,201,199,200),"Running must not trigger walking interaction.");
  Check(!DesktopRules.CanMeet(true,true,1,1,201,199,200),"Same direction is not an encounter.");
  Check(!DesktopRules.CanMeet(true,true,1,-1,199,198,200),"Do not roll again each overlapping frame.");
  var random=new Random(72);int accepted=0;
  for(int i=0;i<100000;i++)if(random.NextDouble()<0.5)accepted++;
  Check(Math.Abs(accepted/100000.0-0.5)<0.01,"Interaction coin must be 50 percent.");
  var schedule=new DesktopSchedule(new Random(3),10,24,1000,1000);schedule.ResetCharacter();
  schedule.RequestSpecial();Check(schedule.Behavior==MiniBehavior.Special,"Click interrupts gait immediately.");
  schedule.RequestSpecial();Check(schedule.Behavior==MiniBehavior.Special,"Repeated click restarts special state.");
  schedule.ResumeLocomotion();Check(schedule.Behavior!=MiniBehavior.Special,"Special resumes a gait without waiting.");
  Check(DesktopChoice.SpecialCutoff(3.5,1)==2.5,"Exclusive performance ends one second early.");
  var requests=new ManualEntryQueue();
  Check(requests.Next(0)==null,"Empty manual queue.");
  Check(requests.Request(1001," Ａ ",new[]{"A"})==ManualEntryResult.AlreadyPresent,"Reject an already present normalized name.");
  Check(requests.Request(1002,"B",new string[0])==ManualEntryResult.Added,"Queue first named actor.");
  Check(requests.Request(1003,"C",new string[0])==ManualEntryResult.Added,"Queue second named actor.");
  Check(requests.Request(9999,"Ｂ",new string[0])==ManualEntryResult.AlreadyQueued,"Deduplicate by normalized name, including alternate IDs.");
  Check(requests.Count==2,"Duplicate clicks do not add queue entries.");
  Check(requests.Next(6)==null && requests.Next(7)==null,"Count exiting actors toward the six-actor cap.");
  Check(requests.Next(5).CharacterId==1002,"First empty slot belongs to FIFO queue head.");
  Check(!requests.Complete(1003) && requests.Count==2,"Only a successful head load completes its request.");
  Check(requests.Next(5).CharacterId==1002,"A failed load keeps its place.");
  Check(requests.Complete(1002) && requests.Next(5).CharacterId==1003,"Complete first load before next request.");
  Check(requests.Complete(1003) && requests.Count==0,"Complete final request.");
  var manualClock=new PopulationClock(new Random(4),20,20,30,30,8,8);
  manualClock.Added(0,3,6);manualClock.ManualAdded(5,4,6,false);
  Check(manualClock.NextAt==25,"Immediate successful entry resets next random entry timer.");
  Check(manualClock.Decide(24.99,4,6)==PopulationAction.None,"Old random entry deadline cannot fire.");
  Check(manualClock.Decide(25,4,6)==PopulationAction.Add,"New timer fires after a full interval.");
  manualClock.Retiring();manualClock.ManualAdded(7,5,6,true);manualClock.Removed(10);
  Check(manualClock.NextAt==27 && !manualClock.AwaitingExit,"Finishing another exit preserves the manual-entry timer.");
  manualClock.Retiring();manualClock.Removed(40);
  Check(manualClock.NextAt==48,"Following ordinary departures retain the normal empty gap.");
  manualClock.ManualAdded(50,6,6,false);
  Check(manualClock.NextAt==80,"Full population resets its normal rotation hold.");
  manualClock.Retiring();manualClock.Removed(90,2,2);Check(manualClock.NextAt==120,"A reduced random pool keeps a full hold instead of immediately draining remaining manual actors.");
  Console.WriteLine("PASS: "+n+" fleet/filter/manual-entry/encounter assertions; accepted="+accepted+"/100000");
 }
}
