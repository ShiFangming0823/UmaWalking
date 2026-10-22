using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using UmaDesktop;

internal sealed partial class ControlPanel
{
    private readonly NumericUpDown actorLimitBox=new NumericUpDown {Minimum=1,Maximum=10,Value=6,Width=85,TextAlign=HorizontalAlignment.Center,AccessibleName="模型上限数量"};
    private readonly Timer actorLimitTimer=new Timer {Interval=220};
    private bool syncingActorLimit,dirtyActorLimit,awaitingActorLimit;
    private int requestedActorLimit=6;
    private void AddActorLimitControls(TableLayoutPanel layout)
    {
        var row=new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,8,0,0),WrapContents=false};
        row.Controls.Add(new Label {Text="模型上限数量",AutoSize=true,Padding=new Padding(0,5,8,0)});
        row.Controls.Add(actorLimitBox);row.Controls.Add(new Label {Text="入场和退场均计入数量。",AutoSize=true,Padding=new Padding(8,5,0,0)});
        layout.RowCount=9;layout.Controls.Add(row,0,8);layout.SetColumnSpan(row,2);
        var saved=ReadSavedSettings();syncingActorLimit=true;
        actorLimitBox.Value=DesktopLimits.Clamp(saved.ContainsKey("MaxActors")?I(saved,"MaxActors"):DesktopLimits.Default);syncingActorLimit=false;
        actorLimitBox.ValueChanged+=(s,e)=>{if(syncingActorLimit)return;dirtyActorLimit=true;actorLimitTimer.Stop();actorLimitTimer.Start();};
        actorLimitBox.Leave+=(s,e)=>FlushActorLimit();actorLimitTimer.Tick+=(s,e)=>FlushActorLimit();
    }
    private void FlushActorLimit()
    {
        actorLimitTimer.Stop();if(!dirtyActorLimit)return;dirtyActorLimit=false;
        requestedActorLimit=Convert.ToInt32(actorLimitBox.Value);awaitingActorLimit=true;
        Send("actorLimit",new Dictionary<string,object>{{"maxActors",requestedActorLimit}});
    }
    private void PollActorLimit()
    {
        if(state==null||!state.ContainsKey("maxActors"))return;
        int actual=DesktopLimits.Clamp(I(state,"maxActors"));
        if(awaitingActorLimit&&actual==requestedActorLimit)awaitingActorLimit=false;
        if(dirtyActorLimit||awaitingActorLimit)return;
        syncingActorLimit=true;actorLimitBox.Value=actual;syncingActorLimit=false;
    }
}
