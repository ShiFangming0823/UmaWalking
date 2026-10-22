using System;
using System.Collections.Generic;
using System.Linq;

namespace UmaDesktop
{
    public sealed partial class DesktopPrototype
    {
        private bool IncludeVariants;
        private void RefreshIncludeVariantPolicy()
        {
            IncludeVariants=false;
            if(catalog==null||settings.SelectionMode!="Include")return;
            int count=catalog.Characters.Where(c=>settings.IncludeIds.Contains(c.Entry.Id))
                .Select(c=>DesktopRules.NameKey(c.Entry.Name)).Distinct(StringComparer.Ordinal).Count();
            IncludeVariants=DesktopLimits.AllowVariants(count,settings.MaxActors);
        }
        private bool MayRepeat(MiniMotionCatalog.Character character)
        {return IncludeVariants&&settings.IncludeIds.Contains(character.Entry.Id);}
        private bool SameCharacter(DesktopActor actor,MiniMotionCatalog.Character character)
        {return DesktopRules.NameKey(actor.Character.Entry.Name)==DesktopRules.NameKey(character.Entry.Name);}
        private List<MiniMotionCatalog.Costume> FreeCostumes(MiniMotionCatalog.Character character,DesktopActor replacing=null)
        {
            var occupied=new HashSet<string>(actors.Where(a=>a!=replacing&&SameCharacter(a,character)).Select(a=>a.Costume.Id),StringComparer.Ordinal);
            return character.Costumes.Where(c=>!occupied.Contains(c.Id)).ToList();
        }
        private bool CanSpawnCharacter(MiniMotionCatalog.Character character,DesktopActor replacing=null)
        {return FreeCostumes(character,replacing).Count>0&&(MayRepeat(character)||!actors.Any(a=>a!=replacing&&SameCharacter(a,character)));}
        private MiniMotionCatalog.Costume PickFreeCostume(MiniMotionCatalog.Character character,DesktopActor replacing=null)
        {
            var free=FreeCostumes(character,replacing);
            if(replacing!=null&&free.Any(c=>c.Id!=replacing.Costume.Id))free=free.Where(c=>c.Id!=replacing.Costume.Id).ToList();
            return catalog.PickCostume(character,random,-1,null,free);
        }
        private void ReconcileDuplicateNames()
        {
            // A filter change lets excess copies walk out instead of deleting them.
            foreach(var group in actors.Where(a=>a.Phase!=DesktopPhase.Exiting).GroupBy(a=>DesktopRules.NameKey(a.Character.Entry.Name)))
            {
                if(MayRepeat(group.First().Character))continue;
                foreach(var actor in group.OrderByDescending(a=>a.ManualEntry).ThenBy(a=>a.Ordinal).Skip(1).ToArray())Retire(actor);
            }
        }
        private bool OccupancyValid
        {
            get
            {
                if(actors.Count>DesktopLimits.Maximum)return false;
                if(actors.GroupBy(a=>DesktopRules.NameKey(a.Character.Entry.Name)+"\n"+a.Costume.Id).Any(g=>g.Count()>1))return false;
                return actors.Where(a=>a.Phase!=DesktopPhase.Exiting).GroupBy(a=>DesktopRules.NameKey(a.Character.Entry.Name))
                    .All(g=>g.Count()==1||MayRepeat(g.First().Character));
            }
        }
    }
}
