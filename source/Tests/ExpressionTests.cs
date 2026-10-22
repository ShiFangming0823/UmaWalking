using System;
using System.Collections.Generic;
using UmaDesktop;
class ExpressionTests
{
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static bool In(string value,string list){return Array.IndexOf(list.Split(','),value)>=0;}
    sealed class ZeroRandom:Random{public override int Next(int max){return 0;}public override double NextDouble(){return 0;}}
    sealed class FixedRandom:Random {readonly double roll;readonly bool last;public FixedRandom(double roll,bool last=false){this.roll=roll;this.last=last;}public override int Next(int max){return last?max-1:0;}public override double NextDouble(){return roll;}}
    static void Main()
    {
        var rng=new Random(7843);var counts=new Dictionary<string,int>();
        foreach(var p in MiniExpressionPolicy.Presets)counts[p.Label]=0;
        const int N=300000;
        for(int i=0;i<N;i++)
        {
            string w=MiniExpressionPolicy.Choose(rng,MiniBehavior.Walk,1001);counts[w]++;
            Check(In(w,"WaraiA,WaraiB,WaraiC,WaraiD,WaraiE,SleepA,SleepB"),"Walking pool");
            string s=MiniExpressionPolicy.Choose(rng,MiniBehavior.Special,1001);
            Check(In(s,"WaraiA,WaraiB,WaraiC,WaraiD,WaraiE"),"Special smile pool");
            string run=MiniExpressionPolicy.Choose(rng,MiniBehavior.Run,1001);counts[run]++;
            Check(In(run,"KiriA,KiriB,KiriC,GuruguruA,FightA"),"Running pool");
        }
        double smiles=(counts["WaraiA"]+counts["WaraiB"]+counts["WaraiC"]+counts["WaraiD"]+counts["WaraiE"])/(double)N;
        Check(Math.Abs(smiles-.8)<.005,"Walking smile group must be 80 percent");
        foreach(string label in new[]{"WaraiA","WaraiB","WaraiC","WaraiD","WaraiE"})Check(Math.Abs(counts[label]/(double)N-.16)<.004,"Walking smile variants each receive 16 percent");
        foreach(string label in new[]{"SleepA","SleepB"})Check(Math.Abs(counts[label]/(double)N-.1)<.004,"Walking sleep variants each receive 10 percent");
        Check(MiniExpressionPolicy.Choose(new FixedRandom(.799999),MiniBehavior.Walk,1001)=="WaraiA","Below 80 percent selects smile group");
        Check(MiniExpressionPolicy.Choose(new FixedRandom(.8),MiniBehavior.Walk,1001)=="SleepA","80 percent boundary selects sleep group");
        Check(MiniExpressionPolicy.Choose(new FixedRandom(.999999,true),MiniBehavior.Walk,1001)=="SleepB","Last group member remains selectable");
        double focused=(counts["KiriA"]+counts["KiriB"]+counts["KiriC"])/(double)N;
        Check(Math.Abs(focused-.9)<.005,"Running must be 90 percent focused");
        foreach(string l in new[]{"KiriA","KiriB","KiriC"})Check(Math.Abs(counts[l]/(double)N-.3)<.005,"Focused variants are equiprobable");
        foreach(string l in new[]{"GuruguruA","FightA"})Check(Math.Abs(counts[l]/(double)N-.05)<.003,"Rare variants are equiprobable");
        foreach(int id in new[]{1018,1040,1105})
        {
            for(int i=0;i<100;i++)
            {Check(MiniExpressionPolicy.Choose(rng,MiniBehavior.Special,id)=="Default","Exception keeps default on special");Check(MiniExpressionPolicy.Choose(rng,MiniBehavior.Walk,id)!="Default","Exception still changes while walking");Check(MiniExpressionPolicy.Choose(rng,MiniBehavior.Run,id)!="Default","Exception still changes while running");}
        }
        var zero=new ZeroRandom();string first=MiniExpressionPolicy.Choose(zero,MiniBehavior.Walk,1001);
        Check(first==MiniExpressionPolicy.Choose(zero,MiniBehavior.Walk,1001),"Consecutive states may choose the same expression");
        Check(MiniExpressionPolicy.Presets.Length==12,"Exactly the twelve requested labels");
        Check(MiniExpressionPolicy.Find("SleepA").Mouth==12 && MiniExpressionPolicy.Find("WaraiE").Mouth==17,"Use full preset, not only eyes");
        Check(MiniExpressionPolicy.Find("KiriB").EyebrowLeft==5 && MiniExpressionPolicy.Find("FightA").EyeLeft==7,"Atlas rows are preserved");
        Console.WriteLine("PASS: "+checks+" expression assertions; walkSmiles="+smiles.ToString("F5")+", walkSleep="+(1-smiles).ToString("F5")+"; focused="+focused.ToString("F5")+", rare="+(1-focused).ToString("F5"));
    }
}
