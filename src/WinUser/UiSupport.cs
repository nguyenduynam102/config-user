using System.Drawing;
using System.Text.Json;
namespace WinUser;
internal static class Ui
{
    public static readonly Color Blue=Color.FromArgb(35,91,173), Ink=Color.FromArgb(31,45,65), Muted=Color.FromArgb(88,104,125), Surface=Color.FromArgb(244,247,251);
    public static Image Icon(string glyph,Color? color=null)
    {
        var b=new Bitmap(20,20);using var g=Graphics.FromImage(b);using var f=new Font("Segoe MDL2 Assets",13);using var brush=new SolidBrush(color??Blue);
        g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;g.DrawString(glyph,f,brush,-1,0);return b;
    }
    public static Label Caption(string text)=>new(){Text=text,AutoSize=true,ForeColor=Muted,Margin=new Padding(3,8,8,3)};
    public static TextBox Text(int width=250,string? placeholder=null)=>new(){Width=width,PlaceholderText=placeholder??"",Margin=new Padding(3,5,10,5)};
}
internal sealed class UiPreferences
{
    public int Width {get;set;}=1360;public int Height {get;set;}=880;public int Split {get;set;}=500;
    public bool Maximized {get;set;}public int Filter {get;set;}public string PrivateRoot {get;set;}="";
    public Dictionary<string,int> Columns {get;set;}=new();
    static string FilePath=>Path.Combine(Storage.Root,"ui-preferences.json");
    public static UiPreferences Load(){try{return Storage.Read(FilePath,new UiPreferences());}catch{return new();}}
    public void Save()=>Storage.Write(FilePath,this);
}
internal sealed class PasswordDialog:Form
{
    readonly TextBox password=new(){UseSystemPasswordChar=true,Width=390},confirmation=new(){UseSystemPasswordChar=true,Width=390};
    public string Password=>password.Text;
    public PasswordDialog(string title,string target,bool reset)
    {
        Text=title;Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(500,reset?480:380);MinimumSize=new Size(450,300);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
        var flow=new FlowLayoutPanel(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(20)};
        flow.Controls.Add(new Label(){Text=target,Width=420,Height=48,ForeColor=Ui.Ink});
        flow.Controls.Add(Ui.Caption("Mật khẩu mới"));flow.Controls.Add(password);flow.Controls.Add(Ui.Caption("Nhập lại mật khẩu"));flow.Controls.Add(confirmation);
        var show=new CheckBox(){Text="Hiện mật khẩu",AutoSize=true,Margin=new Padding(3,10,3,10)};show.CheckedChanged+=(_,_)=>password.UseSystemPasswordChar=confirmation.UseSystemPasswordChar=!show.Checked;flow.Controls.Add(show);
        if(reset)flow.Controls.Add(new Label(){Text="Đặt lại mật khẩu có thể ảnh hưởng EFS, chứng chỉ và thông tin xác thực đã lưu. Không tự buộc đổi mật khẩu ở lần đăng nhập sau (có thể cản RDP/NLA).",Width=420,Height=70,ForeColor=Color.Maroon});
        var bar=new FlowLayoutPanel(){Dock=DockStyle.Bottom,Height=55,Padding=new Padding(8)};var ok=new Button(){Text="Tiếp tục",AutoSize=true};var cancel=new Button(){Text="Hủy",DialogResult=DialogResult.Cancel,AutoSize=true};ok.Click+=(_,_)=>{try{AccountRules.Password(password.Text,confirmation.Text);DialogResult=DialogResult.OK;}catch(Exception e){MessageBox.Show(this,e.Message,"Kiểm tra mật khẩu");}};bar.Controls.AddRange(new Control[]{ok,cancel});Controls.Add(flow);Controls.Add(bar);AcceptButton=ok;CancelButton=cancel;
    }
    protected override void Dispose(bool disposing){if(disposing){password.Clear();confirmation.Clear();}base.Dispose(disposing);}
}
