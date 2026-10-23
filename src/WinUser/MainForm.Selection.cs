namespace WinUser;
public sealed partial class MainForm
{
    readonly TextBox userSearch=new(){Width=300,PlaceholderText="Tìm tên user / SID…"};
    readonly ComboBox userFilter=new(){Width=150,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly ComboBox selectionMode=new(){Width=300,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly Label selectionCount=new(){AutoSize=true,Text="USER ĐÍCH: 0"};
    List<LocalUser> allUsers=new();
    void BuildUserSelection(Panel left)
    {
        users.BackgroundColor=System.Drawing.Color.White;users.MultiSelect=true;
        users.Columns.Add(new DataGridViewTextBoxColumn(){DataPropertyName="AccountStatus",HeaderText="Tài khoản",ReadOnly=true,Width=150});
        users.Columns.Add(new DataGridViewTextBoxColumn(){DataPropertyName="SessionStatus",HeaderText="Phiên hiện tại",ReadOnly=true,Width=160});
        userFilter.Items.AddRange(new object[]{"Tất cả","Đang bật","Đã khóa","Mở RDP","Có phiên"});userFilter.SelectedIndex=0;
        selectionMode.Items.AddRange(new object[]{"Chọn bằng click / Ctrl / Shift / bôi đen","Chọn bằng ô tick"});selectionMode.SelectedIndex=0;
        users.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        foreach(DataGridViewColumn column in users.Columns){column.MinimumWidth=column.Name=="Selected"?45:80;column.FillWeight=column.Name=="Selected"?45:110;}
        var panel=Bar();panel.Controls.AddRange(new Control[]{selectionCount,new Label(){Text="Tìm tên / SID:",AutoSize=true},userSearch,userFilter,selectionMode});left.Controls.Add(panel);
        userSearch.TextChanged+=(_,_)=>FilterUsers();userFilter.SelectedIndexChanged+=(_,_)=>FilterUsers();
        selectionMode.SelectedIndexChanged+=(_,_)=>{users.ClearSelection();foreach(DataGridViewRow row in users.Rows)row.Cells[0].Value=false;UpdateSelectionCount();};
        users.SelectionChanged+=(_,_)=>UpdateSelectionCount();users.CurrentCellDirtyStateChanged+=(_,_)=>{if(users.IsCurrentCellDirty)users.CommitEdit(DataGridViewDataErrorContexts.Commit);};
        users.CellValueChanged+=(_,_)=>UpdateSelectionCount();
    }
    void UpdateSelectionCount()
    {
        var count=users.Rows.Cast<DataGridViewRow>().Count(r=>selectionMode.SelectedIndex==1?r.Cells[0].Value is true:r.Selected);
        selectionCount.Text=$"USER ĐÍCH: {count} / {users.Rows.Count} hiển thị";
        users.Columns[0].Visible=selectionMode.SelectedIndex==1;users.Columns[0].ReadOnly=false;
    }
    List<LocalUser> Selected()
    {
        users.EndEdit();var result=users.Rows.Cast<DataGridViewRow>().Where(r=>selectionMode.SelectedIndex==1?r.Cells[0].Value is true:r.Selected).Select(r=>(LocalUser)r.DataBoundItem).ToList();
        if(result.Count==0)throw new ArgumentException("Chọn user ĐÍCH ở bảng trái bằng click/Ctrl/Shift hoặc chế độ ô tick.");return result;
    }
    void FilterUsers()
    {
        // A changed view clears targets: no hidden users receive a bulk operation.
        var q=userSearch.Text.Trim();users.DataSource=allUsers.Where(u=>(u.Name.Contains(q,StringComparison.OrdinalIgnoreCase)||u.Sid.Contains(q,StringComparison.OrdinalIgnoreCase))&&(userFilter.SelectedIndex switch{1=>u.Enabled,2=>!u.Enabled||u.AccountStatus=="Khóa do sai mật khẩu",3=>u.Rdp,4=>u.SessionStatus!="Chưa đăng nhập",_=>true})).ToList();users.ClearSelection();users.CurrentCell=null;UpdateSelectionCount();
    }
    void RefreshUsers()
    {
        var sid=(source.SelectedItem as LocalUser)?.Sid;allUsers=WindowsAccounts.List();FilterUsers();source.DataSource=allUsers.ToList();source.SelectedIndex=-1;if(sid!=null)source.SelectedItem=allUsers.FirstOrDefault(u=>u.Sid==sid);
    }
    LocalUser SingleTarget(){var targets=Selected();if(targets.Count!=1)throw new ArgumentException("Chỉ chọn một user đích cho thao tác này.");return targets[0];}
}
