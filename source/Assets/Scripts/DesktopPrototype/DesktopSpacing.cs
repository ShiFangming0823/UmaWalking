using System;
namespace UmaDesktop
{
    public static class DesktopSpacing
    {
        public static double SampleSpeedFactor(Random random,double min,double max)
        { return min+random.NextDouble()*(max-min); }
        public static bool Clear(double x,double halfWidth,double otherX,double otherHalfWidth,double gap)
        { return Math.Abs(x-otherX)>=halfWidth+otherHalfWidth+gap-0.01; }
        // Swept interval, so a fast runner cannot tunnel through a reserved pose.
        public static double TravelLimit(double x,int direction,double distance,double halfWidth,double otherX,double otherHalfWidth,double gap)
        {
            double radius=halfWidth+otherHalfWidth+gap;
            double separation=(otherX-x)*direction;
            if(separation<=0) return distance; // moving away
            return Math.Min(distance,Math.Max(0,separation-radius));
        }
    }
}
