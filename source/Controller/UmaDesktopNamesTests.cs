using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using UmaDesktop;

internal sealed partial class ControlPanel
{
    internal static void RunNameTests(string root)
    {
        int checks=0;Action<bool,string> check=(value,text)=>{if(!value)throw new Exception(text);checks++;};
        using(var panel=new ControlPanel(root,0,true,()=>{}))
        {
            panel.Show();panel.Poll(true);Application.DoEvents();
            check(panel.catalog.Count>=136,"Complete character roster");
            var rows=new List<object>();
            foreach(var c in panel.catalog)
            {
                string ja=DesktopCharacterNames.Get(c.Id,c.Name,"ja"),zh=DesktopCharacterNames.Get(c.Id,c.Name,"zh"),en=DesktopCharacterNames.Get(c.Id,c.Name,"en");
                check(ja==c.Name,"Japanese raw names preserved: "+c.Id);
                check(!string.IsNullOrEmpty(zh)&&zh!=ja&&!Regex.IsMatch(zh,@"[\u3040-\u30ff]"),"Chinese coverage: "+c.Id);
                check(!string.IsNullOrEmpty(en)&&!Regex.IsMatch(en,@"[\u3040-\u9fff]"),"English coverage: "+c.Id);
                foreach(string query in new[]{ja,zh,en.ToLowerInvariant(),c.Id.ToString()})check(DesktopCharacterNames.Matches(c.Id,c.Name,query),"Cross-language search: "+c.Id);
                rows.Add(new {id=c.Id,ja=ja,zh=zh,en=en});
            }
            check(DesktopCharacterNames.Get(1149,"","en")=="Phalaenopsis"&&DesktopCharacterNames.Get(1149,"","zh")=="蝴蝶兰","Newest character completed");
            check(DesktopCharacterNames.Get(9001,"","zh")=="骏川手纲","Formal support character name");
            check(DesktopCharacterNames.Get(999999,"Future JP","en")=="Future JP","Unknown future IDs preserve fallback");
            var selected=panel.entryBox.Items.Cast<CharacterItem>().First(c=>c.Id==1001);panel.entryBox.SelectedItem=selected;
            int token=panel.SelectedToken;var includeIds=panel.included.OrderBy(i=>i).ToArray();
            string costumes=panel.json.Serialize(panel.catalog.Select(c=>new {id=c.Id,costumes=c.Costumes.Select(d=>new {id=d.Id,name=d.Name})}));
            var rawActor=ArrayOf(panel.state["actors"]).Cast<Dictionary<string,object>>().First();
            foreach(string language in new[]{"ja","en","zh"})
            {
                panel.languageBox.SelectedIndex=language=="ja"?1:language=="en"?2:0;panel.Poll(true);Application.DoEvents();
                string name=DesktopCharacterNames.Get(1001,"",language);
                check(panel.list.Items.Cast<CharacterItem>().First(c=>c.Id==1001).ToString().Contains(name),"Localized filter list: "+language);
                check(panel.entryBox.Text.Contains(name),"Localized manual entry list: "+language);
                check(((Choice)panel.actorBox.SelectedItem).Label.Contains(DesktopCharacterNames.Get(I(rawActor,"characterId"),S(rawActor,"name"),language)),"Localized on-screen actor: "+language);
                check(panel.SelectedToken==token&&((CharacterItem)panel.entryBox.SelectedItem).Id==1001&&panel.included.OrderBy(i=>i).SequenceEqual(includeIds),"Language switch preserves identities and selections");
                var queue=new Dictionary<string,object>{{"pendingEntries",new[]{new Dictionary<string,object>{{"characterId",1001},{"name","スペシャルウィーク"}}}}};
                check(EntryQueueText(queue).Contains(name),"Localized entry queue");
                string outfit="[スペシャルウィークの勝負服・地面]";
                string message=LocalizeRuntimeMessage("已加载 スペシャルウィーク · "+outfit);
                check(message.Contains(name)&&message.EndsWith(" · "+outfit,StringComparison.Ordinal),"Only character segment is translated, including Japanese name inside outfit");
                check(panel.json.Serialize(panel.catalog.Select(c=>new {id=c.Id,costumes=c.Costumes.Select(d=>new {id=d.Id,name=d.Name})}))==costumes,"Every outfit stays byte-for-byte Japanese");
                panel.tabs.SelectedIndex=0;panel.PerformLayout();Application.DoEvents();
                using(var preview=new Bitmap(panel.Width,panel.Height)){panel.DrawToBitmap(preview,new Rectangle(0,0,panel.Width,panel.Height));preview.Save(Path.Combine(root,"names-"+language+".png"));}
            }
            File.WriteAllText(Path.Combine(root,"character-names-coverage.json"),panel.json.Serialize(rows),new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root,"names-selftest-report.json"),panel.json.Serialize(new {Passed=true,Checks=checks,Characters=panel.catalog.Count,OutfitNamesUnchanged=true,Scope="Real panel language handlers, all names, cross-language search and identity preservation. No physical input simulation."}),new UTF8Encoding(false));
        }
    }
}
