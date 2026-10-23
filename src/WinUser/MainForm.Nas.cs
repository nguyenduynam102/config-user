using System.ServiceProcess;
namespace WinUser;
public sealed partial class MainForm
{
    readonly DataGridView nasGrid=new(){Dock=DockStyle.Fill,ReadOnly=true,AutoGenerateColumns=false,AllowUserToAddRows=false,AllowUserToDeleteRows=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,RowHeadersVisible=false,BackgroundColor=System.Drawing.Color.White};
    sealed record NasRow(string WindowsUser,string Sid,string Drive,string Unc,string Login);
    void BuildNasPage(TabControl tabs)
    {
        var page=Page(tabs,"NAS");
        foreach(var (field,title,width) in new[]{("WindowsUser","User Windows",140),("Drive","Ổ",50),("Unc","Đường dẫn SMB",280),("Login","Tài khoản NAS",180)})nasGrid.Columns.Add(new DataGridViewTextBoxColumn(){DataPropertyName=field,HeaderText=title,Width=width});
        page.Controls.Add(nasGrid);var bar=Bar();
        bar.Controls.AddRange(new Control[]{Button("Thêm ánh xạ",()=>EditNasMapping(false)),Button("Sửa ánh xạ",()=>EditNasMapping(true)),Button("Xóa ánh xạ đã chọn",DeleteNasMapping),Button("Kiểm tra hệ thống NAS",()=>MessageBox.Show(this,Nas.Health(),"Broker / Agent")),Button("Kiểm tra SMB",TestSelectedNas),Button("Làm mới",RefreshNas)});page.Controls.Add(bar);
        page.Controls.Add(Note("Thêm: tick các user Windows bên trái rồi bấm Thêm ánh xạ. Sửa/Xóa: chọn một dòng trong bảng. Phải cài Broker/Agent bằng Install.cmd; lưu cấu hình chưa đồng nghĩa đã kết nối. Broker tự khởi chạy Agent cho user đang đăng nhập; user mới nhận mapping khi đăng nhập. Kiểm tra SMB ở đây dùng phiên Administrator, không thay cho kiểm tra phiên user."));
    }
    void RefreshNas()
    {
        var names=WindowsAccounts.List().ToDictionary(u=>u.Sid,u=>u.Name);nasGrid.DataSource=Nas.Load().Select(m=>new NasRow(names.GetValueOrDefault(m.Sid,"SID không còn tồn tại"),m.Sid,m.Drive,m.Unc,m.User)).ToList();nasGrid.ClearSelection();nasGrid.CurrentCell=null;
    }
    NasRow SelectedNas()=>nasGrid.CurrentRow?.DataBoundItem as NasRow??throw new ArgumentException("Chọn một dòng ánh xạ trong bảng NAS.");
    void EditNasMapping(bool edit)
    {
        NasRow? old=edit?SelectedNas():null;var target=edit?WindowsAccounts.List().Where(u=>u.Sid==old!.Sid).ToList():Selected();
        using var dialog=new Form(){Text=edit?"Sửa ánh xạ NAS":"Thêm ánh xạ NAS",Width=570,Height=430,StartPosition=FormStartPosition.CenterParent,Font=Font};
        var flow=new FlowLayoutPanel(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(16)};
        var letter=new ComboBox(){DropDownStyle=ComboBoxStyle.DropDownList,Width=95};letter.Items.AddRange(Enumerable.Range('D',23).Select(c=>(object)((char)c+":")).ToArray());letter.SelectedItem=old?.Drive??"Z:";
        var path=new TextBox(){Width=490,Text=old?.Unc??@"\\truenas\share"};var login=new TextBox(){Width=300,Text=old?.Login??""};var password=new TextBox(){Width=300,UseSystemPasswordChar=true,PlaceholderText=edit?"Để trống để giữ mật khẩu (cùng máy chủ/tài khoản)":"Mật khẩu NAS"};var show=new CheckBox(){Text="Hiện mật khẩu",AutoSize=true};show.CheckedChanged+=(_,_)=>password.UseSystemPasswordChar=!show.Checked;
        flow.Controls.AddRange(new Control[]{new Label(){Text="User đích: "+string.Join(", ",target.Select(u=>u.Name)),Width=490,Height=40},new Label(){Text="Ký tự ổ",AutoSize=true},letter,new Label(){Text="UNC (\\\\máy-chủ\\share)",AutoSize=true},path,new Label(){Text="Tài khoản NAS",AutoSize=true},login,new Label(){Text="Mật khẩu NAS",AutoSize=true},password,show});
        var save=new Button(){Text=edit?"Lưu thay đổi":"Thêm ánh xạ",DialogResult=DialogResult.OK,AutoSize=true};var buttons=new FlowLayoutPanel(){Dock=DockStyle.Bottom,Height=55,Padding=new Padding(8)};buttons.Controls.Add(save);buttons.Controls.Add(new Button(){Text="Hủy",DialogResult=DialogResult.Cancel});dialog.Controls.Add(flow);dialog.Controls.Add(buttons);
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;var d=Policy.Drive(letter.Text);var unc=Policy.Unc(path.Text.Trim());var user=login.Text.Trim();var secret=password.Text;
        if(!edit&&string.IsNullOrEmpty(secret))throw new ArgumentException("Nhập mật khẩu NAS cho ánh xạ mới.");
        if(!Confirm((edit?"Sửa":"Thêm")+" "+d+" → "+unc+"\r\nUser Windows: "+string.Join(", ",target.Select(u=>u.Name))+"\r\nKhông đổi quyền hoặc xóa dữ liệu trên NAS."))return;
        foreach(var u in target)
        {
            try{if(edit)Nas.Update(u,old!.Drive,d,unc,user,secret);else Nas.Save(u,d,unc,user,secret,false);Storage.Log(edit?"EditNas":"AddNas",u.Sid,true);Append(u.Name+": đã lưu "+d+"; chờ Broker/Agent áp dụng.");}
            catch(Exception e){Storage.Log("SaveNas",u.Sid,false,e.HResult);Append(u.Name+": THẤT BẠI: "+e.Message);}
        }
        password.Clear();RefreshNas();Append(Nas.Health());
    }
    void DeleteNasMapping()
    {
        var row=SelectedNas();var user=WindowsAccounts.List().Single(u=>u.Sid==row.Sid);
        if(!Confirm("Xóa mapping "+row.Drive+" của "+user.Name+"? Không xóa dữ liệu NAS; Agent sẽ thử ngắt mapping, không ép đóng tệp."))return;
        Nas.Remove(user,row.Drive);Storage.Log("RemoveNas",user.Sid,true);RefreshNas();Append("Đã xóa cấu hình; chờ Agent áp dụng.");
    }
    async void TestSelectedNas()
    {
        try
        {
            var row=SelectedNas();var m=Nas.Load().Single(m=>m.Sid==row.Sid&&m.Drive==row.Drive);
            if(!Confirm("Kiểm tra SMB trong phiên Administrator tới "+m.Unc+"? Không chứng minh ổ đã hiện trong phiên user đích."))return;
            var result=await Task.Run(()=>Nas.Connect(new(m.Drive,m.Unc,m.User,Storage.Decrypt(m.ProtectedPassword)),true));
            Append("SMB: "+result+" — "+(result==0?"kết nối thành công trong phiên Administrator":new System.ComponentModel.Win32Exception(result).Message));
        }
        catch(Exception e){Append("LỖI NAS: "+e.Message);MessageBox.Show(this,e.Message,"Không hoàn tất");}
    }
}
