using System;
using System.Collections.Generic;
namespace UmaDesktop
{
    public sealed class MiniExpressionPreset
    {
        public readonly string Label;
        public readonly int EyebrowLeft,EyebrowRight,EyeLeft,EyeRight,Mouth;
        public MiniExpressionPreset(string label,int bl,int br,int el,int er,int mouth)
        {Label=label;EyebrowLeft=bl;EyebrowRight=br;EyeLeft=el;EyeRight=er;Mouth=mouth;}
        public int[] Indices {get{return new[]{EyebrowLeft,EyebrowRight,EyeLeft,EyeRight,Mouth};}}
    }
    public static class MiniExpressionPolicy
    {
        // Atlas indices verified against this game's master.mdb mini_face_type_data,
        // matching the previously rendered 36-preset expression gallery.
        public static readonly MiniExpressionPreset[] Presets={
            new MiniExpressionPreset("WaraiA",0,0,0,0,1),
            new MiniExpressionPreset("WaraiB",0,0,0,0,2),
            new MiniExpressionPreset("WaraiC",0,0,9,9,1),
            new MiniExpressionPreset("WaraiD",0,0,9,9,2),
            new MiniExpressionPreset("WaraiE",0,0,9,9,17),
            new MiniExpressionPreset("SleepA",1,1,10,10,12),
            new MiniExpressionPreset("SleepB",1,1,10,10,2),
            new MiniExpressionPreset("KiriA",4,4,6,6,1),
            new MiniExpressionPreset("KiriB",5,5,6,6,1),
            new MiniExpressionPreset("KiriC",5,5,0,0,1),
            new MiniExpressionPreset("GuruguruA",3,3,14,14,11),
            new MiniExpressionPreset("FightA",5,5,7,7,7)
        };
        private static readonly string[] smiles={"WaraiA","WaraiB","WaraiC","WaraiD","WaraiE"};
        private static readonly string[] sleeping={"SleepA","SleepB"};
        private static readonly string[] focused={"KiriA","KiriB","KiriC"};
        private static readonly string[] rare={"GuruguruA","FightA"};
        public static bool KeepsDefaultForSpecial(int characterId)
        {return characterId==1018 || characterId==1040 || characterId==1105;}
        public static string Choose(Random random,MiniBehavior action,int characterId)
        {
            if(action==MiniBehavior.Special && KeepsDefaultForSpecial(characterId))return "Default";
            string[] pool=action==MiniBehavior.Walk ? (random.NextDouble()<0.8 ? smiles : sleeping) : action==MiniBehavior.Special ? smiles : random.NextDouble()<0.9 ? focused : rare;
            return pool[random.Next(pool.Length)];
        }
        public static MiniExpressionPreset Find(string label)
        {
            if(label=="Default")return null;
            foreach(var preset in Presets)if(preset.Label==label)return preset;
            throw new ArgumentException("Unknown mini expression: "+label);
        }
    }
}
