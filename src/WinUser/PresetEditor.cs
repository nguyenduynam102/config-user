namespace WinUser;
public sealed class PresetEditor:Form
{
    readonly Preset original;
    readonly TextBox name=new(){Width=300},root=new(){Width=350},registry=new(){Width=350,ReadOnly=true};
    readonly CheckBox rdp=new(){Text="Cho phép RDP",AutoSize=true},hasRegistry=new(){Text="Áp dụng Registry",AutoSize=true};
    readonly ListBox shortcuts=new(){Dock=DockStyle.Fill},nas=new(){Dock=DockStyle.Fill,DisplayMember="Unc"};
    readonly Dictionary<string,string> assets=new(StringComparer.OrdinalIgnoreCase);
    readonly List<NasMapping> mappings;
    public PresetEditor(Preset preset)
    {
        original=preset;mappings=preset.Nas.ToList();Text="Chỉnh sửa bộ cấu hình";Width=820;Height=660;StartPosition=FormStartPosition.CenterParent;
        name.Text=preset.Name;root.Text=preset.PrivateRoot??"";rdp.Checked=preset.Rdp;hasRegistry.Checked=preset.HasRegistry;
        if(preset.HasRegistry)registry.Text=Path.Combine(Presets.PathOf(preset.Id),"settings.reg");
        foreach(var n in preset.Shortcuts)assets[n]=Path.Combine(Presets.PathOf(preset.Id),n);
        var header=new FlowLayoutPanel(){Dock=DockStyle.Top,Height=150,AutoScroll=true};
        header.Controls.AddRange(new Control[]{new Label(){Text="Tên bộ",AutoSize=true},name,rdp,new Label(){Text="Thư mục gốc",AutoSize=true},root,B("Chọn thư mục",()=>{using var f=new FolderBrowserDialog();if(f.ShowDialog()==DialogResult.OK)root.Text=f.SelectedPath;}),hasRegistry,registry,B("Chọn .reg",()=>{using var f=new OpenFileDialog(){Filter="Registry|*.reg"};if(f.ShowDialog()==DialogResult.OK){Policy.ParseRegistry(File.ReadAllText(f.FileName));registry.Text=f.FileName;hasRegistry.Checked=true;}})});
        var tabs=new TabControl(){Dock=DockStyle.Fill};var desktopTab=new TabPage("Shortcut");var nasTab=new TabPage("NAS");tabs.TabPages.Add(desktopTab);tabs.TabPages.Add(nasTab);
        desktopTab.Controls.Add(shortcuts);var bar=new FlowLayoutPanel(){Dock=DockStyle.Top,Height=45};bar.Controls.AddRange(new Control[]{B("Thêm shortcut",()=>{using var f=new OpenFileDialog(){Filter="Shortcut|*.lnk;*.url",Multiselect=true};if(f.ShowDialog()!=DialogResult.OK)return;foreach(var file in f.FileNames){var n=Policy.Shortcut(Path.GetFileName(file));if(assets.ContainsKey(n))throw new ArgumentException("Tên shortcut đã có: "+n);assets[n]=file;}RefreshLists();}),B("Bỏ shortcut khỏi bộ",()=>{if(shortcuts.SelectedItem is string n)assets.Remove(n);RefreshLists();})});desktopTab.Controls.Add(bar);
        nasTab.Controls.Add(nas);var nbar=new FlowLayoutPanel(){Dock=DockStyle.Top,Height=45};nbar.Controls.AddRange(new Control[]{B("Thêm ánh xạ",()=>EditNas(null)),B("Sửa ánh xạ / mật khẩu",()=>{if(nas.SelectedItem is NasMapping m)EditNas(m);}),B("Bỏ ánh xạ khỏi bộ",()=>{if(nas.SelectedItem is NasMapping m)mappings.Remove(m);RefreshLists();})});nasTab.Controls.Add(nbar);
        var footer=new FlowLayoutPanel(){Dock=DockStyle.Bottom,Height=70};footer.Controls.AddRange(new Control[]{new Label(){Text="Chỉ sửa bộ cấu hình; chưa áp dụng lên user. Mật khẩu cũ không hiển thị.",AutoSize=true},B("Xem trước và lưu",Save)});
        Controls.Add(tabs);Controls.Add(header);Controls.Add(footer);RefreshLists();
    }
    Button B(string text,Action action){var b=new Button(){Text=text,AutoSize=true};b.Click+=(_,_)=>{try{action();}catch(Exception e){MessageBox.Show(e.Message,"Không lưu được",MessageBoxButtons.OK,MessageBoxIcon.Error);}};return b;}
    void RefreshLists(){shortcuts.DataSource=assets.Keys.ToList();nas.DataSource=null;nas.DataSource=mappings.ToList();}
    void EditNas(NasMapping? old)
    {
        using var d=new Form(){Text="Ánh xạ NAS trong bộ",Width=520,Height=300,StartPosition=FormStartPosition.CenterParent};var flow=new FlowLayoutPanel(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown};
        var drive=new TextBox(){Width=430,Text=old?.Drive??"Z:"};var unc=new TextBox(){Width=430,Text=old?.Unc??@"\\nas\share"};var user=new TextBox(){Width=430,Text=old?.User??""};var password=new TextBox(){Width=430,UseSystemPasswordChar=true,PlaceholderText=old==null?"Mật khẩu NAS":"Để trống để giữ mật khẩu cũ"};
        var save=new Button(){Text="Lưu vào bộ",DialogResult=DialogResult.OK,AutoSize=true};flow.Controls.AddRange(new Control[]{new Label(){Text="Ổ đĩa / UNC / NAS user / mật khẩu",AutoSize=true},drive,unc,user,password,save});d.Controls.Add(flow);
        if(d.ShowDialog(this)!=DialogResult.OK)return;
        var letter=Policy.Drive(drive.Text);var path=Policy.Unc(unc.Text);if(string.IsNullOrWhiteSpace(user.Text))throw new ArgumentException("Cần tài khoản NAS.");
        if(mappings.Any(m=>m!=old&&m.Drive==letter))throw new ArgumentException("Trùng ký tự ổ trong bộ.");
        var secret=string.IsNullOrEmpty(password.Text)?old?.ProtectedPassword:Storage.Encrypt(password.Text);password.Clear();
        if(secret==null)throw new ArgumentException("Cần mật khẩu cho ánh xạ mới.");
        if(old!=null)mappings.Remove(old);mappings.Add(new("",letter,path,user.Text,secret));RefreshLists();
    }
    void Save()
    {
        if(string.IsNullOrWhiteSpace(name.Text))throw new ArgumentException("Cần tên bộ.");
        if(hasRegistry.Checked)Policy.ParseRegistry(File.ReadAllText(registry.Text));
        if(mappings.GroupBy(m=>m.Unc.Split('\\')[2],StringComparer.OrdinalIgnoreCase).Any(g=>g.Select(m=>m.User).Distinct(StringComparer.OrdinalIgnoreCase).Count()>1))
            throw new ArgumentException("Cùng máy chủ SMB phải dùng cùng tài khoản NAS trong một phiên.");
        var updated=original with{Name=name.Text,Rdp=rdp.Checked,PrivateRoot=string.IsNullOrWhiteSpace(root.Text)?null:Path.GetFullPath(root.Text),HasRegistry=hasRegistry.Checked,Shortcuts=assets.Keys.ToArray(),Nas=mappings.ToList()};
        if(MessageBox.Show("Lưu bộ "+updated.Name+"\r\nRDP: "+updated.Rdp+"\r\nShortcut: "+updated.Shortcuts.Length+"\r\nRegistry: "+updated.HasRegistry+"\r\nThư mục: "+updated.PrivateRoot+"\r\nNAS: "+updated.Nas.Count+"\r\nBản cũ sẽ được sao lưu. Chưa thay đổi user.","Xác nhận",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
        Presets.Update(updated,assets,hasRegistry.Checked?registry.Text:null);DialogResult=DialogResult.OK;Close();
    }
}
