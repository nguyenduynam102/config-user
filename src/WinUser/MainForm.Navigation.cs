using System.Drawing;
namespace WinUser;
// A single keyboard-accessible selector replaces the crowded scrolling tab strip.
internal sealed class NavigationTabs:TabControl
{
    public NavigationTabs(){Appearance=TabAppearance.FlatButtons;SizeMode=TabSizeMode.Fixed;ItemSize=new Size(1,1);Multiline=true;TabStop=false;}
    protected override void WndProc(ref Message message)
    {
        if(message.Msg==0x1328&&!DesignMode){message.Result=IntPtr.Zero;return;}
        base.WndProc(ref message);
    }
}
public sealed partial class MainForm
{
    readonly ComboBox functionPicker=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=340,AccessibleName="Chọn nhóm chức năng"};
    readonly List<(Button Button,int Minimum,int Maximum)> targetActions=new();
    readonly List<Control> targetGroups=new();
    void BuildNavigation(Panel panel,TabControl tabs)
    {
        var names=new[]{"Tài khoản  /  User & RDP","Cá nhân hóa  /  Desktop","Cá nhân hóa  /  Registry","Dữ liệu  /  Thư mục riêng","Thiết lập  /  Cấu hình mặc định","Dữ liệu  /  NAS & ổ đĩa","Hệ thống  /  Sao lưu & nhật ký"};
        functionPicker.Items.AddRange(names);functionPicker.SelectedIndex=0;
        functionPicker.SelectedIndexChanged+=(_,_)=>{if(functionPicker.SelectedIndex>=0)tabs.SelectedIndex=functionPicker.SelectedIndex;};
        tabs.SelectedIndexChanged+=(_,_)=>{if(tabs.SelectedIndex>=0)functionPicker.SelectedIndex=tabs.SelectedIndex;};
        var bar=Bar();bar.Controls.AddRange(new Control[]{new Label(){Text="Chức năng:",AutoSize=true,Margin=new Padding(4,8,4,4)},functionPicker});panel.Controls.Add(bar);
        UpdateTargetActions(0);
    }
    Button TargetButton(string title,Action action,bool single=false)
    {
        var button=Button(title,action);button.Visible=false;targetActions.Add((button,1,single?1:int.MaxValue));return button;
    }
    void UpdateTargetActions(int count)
    {
        foreach(var item in targetActions){item.Button.Visible=count>=item.Minimum;item.Button.Enabled=count<=item.Maximum;}
        foreach(var group in targetGroups)group.Visible=count>0;
        if(accountSelectionHint!=null)accountSelectionHint.Text=count==0?"Chọn user ĐÍCH bên trái để hiện các lệnh quản lý tài khoản, RDP và phiên.":$"Đang chọn {count} user. Lệnh sửa/đổi tên/đổi mật khẩu/xóa/nhóm chỉ dùng khi chọn một user.";
    }
    GroupBox AccountGroup(string title,params Control[] controls)
    {
        var group=new GroupBox(){Text=title,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(8),Margin=new Padding(8),MinimumSize=new Size(370,0)};
        var flow=new FlowLayoutPanel(){AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,WrapContents=true,MaximumSize=new Size(560,0),Padding=new Padding(4)};flow.Controls.AddRange(controls);group.Controls.Add(flow);targetGroups.Add(group);group.Visible=false;return group;
    }
}
