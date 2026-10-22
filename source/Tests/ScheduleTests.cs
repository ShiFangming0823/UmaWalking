using System;
using System.Collections.Generic;
using UmaDesktop;

public static class ScheduleTests
{
    private static int checks;
    private static void Check(bool valid, string message)
    {
        checks++;
        if (!valid) throw new Exception(message);
    }

    public static void Main()
    {
        var schedule = new DesktopSchedule(new Random(1), 2, 2, 100, 100);
        schedule.ResetCharacter();
        MiniBehavior first = schedule.Behavior;
        schedule.Tick(0, true, false);
        Check(schedule.Behavior == first, "Pause must preserve state.");
        schedule.Tick(1.99, true, false);
        Check(schedule.Behavior != MiniBehavior.Special, "Special must not begin early.");
        schedule.Tick(0.02, true, false);
        Check(schedule.Behavior == MiniBehavior.Special, "Locomotion must be interrupted by special.");
        schedule.Tick(50, true, false);
        Check(schedule.Behavior == MiniBehavior.Special, "Special must finish before resuming.");
        schedule.Tick(0.1, true, true);
        Check(schedule.Behavior != MiniBehavior.Special, "A completed one-shot must resume locomotion.");
        schedule.Tick(0.1, true, false);
        Check(schedule.Behavior != MiniBehavior.Special, "Special must not immediately repeat.");

        schedule = new DesktopSchedule(new Random(4), 1, 1, 1.5, 1.5);
        schedule.ResetCharacter();
        schedule.Tick(1, true, false);
        schedule.Tick(2, true, false);
        Check(!schedule.CharacterDue, "Character change must wait for active special.");
        schedule.Tick(0.1, true, true);
        Check(!schedule.CharacterDue, "Finish special cleanly before changing character.");
        schedule.Tick(0.1, true, false);
        Check(schedule.CharacterDue, "Overdue character must change after special.");

        schedule = new DesktopSchedule(new Random(8), 1, 1, 10, 10);
        schedule.ResetCharacter();
        for (int i = 0; i < 30; i++) schedule.Tick(0.1, false, false);
        Check(schedule.Behavior != MiniBehavior.Special, "A character without specials must keep moving.");

        var bag = new ShuffleBag<int>(new[] { 1, 2, 3, 4, 5 }, new Random(7));
        int previous = 0;
        for (int round = 0; round < 1000; round++)
        {
            var seen = new HashSet<int>();
            for (int i = 0; i < 5; i++)
            {
                int n = bag.Next();
                Check(n != previous, "Adjacent picks must differ, including shuffle boundaries.");
                Check(seen.Add(n), "Each shuffle must contain each character once.");
                previous = n;
            }
        }
        var one = new ShuffleBag<int>(new[] { 1001 }, new Random(3));
        Check(one.Next() == 1001 && one.Next() == 1001, "Single-character mode must remain usable.");

        bool sawWalk = false, sawRun = false;
        for (int seed = 1; seed < 100; seed++)
        {
            var s = new DesktopSchedule(new Random(seed), 1, 2, 10, 20);
            s.ResetCharacter();
            sawWalk |= s.Behavior == MiniBehavior.Walk;
            sawRun |= s.Behavior == MiniBehavior.Run;
        }
        Check(sawWalk && sawRun, "Random gait selection must include walking and running.");
        var rng = new Random(932);
        int[] categories = new int[3];
        int walks = 0;
        for (int i = 0; i < 100000; i++)
        {
            categories[DesktopChoice.CostumeCategory(rng, true, true, true)]++;
            var gait = new DesktopSchedule(rng, 10, 24, 100, 100);
            gait.ResetCharacter();
            if (gait.Behavior == MiniBehavior.Walk) walks++;
        }
        Check(Math.Abs(categories[0] / 100000.0 - 0.5) < 0.01, "Race costumes must get 50 percent.");
        Check(Math.Abs(categories[1] / 100000.0 - 0.3) < 0.01, "Uniforms must get 30 percent.");
        Check(Math.Abs(categories[2] / 100000.0 - 0.2) < 0.01, "Other costumes must get 20 percent.");
        Check(Math.Abs(walks / 100000.0 - 0.75) < 0.01, "Walk/run must be 3:1.");
        for (int i = 0; i < 1000; i++) Check(DesktopChoice.CostumeCategory(rng, false, true, true) != 0, "Missing race category must never be picked.");
        Check(DesktopChoice.CostumeCategory(rng, false, false, true) == 2, "Single available category must work.");
        Check(Math.Abs(DesktopChoice.SpecialCutoff(8.2, 0.5) - 7.7) < 1e-9, "Trim the whole special sequence, once.");
        Check(DesktopChoice.SpecialCutoff(0.3, 0.5) == 0, "Short special must not have a negative cutoff.");
        for (int seed = 0; seed < 100; seed++)
        {
            var gait = new DesktopSchedule(new Random(seed), 10, 24, 100, 100);
            gait.ResetCharacter();
            gait.Tick(9.999, true, false);
            Check(gait.Behavior != MiniBehavior.Special, "Gait must last at least ten seconds.");
            gait.Tick(14.002, true, false);
            Check(gait.Behavior == MiniBehavior.Special, "Gait must end by twenty-four seconds.");
        }
        Console.WriteLine("Choices / 100000: race=" + categories[0] + " uniform=" + categories[1] + " other=" + categories[2] + " walk=" + walks);
        Console.WriteLine("PASS: " + checks + " scheduling and shuffle assertions.");
    }
}
