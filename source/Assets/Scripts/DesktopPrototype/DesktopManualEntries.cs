using System;
using System.Linq;
using UnityEngine;

namespace UmaDesktop
{
    public sealed partial class DesktopPrototype
    {
        private readonly ManualEntryQueue manualEntries=new ManualEntryQueue();
        private float manualRetryAt;
        [Serializable] private sealed class PendingEntryExport {public int characterId;public string name,displayName;}
        private int DesiredPopulation
        {
            get
            {
                // Manual choices reserve their own slots; random filters only
                // control random additions and never immediately evict a choice.
                if(IncludeVariants)
                {
                    var manual=actors.Where(a=>a.ManualEntry).ToArray();
                    int capacity=Eligible().SelectMany(c=>c.Costumes.Select(d=>DesktopRules.NameKey(c.Entry.Name)+"\n"+d.Id)).Distinct(StringComparer.Ordinal)
                        .Count(key=>!manual.Any(a=>key==DesktopRules.NameKey(a.Character.Entry.Name)+"\n"+a.Costume.Id));
                    return DesktopLimits.Target(settings.MaxActors,manual.Length,capacity);
                }
                var names=new System.Collections.Generic.HashSet<string>(actors.Where(a=>a.ManualEntry).Select(a=>DesktopRules.NameKey(a.Character.Entry.Name)));
                return DesktopLimits.Target(settings.MaxActors,names.Count,Eligible().Select(c=>DesktopRules.NameKey(c.Entry.Name)).Distinct().Count(n=>!names.Contains(n)));
            }
        }
        private void RequestManualEntry(int id)
        {
            var character=catalog==null?null:catalog.Characters.FirstOrDefault(c=>c.Entry.Id==id);
            if(character==null){message="请选择可用的上场角色。";return;}
            var result=manualEntries.Request(id,character.Entry.Name,MayRepeat(character)?Enumerable.Empty<string>():actors.Select(a=>a.Character.Entry.Name));
            if(result==ManualEntryResult.AlreadyPresent){message=character.Entry.Name+" 已在场上或正在退场，不能重复上场。";return;}
            if(result==ManualEntryResult.AlreadyQueued){message=character.Entry.Name+" 已在上场队列中。";return;}
            manualRetryAt=0;
            message=character.Entry.Name+(EffectivePaused?" 已排队，恢复运行后上场。":actors.Count>=settings.MaxActors?" 已排队，等待空位后上场。":" 即将从入口上场。");
            Record("Manual entry queued character="+id+" count="+manualEntries.Count);
        }
        private bool TryStartManualEntry()
        {
            if(busy || EffectivePaused || now<manualRetryAt)return false;
            var request=manualEntries.Next(actors.Count,settings.MaxActors);if(request==null)return false;
            var character=catalog.Characters.FirstOrDefault(c=>c.Entry.Id==request.CharacterId);
            if(character==null){manualEntries.Complete(request.CharacterId);return false;}
            if(!CanSpawnCharacter(character))return false;
            StartCoroutine(Spawn(character,null,null,true));return true;
        }
        private PendingEntryExport[] ExportPendingEntries()
        {
            if(catalog==null)return new PendingEntryExport[0];
            return manualEntries.Ids.Select(id=>catalog.Characters.FirstOrDefault(c=>c.Entry.Id==id)).Where(c=>c!=null)
                .Select(c=>new PendingEntryExport {characterId=c.Entry.Id,name=c.Entry.Name,displayName=DesktopCharacterNames.Get(c.Entry.Id,c.Entry.Name,settings.UiLanguage)}).ToArray();
        }
        private void RequestManualExit(DesktopActor actor)
        {
            if(actor==null){message="请先选择场上的角色。";return;}
            if(actor.MotionHeld)
            {
                DesktopMouseInput.CancelGesture();
                if(actor.Dragging)actor.FinishDrag();
                actor.PointerHeld=actor.PairHeld=actor.Dragging=false;
                actor.Overlay.TakePointerEvents();actor.Overlay.TakeClicks();
            }
            Retire(actor);message=actor.Character.Entry.Name+" 正在前往出口，离开后释放空位。";
        }
    }

    public sealed partial class DesktopActor
    {
        public bool ManualEntry;
        private float DefaultPortalOpacity
        {
            get
            {
                if(Phase==DesktopPhase.Active)return 1;
                float edge=Phase==DesktopPhase.Entering
                    ?(Direction>0?X-Overlay.ScreenArea.xMin:Overlay.ScreenArea.xMax-X)
                    :(Direction>0?Overlay.ScreenArea.xMax-X:X-Overlay.ScreenArea.xMin);
                return Mathf.InverseLerp(-SurfaceSize*.5f,Height*.32f,edge);
            }
        }
    }
}
