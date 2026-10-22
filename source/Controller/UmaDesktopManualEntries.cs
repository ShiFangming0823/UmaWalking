using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using UmaDesktop;

internal sealed partial class ControlPanel
{
    private readonly ComboBox entryBox=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,AccessibleName=UiText.Get("选择要上场的马娘")};
    private readonly Label entryQueue=new Label {AutoSize=true,MaximumSize=new System.Drawing.Size(600,0),Text=UiText.Get("上场队列：无；达到数量上限时等待空位。")};
    private Button enterButton,retireButton;
    private void AddEntryControls(TableLayoutPanel layout)
    {
        var row=new TableLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,ColumnCount=2,Margin=new Padding(0,8,0,0)};
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(entryBox,0,0);enterButton=Button(UiText.Get("指定角色上场"),EnterSelected);row.Controls.Add(enterButton,1,0);
        layout.RowCount=8;layout.Controls.Add(row,0,6);layout.SetColumnSpan(row,2);layout.Controls.Add(entryQueue,0,7);layout.SetColumnSpan(entryQueue,2);
    }
    private void EnterSelected()
    {
        var selected=entryBox.SelectedItem as CharacterItem;
        if(selected==null){message.Text=UiText.Get("请先选择要上场的角色。");return;}
        Send("enter",new Dictionary<string,object>{{"characterId",selected.Id}});
        message.Text=UiText.Get("已请求 ")+selected.DisplayName+UiText.Get(" 上场；达到数量上限时等待空位，不会挤走其他角色。");
    }
    private string entryLanguage="";
    private void UpdateEntryChoices()
    {
        if(entryBox.Items.Count!=catalog.Count||entryLanguage!=UiText.Language)
        {
            entryLanguage=UiText.Language;var old=entryBox.SelectedItem as CharacterItem;entryBox.BeginUpdate();entryBox.Items.Clear();
            foreach(var c in catalog)entryBox.Items.Add(c);
            int index=old==null?0:catalog.FindIndex(c=>c.Id==old.Id);entryBox.SelectedIndex=catalog.Count==0?-1:Math.Max(0,index);entryBox.EndUpdate();
        }
        entryQueue.Text=EntryQueueText(state);
    }
    private static string EntryQueueText(Dictionary<string,object> snapshot)
    {
        var entries=snapshot!=null && snapshot.ContainsKey("pendingEntries")?ArrayOf(snapshot["pendingEntries"]).Cast<Dictionary<string,object>>().ToList():new List<Dictionary<string,object>>();
        return entries.Count==0?UiText.Get("上场队列：无；达到数量上限时等待空位。"):UiText.Get("等待上场（按顺序）：")+string.Join(" → ",entries.Take(3).Select(e=>DesktopCharacterNames.Get(I(e,"characterId"),S(e,"name"),UiText.Language)))+(entries.Count>3?UiText.Get(" …（共 ")+entries.Count+UiText.Get(" 名）"):"");
    }
}
