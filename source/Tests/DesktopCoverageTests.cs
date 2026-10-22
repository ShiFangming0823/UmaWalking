using System;
using UmaDesktop;
class DesktopCoverageTests
{
    static int count;
    static DesktopRectangle R(int l,int t,int r,int b) {return new DesktopRectangle(l,t,r,b);}
    static void Check(bool v,string label) {count++;if(!v)throw new Exception(label);}
    static void Main()
    {
        var d=R(0,0,1920,1040);
        Check(!DesktopCoverage.FullyCovered(d,new DesktopRectangle[0]),"empty desktop");
        Check(DesktopCoverage.FullyCovered(d,new[]{d}),"maximized work area");
        Check(DesktopCoverage.FullyCovered(d,new[]{R(-8,-8,1928,1088)}),"fullscreen oversized border");
        Check(!DesktopCoverage.FullyCovered(d,new[]{R(0,0,1919,1040)}),"one pixel desktop gap");
        Check(!DesktopCoverage.FullyCovered(d,new[]{R(0,0,1920,1039)}),"bottom strip remains");
        Check(DesktopCoverage.FullyCovered(d,new[]{R(0,0,960,1040),R(960,0,1920,1040)}),"two tiled windows");
        Check(!DesktopCoverage.FullyCovered(d,new[]{R(0,0,959,1040),R(960,0,1920,1040)}),"tiled window gap");
        Check(!DesktopCoverage.FullyCovered(d,new[]{R(-2000,0,-1,1040)}),"other monitor");
        Check(!DesktopCoverage.FullyCovered(d,new[]{R(0,0,1920,500),R(0,501,1920,1040)}),"horizontal gap");
        Check(!DesktopCoverage.FullyCovered(d,new[]{R(0,0,1920,0)}),"zero height");
        Check(!DesktopCoverage.FullyCovered(R(0,0,0,0),new[]{d}),"unknown display fails conservatively");
        Check(DesktopCoverage.FullyCovered(R(-1920,0,0,1040),new[]{R(-1920,0,-960,1040),R(-960,0,0,1040)}),"negative coordinates");
        Check(DesktopTransientWindows.Matches("FancyZones_ZonesOverlay","PowerToys.FancyZones","",false),"FancyZones drag overlay");
        Check(!DesktopTransientWindows.Matches("FancyZones_ZonesOverlay","notepad","",false),"class alone cannot exempt an unrelated app");
        Check(!DesktopTransientWindows.Matches("HwndWrapper","PowerToys.FancyZones","",false),"regular FancyZones windows still count");
        Check(!DesktopTransientWindows.Matches("WinUIDesktopWin32WindowClass","PowerToys.Settings","",false),"PowerToys settings are not exempt");
        Check(DesktopTransientWindows.Matches("MultitaskingViewFrame","explorer","",false),"Windows 10 switching UI");
        Check(DesktopTransientWindows.Matches("TaskSwitcherWnd","explorer","",false),"legacy switching UI");
        Check(DesktopTransientWindows.Matches("XamlExplorerHostIslandWindow","explorer","Task Switching",false),"Windows 11 switching UI");
        Check(DesktopTransientWindows.Matches("XamlExplorerHostIslandWindow","explorer","任务切换",false),"localized switching UI");
        Check(DesktopTransientWindows.Matches("XamlExplorerHostIslandWindow","explorer","",true),"switch event handles unknown localization");
        Check(!DesktopTransientWindows.Matches("XamlExplorerHostIslandWindow","explorer","Start",false),"unrelated shell XAML is not switching UI");
        Check(!DesktopTransientWindows.Matches("MultitaskingViewFrame","unrelated","",true),"unrelated process is not shell");
        Check(DesktopTransientWindows.Matches("Windows.UI.Core.CoreWindow","ShellExperienceHost","",true),"shell-hosted switcher event");
        var state=new DesktopCoverageState();
        Check(!state.Evaluate(false,false,0),"start on exposed desktop");
        Check(!state.Evaluate(true,true,1),"switching preserves visible models even over a fullscreen preview");
        Check(!state.Evaluate(true,true,30),"long FancyZones drag does not time out and hide models");
        Check(!state.Evaluate(true,false,31),"full coverage starts settling after interaction ends");
        Check(!state.Evaluate(true,false,31.74),"settling avoids transition flicker");
        Check(state.Evaluate(true,false,31.76),"stable genuine coverage still pauses");
        Check(state.Evaluate(false,true,32),"already paused stays paused inside switching UI");
        Check(!state.Evaluate(false,false,33),"exposed desktop resumes immediately after switching");
        Check(!state.Evaluate(true,false,34),"brief fullscreen cover is pending");
        Check(!state.Evaluate(false,false,34.1),"brief cover disappears without pausing");
        Check(!state.Evaluate(true,false,35),"new full cover restarts settling");
        Check(!state.Evaluate(true,true,35.5),"interaction clears the pending cover timer");
        Check(!state.Evaluate(true,false,36),"no stale pause at interaction end");
        Check(state.Evaluate(true,false,37),"settled coverage eventually pauses after interaction");
        Check(new DesktopCoverageState().Evaluate(true,false,0),"starting underneath fullscreen still pauses immediately");
        Check(new DesktopCoverageState().Evaluate(true,true,0),"startup switcher does not bypass actual fullscreen below it");
        // Compare the rectangle algorithm with an independent per-pixel oracle.
        var rng=new Random(421);
        for(int n=0;n<3000;n++)
        {
            var covers=new DesktopRectangle[rng.Next(1,15)];
            for(int i=0;i<covers.Length;i++) {int l=rng.Next(-4,12),t=rng.Next(-4,12);covers[i]=R(l,t,l+rng.Next(1,18),t+rng.Next(1,18));}
            bool full=true;
            for(int x=0;x<12;x++)for(int y=0;y<12;y++)
            {bool hit=false;foreach(var r in covers)if(x>=r.Left && x<r.Right && y>=r.Top && y<r.Bottom)hit=true;if(!hit)full=false;}
            Check(DesktopCoverage.FullyCovered(R(0,0,12,12),covers)==full,"pixel oracle "+n);
        }
        Console.WriteLine("Desktop coverage: "+count+" assertions passed.");
    }
}
