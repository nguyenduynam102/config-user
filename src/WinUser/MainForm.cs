using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using Microsoft.Win32;
using System.Text.Json;
namespace WinUser;
public sealed partial class MainForm:Form
{
    readonly TargetSelection targets=new();readonly UiPreferences preferences=UiPreferences.Load();readonly ToolTip tips=new(){AutoPopDelay=15000};
    readonly DataGridView users=new(),results=new();readonly TextBox output=new(){Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,Font=new Font("Consolas",9)};
    readonly TextBox search=Ui.Text(240,"Tìm tên tài khoản…");readonly ComboBox filter=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=175};
    readonly Label selectionLabel=new(){AutoEllipsis=true,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},detailTitle=new(){AutoSize=true,Font=new Font("Segoe UI",15,FontStyle.Bold)};
    readonly TextBox detailText=new(){Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=Color.White,ScrollBars=ScrollBars.Vertical};
    readonly ComboBox source=new(){Width=240,DropDownStyle=ComboBoxStyle.DropDown,DisplayMember="Name",AutoCompleteMode=AutoCompleteMode.SuggestAppend,AutoCompleteSource=AutoCompleteSource.ListItems};
    readonly Label sourceLabel=new(){AutoSize=true,ForeColor=Ui.Muted};
    readonly ListView shortcuts=new(){Dock=DockStyle.Fill,View=View.LargeIcon,CheckBoxes=true,MultiSelect=true,LargeImageList=new ImageList(){ImageSize=new Size(32,32),ColorDepth=ColorDepth.Depth32Bit}};
    readonly TextBox regFile=Ui.Text(340,"Chọn file .reg"),privateRoot=Ui.Text(330,"Chọn thư mục gốc trên NTFS"),drive=Ui.Text(55),unc=Ui.Text(260),nasUser=Ui.Text(170,"Tài khoản NAS"),nasPassword=Ui.Text(170,"Mật khẩu NAS");
    readonly ComboBox presetList=new(){Width=260,DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="Name"};readonly CheckBox presetRdp=new(){Text="Cho phép RDP trong bộ cấu hình",AutoSize=true};
    readonly TextBox nasInfo=new(){Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,BorderStyle=BorderStyle.None,BackColor=Color.White};
    readonly TabControl tabs=new(){Dock=DockStyle.Fill,Multiline=true,Padding=new Point(12,9)};
    readonly Panel workspace=new(){Dock=DockStyle.Fill},logPanel=new(){Dock=DockStyle.Bottom,Height=190,Visible=false};
    readonly ToolStripStatusLabel status=new(){Text="Sẵn sàng",Spring=true,TextAlign=ContentAlignment.MiddleLeft};readonly ToolStripProgressBar progress=new(){Minimum=0,Maximum=100,Width=180};
    readonly ToolStripButton stop=new(){Text="Dừng các user tiếp theo",Enabled=false},showLog=new(){Text="Kết quả / Nhật ký",CheckOnClick=true};
    readonly List<(Button Button,string Title,bool Manage,bool Desktop)> targetButtons=new();SplitContainer split=null!;
    List<UserView> allRows=new();List<NasMapping> nasMappings=new();bool binding,busy,stopRequested,sessionsKnown;string? inspectedSid,shortcutSourceSid;string sortColumn="Name";bool sortDescending;
    sealed class UserView
    {
        public required LocalUser User {get;init;}public bool Chosen{get;set;}public string Name=>User.Name;
        public string Account=>User.AccountStatus;public string Rdp=>User.RdpStatus;public string Session{get;init;}="Không có phiên";
        public bool HasRdp{get;init;}public string Profile{get;init;}="Chưa có profile";public string Nas{get;init;}="Chưa cấu hình";public string NasAgent{get;init;}="Chưa có báo cáo Agent";
    }
    sealed record Snapshot(List<UserView> Users,List<NasMapping> Nas,bool SessionsKnown,string[] Warnings);
    public MainForm()
    {
        BuildLayout();FormClosing+=OnClosingForm;Shown+=async(_,_)=>await ExecuteAsync(async()=>{await ReloadAsync();RefreshPresets();});
    }
    static int ErrorCode(Exception e)=>e is Win32Exception w?w.NativeErrorCode:e.HResult;
    void Append(string text)
    {
        if(IsDisposed)return;if(InvokeRequired){BeginInvoke(()=>Append(text));return;}
        output.AppendText(DateTime.Now.ToString("HH:mm:ss")+" "+text+Environment.NewLine);
        if(output.TextLength>180000)output.Text=output.Text[^120000..];
        if(text.StartsWith("LỖI")||text.Contains("THẤT BẠI")){showLog.Checked=true;logPanel.Visible=true;}
    }
    void Safe(Action action){try{action();}catch(Exception e){Report(e);}}
    void Report(Exception e){Append("LỖI: "+e.Message);MessageBox.Show(this,e.Message,"Không hoàn tất",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    async Task ExecuteAsync(Func<Task> work)
    {
        if(busy)return;busy=true;workspace.Enabled=false;UseWaitCursor=true;status.Text="Đang xử lý…";UpdateTargets();
        try{await work();}
        catch(Exception e){Report(e);status.Text="Không hoàn tất — xem nhật ký";}
        finally{busy=false;workspace.Enabled=true;UseWaitCursor=false;stop.Enabled=false;progress.Style=ProgressBarStyle.Blocks;UpdateTargets();if(status.Text=="Đang xử lý…")status.Text="Sẵn sàng";}
    }
    static Snapshot LoadSnapshot()
    {
        var accounts=WindowsAccounts.List();var warnings=new List<string>();var sessions=new List<LocalSession>();bool known=true;
        try{sessions=WindowsAccounts.Sessions();}catch(Exception e){known=false;warnings.Add("Không đọc được phiên làm việc: "+e.Message);}
        List<NasMapping> mappings;
        try{mappings=Nas.Load();}catch(Exception e){mappings=new();warnings.Add("Không đọc được cấu hình NAS: "+e.Message);}
        var rows=new List<UserView>();
        foreach(var u in accounts)
        {
            var local=sessions.Where(s=>s.User.Equals(u.Name,StringComparison.OrdinalIgnoreCase)&&s.Domain.Equals(Environment.MachineName,StringComparison.OrdinalIgnoreCase)).ToList();
            var rdp=local.Where(s=>s.Protocol==2).ToList();
            var state=!known?"Chưa xác định":rdp.Any(s=>s.State is 0 or 1)?"RDP · đang kết nối":rdp.Any(s=>s.State==4)?"RDP · đã ngắt":rdp.Count>0?"RDP · trạng thái khác":local.Any(s=>s.Protocol==0)?"Console":"Không có phiên";
            string profile="Chưa có profile";
            try{using var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\"+u.Sid);if(key?.GetValue("ProfileImagePath") is string p)profile=Environment.ExpandEnvironmentVariables(p);}catch{profile="Không đọc được profile";}
            var userMappings=mappings.Where(m=>m.Sid==u.Sid).Select(m=>m.Drive+" → "+m.Unc).ToArray();
            rows.Add(new(){User=u,Session=state,HasRdp=rdp.Count>0,Profile=profile,NasAgent=ReadAgentReport(profile),Nas=userMappings.Length==0?"Chưa cấu hình":string.Join(Environment.NewLine,userMappings)});
        }
        return new(rows,mappings,known,warnings.ToArray());
    }
    async Task ReloadAsync()
    {
        status.Text="Đang đọc tài khoản và phiên Windows…";progress.Style=ProgressBarStyle.Marquee;
        var snap=await Task.Run(LoadSnapshot);allRows=snap.Users;nasMappings=snap.Nas;sessionsKnown=snap.SessionsKnown;
        targets.Retain(allRows.Select(r=>r.User.Sid));
        var sourceSid=(source.SelectedItem as LocalUser)?.Sid;
        binding=true;
        try{source.DataSource=allRows.Select(r=>r.User).ToList();source.SelectedIndex=-1;source.Text="";if(sourceSid!=null)source.SelectedItem=allRows.Select(r=>r.User).FirstOrDefault(u=>u.Sid==sourceSid);}
        finally{binding=false;}
        if(source.SelectedItem is not LocalUser)SourceChanged();
        ApplyFilter();ShowNas();foreach(var warning in snap.Warnings)Append("CẢNH BÁO: "+warning);
        status.Text=$"Đã tải {allRows.Count} tài khoản · {DateTime.Now:HH:mm:ss}";progress.Style=ProgressBarStyle.Blocks;progress.Value=0;
    }
    void RefreshUsers()=>_=ExecuteAsync(ReloadAsync);
    void ApplyFilter()
    {
        if(binding)return;binding=true;
        try
        {
            var query=allRows.Where(r=>r.Name.Contains(search.Text.Trim(),StringComparison.CurrentCultureIgnoreCase));
            query=filter.SelectedIndex switch{1=>query.Where(r=>r.User.Enabled&&!r.User.LockedOut),2=>query.Where(r=>!r.User.Enabled),3=>query.Where(r=>r.User.LockedOut),4=>query.Where(r=>r.HasRdp),_=>query};
            string Sort(UserView r)=>sortColumn switch{"Account"=>r.Account,"Rdp"=>r.Rdp,"Session"=>r.Session,_=>r.Name};
            var list=(sortDescending?query.OrderByDescending(Sort,StringComparer.CurrentCultureIgnoreCase):query.OrderBy(Sort,StringComparer.CurrentCultureIgnoreCase)).ToList();
            foreach(var row in list)row.Chosen=targets.Contains(row.User.Sid);
            users.DataSource=list;users.ClearSelection();users.CurrentCell=null;
            foreach(DataGridViewColumn col in users.Columns)if(col.Name!="Chosen")col.HeaderCell.SortGlyphDirection=col.Name==sortColumn?(sortDescending?SortOrder.Descending:SortOrder.Ascending):SortOrder.None;
            if(inspectedSid!=null)foreach(DataGridViewRow row in users.Rows)if(((UserView)row.DataBoundItem).User.Sid==inspectedSid){row.Selected=true;users.CurrentCell=row.Cells["Name"];break;}
        }
        finally{binding=false;}
        ShowDetails();UpdateTargets();
    }
    List<LocalUser> Selected()
    {
        users.EndEdit();var list=targets.Resolve(allRows,r=>r.User.Sid).Select(r=>r.User).ToList();
        if(list.Count==0)throw new ArgumentException("Chọn user đích bằng checkbox, hoặc bôi đen các dòng rồi bấm ‘Chọn dòng bôi đen’.");
        return list;
    }
    void MarkRows(bool value,bool highlightedOnly)
    {
        var rows=(highlightedOnly?users.SelectedRows.Cast<DataGridViewRow>():users.Rows.Cast<DataGridViewRow>()).ToArray();
        foreach(var row in rows)targets.Set(((UserView)row.DataBoundItem).User.Sid,value);
        SyncChecks();
    }
    void SyncChecks()
    {
        binding=true;try{foreach(DataGridViewRow row in users.Rows){var view=(UserView)row.DataBoundItem;view.Chosen=targets.Contains(view.User.Sid);row.Cells["Chosen"].Value=view.Chosen;}}finally{binding=false;}UpdateTargets();
    }
    void UpdateTargets()
    {
        var chosen=targets.Resolve(allRows,r=>r.User.Sid);var visible=users.Rows.Cast<DataGridViewRow>().Count(r=>targets.Contains(((UserView)r.DataBoundItem).User.Sid));
        var names=string.Join(", ",chosen.Select(r=>r.Name));selectionLabel.Text=chosen.Count==0?"Chưa chọn user đích · Tick checkbox hoặc chọn các dòng bôi đen":$"{chosen.Count} user đích"+(chosen.Count>visible?$" · {chosen.Count-visible} đang ẩn do bộ lọc":"")+"  —  "+names;
        tips.SetToolTip(selectionLabel,names);
        foreach(var item in targetButtons)
        {
            var candidates=chosen.Where(r=>!item.Manage||AccountRules.ProtectedReason(r.User.Sid,WindowsAccounts.CurrentSid)==null);
            if(item.Desktop)candidates=candidates.Where(r=>r.User.Sid!=CurrentSourceSid());
            var count=candidates.Count();item.Button.Text=item.Title+" ("+count+")";item.Button.Enabled=!busy&&count>0;
        }
    }
    void ShowDetails()
    {
        var row=users.CurrentRow?.DataBoundItem as UserView;
        if(row==null){detailTitle.Text="Chi tiết tài khoản";detailText.Text="Click tên một user để xem thông tin.\r\nUser được tick mới là đối tượng của các thao tác.";return;}
        inspectedSid=row.User.Sid;var protectedReason=AccountRules.ProtectedReason(row.User.Sid,WindowsAccounts.CurrentSid);
        detailTitle.Text=row.Name+(protectedReason!=null?" · được bảo vệ":"");
        detailText.Text="Tài khoản: "+row.Account+"\r\nCấu hình RDP: "+row.Rdp+"\r\nPhiên: "+row.Session+"\r\nSID: "+row.User.Sid+"\r\nProfile: "+row.Profile+"\r\nNAS: "+row.Nas+"\r\nAgent NAS: "+row.NasAgent+(protectedReason==null?"":"\r\nBảo vệ: "+protectedReason)+"\r\n\r\nTrạng thái nhóm/deny trực tiếp chưa phản ánh đầy đủ chính sách RDP hiệu lực.";
    }
    bool Confirm(string text)
    {
        using var dialog=new Form(){Text="Xem trước và xác nhận",Font=Font,AutoScaleMode=AutoScaleMode.Dpi,ClientSize=new Size(650,440),StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false};
        var content=new TextBox(){Text=text,Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Color.White,BorderStyle=BorderStyle.None};
        var bar=new FlowLayoutPanel(){Dock=DockStyle.Bottom,Height=55,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(8)};
        var cancel=new Button(){Text="Hủy",DialogResult=DialogResult.Cancel,AutoSize=true};var ok=new Button(){Text="Xác nhận thực hiện",DialogResult=DialogResult.OK,AutoSize=true};bar.Controls.AddRange(new Control[]{cancel,ok});dialog.Controls.Add(content);dialog.Controls.Add(bar);dialog.AcceptButton=cancel;dialog.CancelButton=cancel;
        return dialog.ShowDialog(this)==DialogResult.OK;
    }
    async void Batch(string action,Func<LocalUser,string> apply,bool manage=false,string? excludedSid=null,Action? cleanup=null)
    {
        try
        {
        await ExecuteAsync(async()=>
        {
            var chosen=Selected();var skipped=new List<(LocalUser User,string Reason)>();var pending=new List<LocalUser>();
            foreach(var user in chosen)
            {
                var reason=manage?AccountRules.ProtectedReason(user.Sid,WindowsAccounts.CurrentSid):null;
                if(user.Sid==excludedSid)reason="Trùng user nguồn Desktop";
                if(reason!=null)skipped.Add((user,reason));else pending.Add(user);
            }
            if(pending.Count==0)throw new InvalidOperationException("Không có user đích hợp lệ. "+string.Join("; ",skipped.Select(s=>s.User.Name+": "+s.Reason)));
            var preview=action+"\r\n\r\nSẼ THỰC HIỆN CHO "+pending.Count+" USER:\r\n"+string.Join("\r\n",pending.Select(u=>"• "+u.Name))+ (skipped.Count==0?"":"\r\n\r\nBỎ QUA:\r\n"+string.Join("\r\n",skipped.Select(s=>s.User.Name+": "+s.Reason)))+"\r\n\r\nThao tác nhiều bước có thể thành công một phần. Dừng chỉ bỏ qua các user chưa xử lý.";
            if(!Confirm(preview)){status.Text="Đã hủy — chưa thực hiện thay đổi";return;}
            foreach(var skip in skipped)AddResult(skip.User.Name,action,"Bỏ qua",skip.Reason);
            stopRequested=false;stop.Enabled=true;progress.Style=ProgressBarStyle.Blocks;progress.Maximum=pending.Count;progress.Value=0;int passed=0,failed=0,done=0;
            foreach(var user in pending)
            {
                if(stopRequested)break;status.Text=$"{action.Split('\n')[0]} · {done+1}/{pending.Count}: {user.Name}";
                try
                {
                    var message=await Task.Run(()=>apply(user));Storage.Log(ActionId(action),user.Sid,true);AddResult(user.Name,action,"Thành công",message);passed++;
                }
                catch(Exception e){failed++;Storage.Log(ActionId(action),user.Sid,false,ErrorCode(e));AddResult(user.Name,action,"Thất bại",e.Message+" · Có thể đã thay đổi một phần.");}
                done++;progress.Value=done;
            }
            if(stopRequested)foreach(var user in pending.Skip(done))AddResult(user.Name,action,"Chưa chạy","Đã dừng trước khi xử lý user này.");
            stop.Enabled=false;await ReloadAsync();status.Text=$"Hoàn tất: {passed} thành công · {failed} lỗi · {pending.Count-done} chưa chạy · {skipped.Count} bỏ qua";showLog.Checked=true;
        });
        }
        finally{cleanup?.Invoke();}
    }
    static string ActionId(string action)=>action.Split('\r','\n',':')[0];
    void AddResult(string user,string action,string state,string detail)
    {
        var i=results.Rows.Add(DateTime.Now.ToString("HH:mm:ss"),user,ActionId(action),state,detail);
        if(state=="Thất bại")results.Rows[i].DefaultCellStyle.ForeColor=Color.Firebrick;
        else if(state=="Thành công")results.Rows[i].DefaultCellStyle.ForeColor=Color.DarkGreen;
        if(results.Rows.Count>1000)results.Rows.RemoveAt(0);
        Append(user+" · "+state+": "+detail);if(state=="Thất bại")showLog.Checked=true;
    }
    void ChangePassword()
    {
        var list=Selected().Where(u=>AccountRules.ProtectedReason(u.Sid,WindowsAccounts.CurrentSid)==null).ToList();if(list.Count==0)throw new InvalidOperationException("Các user được chọn đều được bảo vệ.");
        bool common=false;
        if(list.Count>1)
        {
            using var choice=new Form(){Text="Đổi mật khẩu nhiều tài khoản",Font=Font,ClientSize=new Size(480,200),StartPosition=FormStartPosition.CenterParent};
            var individual=new RadioButton(){Text="Nhập mật khẩu riêng từng user (khuyến nghị)",Checked=true,AutoSize=true};var shared=new RadioButton(){Text="Dùng chung một mật khẩu cho các user đã chọn",AutoSize=true};var flow=new FlowLayoutPanel(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,Padding=new Padding(18)};var next=new Button(){Text="Tiếp tục",DialogResult=DialogResult.OK,AutoSize=true};flow.Controls.AddRange(new Control[]{individual,shared,next});choice.Controls.Add(flow);if(choice.ShowDialog(this)!=DialogResult.OK)return;common=shared.Checked;
        }
        var passwords=new Dictionary<string,string>();
        try
        {
            if(common){using var dialog=new PasswordDialog("Đặt lại mật khẩu",list.Count+" tài khoản — dùng chung mật khẩu",true);if(dialog.ShowDialog(this)!=DialogResult.OK)return;foreach(var user in list)passwords[user.Sid]=dialog.Password;}
            else foreach(var user in list){using var dialog=new PasswordDialog("Đặt lại mật khẩu",user.Name,true);if(dialog.ShowDialog(this)!=DialogResult.OK)return;passwords[user.Sid]=dialog.Password;}
            // Batch owns a copy; no password is written to previews, logs or preferences.
            var submitted=new Dictionary<string,string>(passwords);
            Batch("Đặt lại mật khẩu Windows\r\nCó thể ảnh hưởng EFS/chứng chỉ/credential đã lưu; không buộc đổi mật khẩu ở lần đăng nhập sau.",u=>{if(!submitted.Remove(u.Sid,out var password))throw new InvalidOperationException("Thiếu mật khẩu cho user.");WindowsAccounts.ResetPassword(u,password);return "Đã đặt lại mật khẩu.";},true,cleanup:submitted.Clear);
        }
        finally{passwords.Clear();}
    }
    async void CreateUser()
    {
        try
        {

        var name=Prompt("Tên tài khoản Windows mới");if(name==null)return;name=Policy.UserName(name);
        using var password=new PasswordDialog("Tạo tài khoản",name+" — tài khoản thường, không cấp Administrator",false);if(password.ShowDialog(this)!=DialogResult.OK)return;
        Preset? preset=null;if(presetList.SelectedItem is Preset current&&MessageBox.Show(this,"Áp dụng bộ ‘"+current.Name+"’ cho user mới?","Bộ cấu hình",MessageBoxButtons.YesNo)==DialogResult.Yes)preset=current;
        if(!Confirm("Tạo user thường: "+name+(preset==null?"":"\r\n"+PresetPreview(preset))))return;var secret=password.Password;
        await ExecuteAsync(async()=>
        {
            await Task.Run(()=>WindowsAccounts.Create(name,secret));Append("Đã tạo user thường: "+name);
            await ReloadAsync();var user=allRows.Select(r=>r.User).Single(u=>u.Name.Equals(name,StringComparison.OrdinalIgnoreCase));Storage.Log("CreateUser",user.Sid,true);targets.Set(user.Sid,true);SyncChecks();
            if(preset!=null){try{await Task.Run(()=>Presets.Apply(preset,user));Storage.Log("ApplyPreset",user.Sid,true);AddResult(name,"Áp dụng bộ cấu hình","Thành công",preset.Name);}catch(Exception e){Storage.Log("ApplyPreset",user.Sid,false,ErrorCode(e));AddResult(name,"Áp dụng bộ cấu hình","Thất bại","User đã tạo; áp dụng bộ chưa hoàn tất: "+e.Message);}await ReloadAsync();}
        });

        }
        catch(Exception e){Report(e);}
    }
    string[] Names()=>shortcuts.CheckedItems.Cast<ListViewItem>().Select(i=>i.Text).ToArray();
    string? CurrentSourceSid()=>allRows.Select(r=>r.User).FirstOrDefault(u=>u.Name.Equals(source.Text.Trim(),StringComparison.CurrentCultureIgnoreCase))?.Sid;
    LocalUser Source()
    {
        var user=allRows.Select(r=>r.User).FirstOrDefault(u=>u.Name.Equals(source.Text.Trim(),StringComparison.CurrentCultureIgnoreCase));return user??throw new ArgumentException("Chọn user nguồn hợp lệ từ danh sách xổ xuống.");
    }
    async void ReadShortcuts()
    {
        await ExecuteAsync(async()=>
        {
            var from=Source();var snapshot=await Task.Run(()=>new{Paths=UserData.Shortcuts(from),Desktop=UserData.Desktop(from)});
            shortcuts.Items.Clear();shortcuts.LargeImageList!.Images.Clear();
            shortcuts.LargeImageList.Images.Add("link",Ui.Icon("\uE8A5"));shortcuts.LargeImageList.Images.Add("url",Ui.Icon("\uE774"));
            foreach(var path in snapshot.Paths){var n=Path.GetFileName(path);shortcuts.Items.Add(new ListViewItem(n,Path.GetExtension(n).Equals(".url",StringComparison.OrdinalIgnoreCase)?"url":"link"));}
            shortcutSourceSid=from.Sid;sourceLabel.Text=$"Nguồn: {from.Name} · {snapshot.Paths.Length} shortcut · {snapshot.Desktop}";Append(sourceLabel.Text);
        });
    }
    string ShortcutWarning()
    {
        if(shortcutSourceSid!=Source().Sid)throw new ArgumentException("Nguồn đã thay đổi. Bấm ‘Đọc Desktop nguồn’ trước.");
        var desktop=UserData.Desktop(Source());var warnings=new List<string>();foreach(var n in Names()){var path=Path.Combine(desktop,n);if(Path.GetExtension(n).Equals(".lnk",StringComparison.OrdinalIgnoreCase)){object? shell=null,link=null;try{shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);dynamic s=shell!;link=s.CreateShortcut(path);string target=((dynamic)link).TargetPath;if(!File.Exists(target)&&!Directory.Exists(target))warnings.Add(n+": đích không tồn tại/không kiểm tra được; vẫn cho phép sao chép.");}finally{if(link!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);if(shell!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}}}return string.Join(Environment.NewLine,warnings);
    }
    void PreviewDesktop()=>MessageBox.Show(this,"Shortcut đã chọn:\r\n"+string.Join(Environment.NewLine,Names())+"\r\n"+ShortcutWarning(),"Xem trước Desktop");
    void Sync(bool remove)
    {
        var names=Names();if(names.Length==0)throw new ArgumentException("Tick ít nhất một shortcut.");var from=Source();var warning=ShortcutWarning();
        Batch((remove?"Xóa shortcut Desktop":"Thêm shortcut Desktop, giữ bản trùng")+"\r\nNguồn: "+from.Name+"\r\n"+string.Join(", ",names)+"\r\n"+warning,u=>"Sao lưu: "+UserData.Sync(from,u,names,remove),excludedSid:from.Sid);
    }
    string RegistryPreview(){if(!File.Exists(regFile.Text))throw new ArgumentException("Chọn file .reg.");return string.Join(Environment.NewLine,Policy.ParseRegistry(File.ReadAllText(regFile.Text)).Select(c=>"HKCU\\"+c.Key+" → "+c.Name));}
    void ApplyRegistry(){var preview=RegistryPreview();var path=regFile.Text;Batch("Import Registry\r\n"+preview,u=>"Sao lưu: "+UserData.ApplyRegistry(u,path));}
    void ApplyFolders(){var root=privateRoot.Text;if(string.IsNullOrWhiteSpace(root))throw new ArgumentException("Chọn thư mục gốc.");Batch("Tạo thư mục riêng và đặt ACL\r\nGốc: "+root,u=>{UserData.PrivateFolder(u,root);return "Đã tạo thư mục mới và đặt quyền NTFS.";});}
    Preset Preset()=>presetList.SelectedItem as Preset??throw new ArgumentException("Chọn bộ cấu hình.");
    void RefreshPresets(){var id=(presetList.SelectedItem as Preset)?.Id;var list=Presets.List();presetList.DataSource=list;presetList.SelectedItem=list.FirstOrDefault(p=>p.Id==id)??list.FirstOrDefault();}
    void CreatePreset(){var n=Prompt("Tên bộ cấu hình");if(n==null)return;LocalUser? from=string.IsNullOrWhiteSpace(source.Text)?null:Source();if(Names().Length>0&&shortcutSourceSid!=from?.Sid)throw new ArgumentException("Đọc lại Desktop nguồn.");Presets.Create(n,from,Names(),string.IsNullOrWhiteSpace(regFile.Text)?null:regFile.Text,string.IsNullOrWhiteSpace(privateRoot.Text)?null:privateRoot.Text,presetRdp.Checked);RefreshPresets();Append("Đã lưu bộ cấu hình.");}
    static string PresetPreview(Preset p)=>"Bộ: "+p.Name+"\r\nCho phép RDP: "+p.Rdp+"\r\nShortcut: "+string.Join(", ",p.Shortcuts)+"\r\nRegistry: "+p.HasRegistry+"\r\nThư mục gốc: "+p.PrivateRoot+"\r\nNAS: "+string.Join(", ",p.Nas.Select(m=>m.Drive+" → "+m.Unc));
    static string ReadAgentReport(string profile)
    {
        if(!Path.IsPathFullyQualified(profile))return "Chưa có profile/báo cáo Agent";
        var file=Path.Combine(profile,"AppData","Local","WinUser","nas-status.json");if(!File.Exists(file))return "Chưa có báo cáo Agent";
        try
        {
            using var json=JsonDocument.Parse(SafeFile.ReadText(file));var root=json.RootElement;
            var time=root.GetProperty("utc").GetDateTime().ToUniversalTime();var broker=root.GetProperty("broker").GetBoolean();var age=DateTime.UtcNow-time;
            var stale=age>TimeSpan.FromMinutes(2)||age<TimeSpan.FromMinutes(-5);var states=new List<string>();
            foreach(var item in root.GetProperty("results").EnumerateArray())
            {
                var letter=Policy.Drive(item.GetProperty("drive").GetString()??"");var code=item.GetProperty("code").GetInt32();states.Add(letter+" "+(code==0?"đã ánh xạ / đã có":code==2401?"chờ ngắt mapping cũ":"lỗi "+code));
            }
            return time.ToLocalTime().ToString("g")+(stale?" · báo cáo cũ/sai thời gian":"")+" · Broker "+(broker?"kết nối":"chưa kết nối")+" · "+string.Join(", ",states)+" (báo cáo do user ghi, chưa xác thực)";
        }
        catch{return "Không đọc được báo cáo Agent hoặc dữ liệu không hợp lệ";}
    }
    void ShowNas()
    {
        nasInfo.Text="CẤU HÌNH ĐÃ LƯU\r\n"+string.Join(Environment.NewLine,nasMappings.Select(m=>allRows.FirstOrDefault(r=>r.User.Sid==m.Sid)?.Name+" | "+m.Drive+" → "+m.Unc+" | NAS user: "+m.User))+"\r\n\r\nBÁO CÁO AGENT (F5 để làm mới)\r\n"+string.Join(Environment.NewLine,allRows.Select(r=>r.Name+": "+r.NasAgent))+"\r\n\r\nTrạng thái thực tế cần xác nhận trong phiên user đích; cấu hình và báo cáo Agent không phải bằng chứng quyền truy cập NAS.";
    }
    void SaveNas(){var d=drive.Text;var path=unc.Text;var login=nasUser.Text;var secret=nasPassword.Text;Policy.Drive(d);Policy.Unc(path);Batch("Lưu ánh xạ NAS\r\n"+d+" → "+path,u=>{Nas.Save(u,d,path,login,secret);return "Đã lưu cấu hình; chờ Agent trong phiên user áp dụng.";});nasPassword.Clear();}
    void RemoveNas(){var d=drive.Text;Policy.Drive(d);Batch("Xóa cấu hình ánh xạ NAS\r\nỔ: "+d,u=>{Nas.Remove(u,d);return "Đã xóa cấu hình; Agent sẽ thử ngắt mapping, không ép đóng tệp.";});}
    async void TestNas()
    {
        try
        {

        var mapping=new AgentMapping(Policy.Drive(drive.Text),Policy.Unc(unc.Text),nasUser.Text,nasPassword.Text);
        if(!Confirm("Kiểm tra SMB trong phiên Administrator tới "+mapping.Unc+". Có thể tạo kết nối SMB tạm; không chứng minh user đích đã kết nối."))return;
        await ExecuteAsync(async()=>{var code=await Task.Run(()=>Nas.Connect(mapping,true));Append("NAS: "+code+" — "+(code==0?"Kết nối thành công trong phiên Administrator":new Win32Exception(code).Message)+" (85: trùng ổ; 1219: xung đột SMB; 1326: xác thực; 5: quyền).");showLog.Checked=true;});

        }
        catch(Exception e){Report(e);}
    }
    async void Restore(){
        try
        {
using var f=new FolderBrowserDialog(){InitialDirectory=Path.Combine(Storage.Root,"Backups")};if(f.ShowDialog(this)!=DialogResult.OK||!Confirm("Khôi phục từ "+f.SelectedPath+"? Có thể thay thế shortcut, Registry hoặc cấu hình hiện tại."))return;var path=f.SelectedPath;await ExecuteAsync(async()=>{await Task.Run(()=>UserData.Restore(path));Storage.Log("Restore","backup",true);Append("Đã khôi phục trong phạm vi bản sao lưu.");RefreshPresets();await ReloadAsync();});
        }
        catch(Exception e){Report(e);}
    }
    string? Prompt(string title,string initial="")
    {
        using var dialog=new Form(){Text=title,Font=Font,AutoScaleMode=AutoScaleMode.Dpi,ClientSize=new Size(440,140),StartPosition=FormStartPosition.CenterParent};var text=new TextBox(){Text=initial,Dock=DockStyle.Top};var ok=new Button(){Text="Tiếp tục",Dock=DockStyle.Bottom,DialogResult=DialogResult.OK};dialog.Controls.Add(text);dialog.Controls.Add(ok);dialog.AcceptButton=ok;return dialog.ShowDialog(this)==DialogResult.OK?text.Text:null;
    }
    void OnClosingForm(object? sender,FormClosingEventArgs e)
    {
        if(busy){e.Cancel=true;stopRequested=true;status.Text="Đang dừng sau thao tác hiện tại. Chờ hoàn tất rồi đóng cửa sổ.";return;}
        Safe(()=>{var bounds=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;preferences.Width=bounds.Width;preferences.Height=bounds.Height;preferences.Maximized=WindowState==FormWindowState.Maximized;preferences.Split=split.SplitterDistance;preferences.Filter=filter.SelectedIndex;preferences.PrivateRoot=privateRoot.Text;preferences.Columns=users.Columns.Cast<DataGridViewColumn>().ToDictionary(c=>c.Name,c=>c.Width);preferences.Save();});
    }
}
