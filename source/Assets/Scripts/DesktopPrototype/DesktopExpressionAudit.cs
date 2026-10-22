using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace UmaDesktop
{
    public sealed partial class DesktopPrototype
    {
        private bool expressionAudit;
        [Serializable] private sealed class ExpressionCase
        {
            public int characterId,selection;public string action,label,kind,file;
            public bool defaultFace,uvMatches;public int[] actual;
        }
        [Serializable] private sealed class ExpressionAuditReport
        {public bool Passed;public int models,stateTransitions,presetPortraits;public string[] failures;public ExpressionCase[] cases;}
        private ExpressionCase CheckExpression(DesktopActor actor,string kind,string file)
        {
            bool isDefault=actor.Expressions.Action==MiniBehavior.Special && MiniExpressionPolicy.KeepsDefaultForSpecial(actor.Character.Entry.Id);
            if(kind=="state" && (actor.Expressions.Label=="Default")!=isDefault)throw new InvalidOperationException("Default-face exception mismatch.");
            if(!actor.Expressions.MatchesPreset)throw new InvalidOperationException("Facial animation overwrote selected atlas indices.");
            return new ExpressionCase {characterId=actor.Character.Entry.Id,selection=actor.Expressions.Selections,action=actor.Expressions.Action.ToString(),label=actor.Expressions.Label,
                kind=kind,file=file,defaultFace=!actor.Expressions.OverrideActive,uvMatches=actor.Expressions.MatchesPreset,actual=actor.Expressions.ActualIndices};
        }
        private IEnumerator RunExpressionAudit()
        {
            if(Environment.GetCommandLineArgs().Contains("--desktop-face-sampling-audit")){yield return RunFaceSamplingAudit();yield break;}
            string output=Path.Combine(logDirectory,"ExpressionAudit");Directory.CreateDirectory(output);
            var cases=new List<ExpressionCase>();var failures=new List<string>();int models=0,transitions=0,portraits=0;
            foreach(int id in new[]{1001,1018,1040,1105,1028})
            {
                var character=catalog.Characters.First(c=>c.Entry.Id==id);
                var outfit=character.Costumes.FirstOrDefault(c=>c.Id=="00") ?? character.Costumes.First(c=>c.Category==0);
                yield return Spawn(character,outfit.Id);
                var actor=actors.FirstOrDefault(a=>a.Character.Entry.Id==id);
                if(actor==null){failures.Add("Model did not load: "+id);break;}
                try
                {
                    actor.X=actor.Overlay.ScreenArea.center.x;actor.Phase=DesktopPhase.Active;
                    if(!actor.HasSpecial)throw new InvalidOperationException("Missing special for "+id);
                    for(int round=0;round<12;round++)
                        foreach(var action in new[]{MiniBehavior.Walk,MiniBehavior.Special,MiniBehavior.Run,MiniBehavior.Walk})
                        {
                            int before=actor.Expressions.Selections;
                            if(action==MiniBehavior.Special)actor.BeginSpecial();
                            else {actor.Schedule.ForceGait(action);actor.BeginGait();}
                            if(actor.Expressions.Selections!=before+1)throw new InvalidOperationException("State entry did not select one expression.");
                            actor.Player.Seek(.25f+round*.01f);actor.TickPose(0);
                            string file=null;
                            if(round==0)
                            {file=id+"-"+action+"-"+actor.Expressions.Label+".png";actor.CaptureExpressionPortrait(Path.Combine(output,file));}
                            cases.Add(CheckExpression(actor,"state",file));transitions++;
                        }
                    // Continue the same multi-part special without selecting again.
                    actor.BeginSpecial();int selected=actor.Expressions.Selections;string label=actor.Expressions.Label;
                    actor.Player.Seek(actor.Player.Duration+0.01f);actor.Step(.01f);actor.TickPose(0);
                    if(actor.Expressions.Selections!=selected || actor.Expressions.Label!=label)throw new InvalidOperationException("Subclip changed the expression.");
                    cases.Add(CheckExpression(actor,"subclip",null));
                    // Paired performance uses the same special-state policy.
                    actor.BeginInteraction(pairMotions[0].A,999);actor.Player.Seek(.5f);actor.TickPose(0);
                    cases.Add(CheckExpression(actor,"state",null));transitions++;
                    if(id==1001)
                        foreach(var preset in MiniExpressionPolicy.Presets)
                        {
                            actor.Schedule.ForceGait(MiniBehavior.Walk);actor.BeginGait();actor.Player.Seek(.25f);
                            actor.Expressions.SetAuditPreset(preset.Label);actor.TickPose(0);
                            string file="preset-"+preset.Label+".png";actor.CaptureExpressionPortrait(Path.Combine(output,file));
                            cases.Add(CheckExpression(actor,"preset",file));portraits++;
                        }
                    models++;
                }
                catch(Exception ex){failures.Add(id+": "+ex);}
                Remove(actor);yield return null;
            }
            var report=new ExpressionAuditReport {Passed=failures.Count==0 && models==5 && transitions==245 && portraits==12,models=models,stateTransitions=transitions,presetPortraits=portraits,failures=failures.ToArray(),cases=cases.ToArray()};
            File.WriteAllText(Path.Combine(output,"expression-report.json"),JsonUtility.ToJson(report,true));
            Record("Expression audit Passed="+report.Passed+" transitions="+transitions);WriteStatus();Application.Quit();
        }
    }
}
