using System;
using System.Collections.Generic;

namespace UmaDesktop
{
    public enum MiniBehavior { Walk, Run, Special }

    // Independent of Unity so timing, interruption and shuffle behavior are testable.
    public sealed class DesktopSchedule
    {
        private readonly Random random;
        private readonly double moveMin, moveMax, lifeMin, lifeMax, walkProbability;
        private double moveRemaining, lifeRemaining;
        public MiniBehavior Behavior { get; private set; }
        public bool SpecialPending { get { return Behavior!=MiniBehavior.Special && moveRemaining<=0; } }
        public void MakeSpecialDue() { if(Behavior!=MiniBehavior.Special) moveRemaining=0; }
        public bool CharacterDue { get; private set; }

        public DesktopSchedule(Random random, double moveMin, double moveMax, double lifeMin, double lifeMax, double walkProbability = 0.75)
        {
            if (random == null || moveMin <= 0 || moveMax < moveMin || lifeMin <= 0 || lifeMax < lifeMin)
                throw new ArgumentException("Invalid schedule settings.");
            this.random = random;
            this.walkProbability = Math.Max(0, Math.Min(1, walkProbability));
            this.moveMin = moveMin; this.moveMax = moveMax;
            this.lifeMin = lifeMin; this.lifeMax = lifeMax;
        }

        public void ResetCharacter()
        {
            lifeRemaining = Between(lifeMin, lifeMax);
            CharacterDue = false;
            NextGait();
        }

        public void Tick(double seconds, bool hasSpecial, bool specialFinished, bool allowSpecial = true)
        {
            if (seconds <= 0 || CharacterDue) return;
            lifeRemaining -= seconds;
            if (Behavior == MiniBehavior.Special)
            {
                // Finish the complete one-shot before changing characters.
                if (specialFinished) NextGait();
                return;
            }
            if (lifeRemaining <= 0) { CharacterDue = true; return; }
            moveRemaining -= seconds;
            if (moveRemaining <= 0)
            {
                if (hasSpecial) { if(allowSpecial) Behavior = MiniBehavior.Special; }
                else NextGait();
            }
        }

        public void ForceGait(MiniBehavior behavior) { Behavior = behavior; moveRemaining = Between(moveMin, moveMax); }
        public void RequestSpecial() { Behavior = MiniBehavior.Special; }
        public void ResumeLocomotion() { NextGait(); }

        private void NextGait()
        {
            Behavior = random.NextDouble() < walkProbability ? MiniBehavior.Walk : MiniBehavior.Run;
            moveRemaining = Between(moveMin, moveMax);
        }

        private double Between(double min, double max) { return min + random.NextDouble() * (max - min); }
    }

    public static class DesktopChoice
    {
        // Missing categories are omitted and the remaining weights renormalized.
        public static int CostumeCategory(Random random, bool race, bool uniform, bool other)
        {
            int total = (race ? 50 : 0) + (uniform ? 30 : 0) + (other ? 20 : 0);
            if (total == 0) throw new ArgumentException("No available costume category.");
            int value = random.Next(total);
            if (race) { if (value < 50) return 0; value -= 50; }
            if (uniform) { if (value < 30) return 1; value -= 30; }
            return 2;
        }
        public static double SpecialCutoff(double duration, double trim)
        {
            return Math.Max(0, duration - trim);
        }
    }

    public sealed class ShuffleBag<T>
    {
        private readonly List<T> source, bag = new List<T>();
        private readonly Random random;
        private T previous;
        private bool hasPrevious;

        public ShuffleBag(IEnumerable<T> items, Random random)
        {
            source = new List<T>(items);
            this.random = random;
            if (source.Count == 0) throw new ArgumentException("Empty shuffle bag.");
        }

        public T Next()
        {
            if (bag.Count == 0)
            {
                bag.AddRange(source);
                for (int i = bag.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    T temp = bag[i]; bag[i] = bag[j]; bag[j] = temp;
                }
                if (hasPrevious && bag.Count > 1 && EqualityComparer<T>.Default.Equals(bag[bag.Count - 1], previous))
                {
                    T temp = bag[0]; bag[0] = bag[bag.Count - 1]; bag[bag.Count - 1] = temp;
                }
            }
            previous = bag[bag.Count - 1];
            hasPrevious = true;
            bag.RemoveAt(bag.Count - 1);
            return previous;
        }
    }
}
