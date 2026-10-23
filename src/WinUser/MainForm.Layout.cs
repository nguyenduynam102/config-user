using System.Diagnostics;
using System.Drawing;
namespace WinUser;
public sealed partial class MainForm
{
    Button Button(string title,Action action,string glyph="\uE713",bool danger=false)
    {
        var b=new Button(){Text=title,AutoSize=true,MinimumSize=new Size(90,36),Padding=new Padding(7,3,7,3),Margin=new Padding(4),FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=danger?Color.Firebrick:Ui.Ink,Image=Ui.Icon(glyph,danger?Color.Firebrick:Ui.Blue),TextImageRelation=TextImageRelation.ImageBeforeText,ImageAlign=ContentAlignment.MiddleLeft};
        b.FlatAppearance.BorderColor=Color.FromArgb(211,220,232);b.Click+=(_,_)=>Safe(action);return b;
    }
    Button TargetButton(string title,Action action,string glyph,bool manage=false,bool desktop=false,bool danger=false)
    {
        var button=Button(title,action,glyph,danger);targetButtons.Add((button,title,manage,desktop));
        tips.SetToolTip(button,manage?"Áp dụng cho user đích được tick. Tài khoản quản trị hiện tại và tài khoản tích hợp được bảo vệ.":"Áp dụng cho user đích được tick; danh sách được xác nhận trước khi thay đổi.");return button;
    }
    static FlowLayoutPanel Bar()=>new(){Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(8),BackColor=Color.White};
    static Label Note(string text)
    {
        var label=new Label(){Text=text,Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(12,7,12,10),ForeColor=Ui.Muted,BackColor=Color.White};
        label.ParentChanged+=(_,_)=>{if(label.Parent is not Control parent)return;void Fit()=>label.MaximumSize=new Size(Math.Max(180,parent.ClientSize.Width-parent.Padding.Horizontal),0);parent.Resize+=(_,_)=>Fit();Fit();};return label;
    }
    TabPage Page(string name,string glyph)
    {
        var image=Ui.Icon(glyph);tabs.ImageList!.Images.Add(image);var page=new TabPage(name){ImageIndex=tabs.ImageList.Images.Count-1,BackColor=Color.White,Padding=new Padding(8)};tabs.TabPages.Add(page);return page;
    }
    static GroupBox Group(string title,params Control[] controls)
    {
        var group=new GroupBox(){Text=title,Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(8),Margin=new Padding(8),ForeColor=Ui.Ink};var flow=new FlowLayoutPanel(){Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Padding=new Padding(3)};flow.Controls.AddRange(controls);group.Controls.Add(flow);return group;
    }
    void BuildLayout()
    {
        Text="WinUser RDP Manager · Preview 3";Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;BackColor=Ui.Surface;ForeColor=Ui.Ink;StartPosition=FormStartPosition.CenterScreen;MinimumSize=new Size(1100,700);
        var screen=Screen.PrimaryScreen!.WorkingArea;Size=new Size(Math.Min(Math.Max(preferences.Width,1100),screen.Width),Math.Min(Math.Max(preferences.Height,700),screen.Height));if(preferences.Maximized)WindowState=FormWindowState.Maximized;
        KeyPreview=true;KeyDown+=(_,e)=>{if(e.KeyCode==Keys.F5){e.Handled=true;RefreshUsers();}if(e.Control&&e.KeyCode==Keys.F){e.Handled=true;search.Focus();search.SelectAll();}};
        var header=new TableLayoutPanel(){Dock=DockStyle.Top,Height=86,Padding=new Padding(15,9,15,9),BackColor=Color.White,ColumnCount=2,RowCount=1};header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var branding=new FlowLayoutPanel(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};branding.Controls.Add(new Label(){Text="WinUser RDP Manager",Font=new Font("Segoe UI",18,FontStyle.Bold),AutoSize=true,ForeColor=Ui.Blue});branding.Controls.Add(new Label(){Text="Quản lý tài khoản và môi trường làm việc Windows · Preview 3",AutoSize=true,ForeColor=Ui.Muted});header.Controls.Add(branding,0,0);
        var headerButtons=new FlowLayoutPanel(){AutoSize=true,Anchor=AnchorStyles.Right};headerButtons.Controls.AddRange(new Control[]{Button("Làm mới · F5",RefreshUsers,"\uE72C"),Button("Trợ giúp",About,"\uE946")});header.Controls.Add(headerButtons,1,0);
        var targetStrip=new TableLayoutPanel(){Dock=DockStyle.Top,Height=52,Padding=new Padding(12,6,12,6),BackColor=Color.FromArgb(230,240,255),ColumnCount=2};targetStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));targetStrip.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));targetStrip.Controls.Add(selectionLabel,0,0);targetStrip.Controls.Add(Button("Bỏ toàn bộ",()=>{targets.Clear();SyncChecks();},"\uE894"),1,0);
        split=new SplitContainer(){Dock=DockStyle.Fill,Size=new Size(1300,650),SplitterWidth=7,SplitterDistance=Math.Clamp(preferences.Split,380,600),Panel1MinSize=360,Panel2MinSize=560};
        workspace.Controls.Add(split);workspace.Controls.Add(targetStrip);BuildUserList();
        tabs.ImageList=new ImageList(){ImageSize=new Size(20,20),ColorDepth=ColorDepth.Depth32Bit};split.Panel2.Controls.Add(tabs);
        BuildAccountPage();BuildDesktopPage();BuildRegistryPage();BuildFolderPage();BuildPresetPage();BuildNasPage();BuildBackupPage();
        results.Dock=DockStyle.Fill;results.ReadOnly=true;results.AllowUserToAddRows=false;results.RowHeadersVisible=false;results.BackgroundColor=Color.White;results.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        foreach(var name in new[]{"Thời gian","User","Thao tác","Kết quả","Chi tiết"})results.Columns.Add(name,name);
        results.Columns[0].FillWeight=55;results.Columns[1].FillWeight=65;results.Columns[2].FillWeight=105;results.Columns[3].FillWeight=65;results.Columns[4].FillWeight=240;
        var logTabs=new TabControl(){Dock=DockStyle.Fill};var resultTab=new TabPage("Kết quả từng user");resultTab.Controls.Add(results);var logTab=new TabPage("Nhật ký phiên này");logTab.Controls.Add(output);logTabs.TabPages.AddRange(new[]{resultTab,logTab});logPanel.Controls.Add(logTabs);
        var strip=new StatusStrip(){BackColor=Color.White};strip.Items.AddRange(new ToolStripItem[]{status,progress,stop,showLog});stop.Click+=(_,_)=>{stopRequested=true;stop.Enabled=false;status.Text="Sẽ dừng sau user đang xử lý; không hoàn tác thao tác đã chạy.";};showLog.CheckedChanged+=(_,_)=>logPanel.Visible=showLog.Checked;
        Controls.Add(workspace);Controls.Add(logPanel);Controls.Add(header);Controls.Add(strip);
        source.TextChanged+=(_,_)=>SourceChanged();source.SelectedIndexChanged+=(_,_)=>SourceChanged();
        regFile.ReadOnly=true;privateRoot.Text=preferences.PrivateRoot;drive.Text="Z:";unc.Text=@"\\truenas\share";nasPassword.UseSystemPasswordChar=true;
        UpdateTargets();
    }
    void BuildUserList()
    {
        var left=new Panel(){Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(6)};split.Panel1.Controls.Add(left);
        users.Dock=DockStyle.Fill;users.AutoGenerateColumns=false;users.AllowUserToAddRows=false;users.AllowUserToDeleteRows=false;users.AllowUserToResizeRows=false;users.RowHeadersVisible=false;users.MultiSelect=true;users.SelectionMode=DataGridViewSelectionMode.FullRowSelect;users.BackgroundColor=Color.White;users.BorderStyle=BorderStyle.None;users.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;users.GridColor=Color.FromArgb(233,238,245);users.EnableHeadersVisualStyles=false;users.ColumnHeadersDefaultCellStyle.BackColor=Ui.Surface;users.ColumnHeadersDefaultCellStyle.ForeColor=Ui.Ink;users.ColumnHeadersHeight=38;users.RowTemplate.Height=34;users.DefaultCellStyle.SelectionBackColor=Color.FromArgb(218,233,252);users.DefaultCellStyle.SelectionForeColor=Ui.Ink;users.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(249,251,254);
        users.Columns.Add(new DataGridViewCheckBoxColumn(){Name="Chosen",DataPropertyName="Chosen",HeaderText="✓",Width=35,Frozen=true});
        foreach(var (name,title,width) in new[]{("Name","Tài khoản",115),("Account","Trạng thái",110),("Rdp","Cấu hình RDP",120),("Session","Phiên",110)})users.Columns.Add(new DataGridViewTextBoxColumn(){Name=name,DataPropertyName=name,HeaderText=title,Width=preferences.Columns.TryGetValue(name,out var saved)?Math.Clamp(saved,70,350):width,ReadOnly=true,SortMode=DataGridViewColumnSortMode.Programmatic});
        users.CurrentCellDirtyStateChanged+=(_,_)=>{if(users.IsCurrentCellDirty)users.CommitEdit(DataGridViewDataErrorContexts.Commit);};
        users.CellValueChanged+=(_,e)=>{if(binding||e.RowIndex<0||e.ColumnIndex!=0)return;var row=users.Rows[e.RowIndex];targets.Set(((UserView)row.DataBoundItem).User.Sid,row.Cells[0].Value is true);UpdateTargets();};
        users.CellMouseDown+=(_,e)=>{if(e.Button==MouseButtons.Right&&e.RowIndex>=0&&!users.Rows[e.RowIndex].Selected){users.ClearSelection();users.CurrentCell=users.Rows[e.RowIndex].Cells["Name"];users.Rows[e.RowIndex].Selected=true;}};
        users.SelectionChanged+=(_,_)=>{if(!binding)ShowDetails();};
        users.CellFormatting+=(_,e)=>{if(e.RowIndex<0)return;var row=(UserView)users.Rows[e.RowIndex].DataBoundItem;if(e.ColumnIndex==users.Columns["Account"].Index)e.CellStyle!.ForeColor=!row.User.Enabled?Color.Firebrick:row.User.LockedOut?Color.DarkOrange:Color.DarkGreen;};
        users.ColumnHeaderMouseClick+=(_,e)=>{var col=users.Columns[e.ColumnIndex];if(col.Name=="Chosen")return;sortDescending=sortColumn==col.Name&&!sortDescending;sortColumn=col.Name;ApplyFilter();};
        users.KeyDown+=(_,e)=>{if(e.KeyCode is Keys.Space or Keys.Enter){e.SuppressKeyPress=true;MarkRows(true,true);}};
        var context=new ContextMenuStrip();context.Items.Add("Chọn dòng bôi đen làm user đích",null,(_,_)=>MarkRows(true,true));context.Items.Add("Bỏ các dòng bôi đen khỏi user đích",null,(_,_)=>MarkRows(false,true));context.Items.Add("Dùng user đang xem làm nguồn Desktop",null,(_,_)=>Safe(()=>{if(users.CurrentRow?.DataBoundItem is UserView r){source.SelectedItem=source.Items.Cast<LocalUser>().First(u=>u.Sid==r.User.Sid);tabs.SelectedIndex=1;}}));users.ContextMenuStrip=context;
        var tools=Bar();search.Width=210;filter.Items.AddRange(new object[]{"Tất cả tài khoản","Đang bật","Đã vô hiệu hóa","Khóa do nhập sai","Có phiên RDP"});filter.SelectedIndex=Math.Clamp(preferences.Filter,0,4);tools.Controls.AddRange(new Control[]{search,filter});search.TextChanged+=(_,_)=>ApplyFilter();filter.SelectedIndexChanged+=(_,_)=>ApplyFilter();
        var select=Bar();select.Controls.AddRange(new Control[]{Button("Chọn dòng bôi đen",()=>MarkRows(true,true),"\uE762"),Button("Chọn kết quả lọc",()=>MarkRows(true,false),"\uE73A"),Button("Bỏ dòng bôi đen",()=>MarkRows(false,true),"\uE894")});
        var hint=Note("Click tên: xem chi tiết · Tick ô: chọn user đích\nCtrl/Shift + click: bôi đen nhiều dòng; Enter/Space: chọn các dòng đó.");
        left.Controls.Add(users);left.Controls.Add(hint);left.Controls.Add(select);left.Controls.Add(tools);
    }
    void BuildAccountPage()
    {
        var page=Page("User / RDP","\uE716");page.AutoScroll=true;page.AutoScrollMinSize=new Size(0,610);var details=new GroupBox(){Text="Đang xem — không tự chọn làm user đích",Dock=DockStyle.Fill,Padding=new Padding(15)};var titlePanel=new Panel(){Dock=DockStyle.Top,Height=40};titlePanel.Controls.Add(detailTitle);details.Controls.Add(detailText);details.Controls.Add(titlePanel);page.Controls.Add(details);
        page.Controls.Add(Note("Khóa tài khoản = vô hiệu hóa đăng nhập và ngắt RDP, giữ ứng dụng đang chạy. Ngắt phiên riêng chỉ ngắt kết nối, không đổi tài khoản/quyền RDP. Không đăng xuất user."));
        page.Controls.Add(Group("Phiên làm việc",TargetButton("Ngắt phiên RDP",()=>Batch("Ngắt kết nối phiên RDP (không đăng xuất)",u=>{var n=WindowsAccounts.Disconnect(u);return n==0?"Không có phiên RDP cần ngắt; trạng thái tài khoản không đổi.":$"Đã ngắt {n} phiên RDP; giữ chương trình đang chạy.";},true),"\uE8CD",true),Button("Kiểm tra RDP",CheckRdp,"\uE9D9")));
        page.Controls.Add(Group("Quyền đăng nhập RDP",TargetButton("Cho phép RDP",()=>Batch("Cho phép RDP: thêm nhóm và gỡ deny trực tiếp của SID",u=>{WindowsAccounts.Rdp(u,true);return "Đã lưu cấu hình; GPO/deny từ nhóm vẫn có thể chặn đăng nhập.";},true),"\uE8A7",true),TargetButton("Chặn RDP",()=>Batch("Chặn RDP: gán deny trực tiếp cho SID, gỡ nhóm; không vô hiệu hóa tài khoản",u=>{WindowsAccounts.Rdp(u,false);return "Đã chặn RDP trực tiếp; trạng thái tài khoản không đổi.";},true),"\uE711",true,danger:true)));
        page.Controls.Add(Group("Tài khoản Windows",Button("Tạo tài khoản",CreateUser,"\uE710"),TargetButton("Đổi mật khẩu",ChangePassword,"\uE192",true),TargetButton("Khóa tài khoản",()=>Batch("Khóa tài khoản và ngắt RDP, không đăng xuất",u=>{WindowsAccounts.Enabled(u,false);return "Đã vô hiệu hóa tài khoản và ngắt kết nối RDP; không logoff.";},true),"\uE72E",true,danger:true),TargetButton("Bật tài khoản",()=>Batch("Bật tài khoản Windows",u=>{WindowsAccounts.Enabled(u,true);return "Đã bật tài khoản; không đổi cấu hình RDP.";},true),"\uE785",true),TargetButton("Mở khóa nhập sai",()=>Batch("Mở khóa do nhập sai mật khẩu; không tự bật tài khoản bị vô hiệu hóa",u=>{WindowsAccounts.Unlock(u);return "Đã xóa trạng thái lockout; giữ nguyên cờ vô hiệu hóa và quyền RDP.";},true),"\uE73E",true)));
    }
    void BuildDesktopPage()
    {
        var page=Page("Desktop","\uE7F4");page.Controls.Add(shortcuts);page.Controls.Add(Note("Chỉ .lnk/.url; giữ shortcut trùng và Public Desktop. User nguồn tự loại khỏi đích khi áp dụng. Lưới biểu tượng chưa thể hiện tọa độ Desktop thực tế."));
        var commands=Bar();commands.Controls.AddRange(new Control[]{Button("Chọn tất cả shortcut",()=>{foreach(ListViewItem i in shortcuts.Items)i.Checked=true;},"\uE73A"),Button("Bỏ chọn shortcut",()=>{foreach(ListViewItem i in shortcuts.Items)i.Checked=false;},"\uE894"),Button("Xem trước",PreviewDesktop,"\uE890"),TargetButton("Thêm shortcut",()=>Sync(false),"\uE710",desktop:true),TargetButton("Xóa shortcut",()=>Sync(true),"\uE74D",desktop:true,danger:true)});page.Controls.Add(commands);
        var sourceInfo=Bar();sourceLabel.MaximumSize=new Size(680,0);sourceLabel.Text="Chưa chọn user nguồn";sourceInfo.Controls.Add(sourceLabel);page.Controls.Add(sourceInfo);
        page.Controls.Add(Group("1. Nguồn Desktop — một user",Ui.Caption("Tìm / chọn user nguồn"),source,Button("Lấy user đang xem",()=>{if(users.CurrentRow?.DataBoundItem is not UserView row)throw new ArgumentException("Click tên user trong danh sách bên trái trước.");source.SelectedItem=source.Items.Cast<LocalUser>().First(u=>u.Sid==row.User.Sid);},"\uE77B"),Button("Đọc Desktop nguồn",ReadShortcuts,"\uE8B7")));
        tips.SetToolTip(source,"Gõ tên để tìm hoặc mở danh sách xổ xuống. Danh sách user đích nằm bên trái.");
    }
    void BuildRegistryPage()
    {
        var page=Page("Registry","\uE713");page.Controls.Add(Note("Áp dụng đúng hive SID của user đích; tự sao lưu trước ghi. Chỉ HKCU\\Software, REG_SZ/DWORD; từ chối khóa nhạy cảm, Registry toàn máy và thao tác xóa. Có thể chuẩn bị profile mới khi Windows cho phép."));
        page.Controls.Add(Group("File cấu hình Registry",regFile,Button("Chọn .reg",()=>{using var f=new OpenFileDialog(){Filter="Registry|*.reg"};if(f.ShowDialog(this)==DialogResult.OK){Policy.ParseRegistry(File.ReadAllText(f.FileName));regFile.Text=f.FileName;}},"\uE8B7"),Button("Xem trước",()=>MessageBox.Show(this,RegistryPreview(),"Registry sẽ áp dụng"),"\uE890"),TargetButton("Áp dụng Registry",ApplyRegistry,"\uE73E")));
    }
    void BuildFolderPage()
    {
        var page=Page("Thư mục riêng","\uE8B7");page.Controls.Add(Note("Tạo thư mục mới theo user trên NTFS cục bộ. User là owner; chỉ user, Administrators và SYSTEM Full Control. Thư mục đã tồn tại sẽ báo lỗi, không tự sửa ACL hoặc xóa dữ liệu. ACL không bảo đảm ẩn tên thư mục khi duyệt ổ local."));
        page.Controls.Add(Group("Chọn gốc — mỗi user một thư mục con",privateRoot,Button("Duyệt thư mục",()=>{using var f=new FolderBrowserDialog();if(f.ShowDialog(this)==DialogResult.OK)privateRoot.Text=f.SelectedPath;},"\uE8B7"),TargetButton("Xem trước thư mục",()=>MessageBox.Show(this,string.Join(Environment.NewLine,Selected().Select(u=>Path.Combine(privateRoot.Text,u.Name))),"Thư mục sẽ tạo"),"\uE890"),TargetButton("Tạo và phân quyền",ApplyFolders,"\uE72E")));
    }
    void BuildPresetPage()
    {
        var page=Page("Bộ cấu hình","\uE713");page.Controls.Add(Note("Tạo bộ từ các lựa chọn hiện tại: RDP, shortcut nguồn, file .reg, thư mục riêng và NAS của nguồn. Chỉnh sửa bộ không tự áp dụng lên user. Áp dụng nhiều bước có thể thành công một phần; xem kết quả từng user."));
        page.Controls.Add(Group("Áp dụng",Button("Xem nội dung",()=>MessageBox.Show(this,PresetPreview(Preset()),"Nội dung bộ cấu hình"),"\uE890"),TargetButton("Áp dụng bộ cấu hình",()=>{var p=Preset();Batch(PresetPreview(p),u=>{Presets.Apply(p,u);return "Đã áp dụng bộ ‘"+p.Name+"’; NAS chờ Agent.";},true);},"\uE73E",true)));
        page.Controls.Add(Group("Quản lý bộ",presetList,Button("Chỉnh sửa",()=>{using var editor=new PresetEditor(Preset());editor.ShowDialog(this);RefreshPresets();},"\uE70F"),Button("Sao chép",()=>{var p=Preset();var n=Prompt("Tên bản sao",p.Name+" - bản sao");if(n!=null)Presets.Clone(p,n);RefreshPresets();},"\uE8C8"),Button("Xóa",()=>{var p=Preset();if(Confirm("Xóa bộ ‘"+p.Name+"’? Giữ bản sao để khôi phục."))Presets.Delete(p);RefreshPresets();},"\uE74D",true)));
        page.Controls.Add(Group("Tạo bộ từ các lựa chọn",presetRdp,Button("Lưu bộ mới",CreatePreset,"\uE74E")));
    }
    void BuildNasPage()
    {
        var page=Page("NAS","\uE968");page.Controls.Add(nasInfo);page.Controls.Add(Note("Broker/Agent phải được cài bằng Install.cmd. Mapping chạy trong phiên user, thử lại mỗi 30 giây; Windows có thể trì hoãn khởi động Agent. NAS account dùng chung không bảo đảm giấu bí mật khỏi chính user được cấp. Không dùng tài khoản quản trị TrueNAS."));
        page.Controls.Add(Group("Thao tác",TargetButton("Lưu / cập nhật NAS",SaveNas,"\uE74E"),TargetButton("Xóa ánh xạ",RemoveNas,"\uE74D",danger:true),Button("Kiểm tra bằng tài khoản nhập",TestNas,"\uE9D9"),Button("Làm mới cấu hình",RefreshUsers,"\uE72C")));
        var show=new CheckBox(){Text="Hiện mật khẩu",AutoSize=true};show.CheckedChanged+=(_,_)=>nasPassword.UseSystemPasswordChar=!show.Checked;
        page.Controls.Add(Group("Ánh xạ cho user đích",Ui.Caption("Ổ"),drive,Ui.Caption("UNC"),unc,Ui.Caption("NAS user"),nasUser,Ui.Caption("Mật khẩu"),nasPassword,show));
    }
    void BuildBackupPage()
    {
        var page=Page("Sao lưu","\uE777");page.Controls.Add(Note("Bản sao chỉ Administrators/SYSTEM truy cập. Khôi phục Desktop, Registry, NAS hoặc bộ cấu hình trong phạm vi đã lưu. Không khôi phục mật khẩu Windows hoặc dữ liệu NAS. Nhật ký không chứa mật khẩu; xem kết quả từng user ở thanh dưới."));
        page.Controls.Add(Group("Sao lưu và nhật ký",Button("Mở bản sao lưu",()=>Process.Start(new ProcessStartInfo(Path.Combine(Storage.Root,"Backups")){UseShellExecute=true}),"\uE8B7"),Button("Khôi phục",Restore,"\uE777"),Button("Xem nhật ký quản trị",()=>{var path=Path.Combine(Storage.Root,"audit.jsonl");if(File.Exists(path))Process.Start(new ProcessStartInfo("notepad.exe"){UseShellExecute=true,ArgumentList={path}});else Append("Chưa có nhật ký quản trị.");},"\uE8A5")));
    }
    void SourceChanged(){if(binding)return;shortcuts.Items.Clear();shortcutSourceSid=null;sourceLabel.Text="Nguồn: "+(string.IsNullOrWhiteSpace(source.Text)?"chưa chọn":source.Text)+" · bấm Đọc Desktop nguồn";UpdateTargets();}
    async void CheckRdp()
    {
        await ExecuteAsync(async()=>{var text=await Task.Run(RdpDiagnostics.Read);using var dialog=new Form(){Text="Kiểm tra RDP — chỉ đọc",Font=Font,ClientSize=new Size(700,470),StartPosition=FormStartPosition.CenterParent};dialog.Controls.Add(new TextBox(){Text=text,Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Color.White});dialog.ShowDialog(this);});
    }
    void About()
    {
        using var about=new Form(){Text="WinUser · Trợ giúp",Font=Font,ClientSize=new Size(620,360),StartPosition=FormStartPosition.CenterParent};var text=new Label(){Text="WinUser RDP Manager · Preview 3\r\n\r\n1. Click tên user để xem chi tiết.\r\n2. Tick user đích, hoặc Ctrl/Shift + click rồi Enter để chọn nhiều.\r\n3. Desktop: chọn một nguồn ở danh sách xổ xuống; nguồn tự loại khỏi đích.\r\n4. Xem trước, xác nhận và theo dõi kết quả từng user.\r\n\r\nKhóa tài khoản sẽ ngắt RDP nhưng không đăng xuất. Ngắt phiên riêng không khóa tài khoản. Không vá giới hạn phiên Windows.\r\nBản thử nghiệm: cần kiểm chứng API Windows/RDP/NAS trên máy thật.",Dock=DockStyle.Fill,Padding=new Padding(18)};var bar=Bar();bar.Dock=DockStyle.Bottom;bar.Controls.Add(Button("Mở trang cập nhật chính thức",()=>Process.Start(new ProcessStartInfo("https://github.com/nguyenduynam102/config-user/releases"){UseShellExecute=true}),"\uE774"));about.Controls.Add(text);about.Controls.Add(bar);about.ShowDialog(this);
    }
}
