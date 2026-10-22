using System;
using System.Linq;

namespace UmaDesktop
{
    public sealed partial class DesktopPrototype
    {
        private void ApplyActorLimit(int value)
        {
            settings.MaxActors=persisted.MaxActors=DesktopLimits.Clamp(value);
            RefreshIncludeVariantPolicy();drawBag.Clear();
            ReconcileDuplicateNames();ReconcilePopulationLimit();
            if(population!=null && !actors.Any(a=>a.Phase==DesktopPhase.Exiting))population.RetryAfter(now,0);
            message="模型数量上限："+settings.MaxActors+"；多余角色会走向出口后离场。";
            Record("Actor limit="+settings.MaxActors+" count="+actors.Count+" target="+TargetCount);
        }
        private void ReconcilePopulationLimit()
        {
            if(catalog==null)return;
            // Retiring actors keep their physical slot until fully removed. Only
            // non-retiring actors determine how many *additional* exits are due.
            var active=actors.Where(a=>a.Phase!=DesktopPhase.Exiting).ToArray();
            int excess=DesktopLimits.Excess(active.Length,TargetCount);
            // Prefer to retain explicit choices and older arrivals. A lower
            // global limit also applies to explicit choices when necessary.
            foreach(var a in active.OrderBy(a=>a.ManualEntry).ThenByDescending(a=>a.Ordinal).Take(excess))Retire(a);
        }
    }
}
