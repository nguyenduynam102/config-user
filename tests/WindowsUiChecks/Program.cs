using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using WinUser;
namespace WindowsUiChecks;
internal static partial class Program
{
    static int passed;
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object form,string name)=>(T)form.GetType().GetField(name,Private)!.GetValue(form)!;
    static void Check(string name,Action action){action();passed++;Console.WriteLine("PASS "+name);}
    static IEnumerable<T> Find<T>(Control control)where T:Control=>control.Controls.Cast<Control>().SelectMany(c=>Find<T>(c).Concat(c is T item?new[]{item}:Array.Empty<T>()));
    [STAThread]static int Main(string[] args)
    {
        if(args.FirstOrDefault()=="--client")return Client(args);
        if(args.FirstOrDefault()=="--integration-fixture")return Integration(args);
        ApplicationConfiguration.Initialize();var output=Path.GetFullPath(args.Length>0?args[0]:"artifacts/ui-checks");Directory.CreateDirectory(output);
        if(!WindowsAccounts.IsAdmin)throw new InvalidOperationException("Requires elevated disposable Windows runner.");Enable();Storage.Initialize();
        Check("real local account API",()=>{if(WindowsAccounts.List().Count==0)throw new Exception("No local users");});
        using var form=new MainForm();var watchdog=new System.Windows.Forms.Timer(){Interval=120000};watchdog.Tick+=(_,_)=>Environment.Exit(1);watchdog.Start();
        form.Shown+=async(_,_)=>
        {
            try
            {
                await Task.Delay(300);var grid=Field<DataGridView>(form,"users");
                Check("Preview 2 interface and correct version",()=>{if(!form.Text.Contains("Preview 2.2")||grid.Rows.Count==0)throw new Exception("Wrong UI or empty accounts");});
                Check("NAS has explicit add edit remove and health actions",()=>{var labels=Find<Button>(form).Select(b=>b.Text).ToArray();foreach(var title in new[]{"Thêm ánh xạ","Sửa ánh xạ","Xóa ánh xạ đã chọn","Kiểm tra hệ thống NAS"})if(!labels.Contains(title))throw new Exception("Missing "+title);});
                Check("NAS supports row-based mapping management",()=>{var mappingGrid=Field<DataGridView>(form,"nasGrid");if(mappingGrid.MultiSelect||mappingGrid.Columns.Count!=5)throw new Exception("Wrong NAS grid");});
                Check("private folder has export without rewriting Preview 2 workflow",()=>{if(!Find<Button>(form).Any(b=>b.Text=="Xuất ZIP chia sẻ"))throw new Exception("Missing export action");});
                Check("account commands and RDP labels are explicit",()=>{var labels=Find<Button>(form).Select(b=>b.Text).ToArray();foreach(var title in new[]{"Mở RDP","Đóng RDP","Đổi mật khẩu","Đổi tên","Xóa user","Sửa / Chi tiết","Ngắt phiên","Khóa tài khoản","Sao chép ra thư mục chung"})if(!labels.Contains(title))throw new Exception("Missing "+title);if(labels.Any(t=>t.Contains("Shutdown")))throw new Exception("Shutdown excluded by user");});
                Check("RDP column displays configured state instead of group membership",()=>{if(!grid.Columns.Cast<DataGridViewColumn>().Any(c=>c.DataPropertyName=="RdpStatus"&&c.HeaderText=="Trạng thái RDP"))throw new Exception("Ambiguous RDP column");});
                Check("source is independent and never automatically selected",()=>{if(Field<ComboBox>(form,"source").SelectedIndex!=-1)throw new Exception("Unexpected source");});
                Check("highlight multi-select is used as bulk target selection",()=>{grid.ClearSelection();grid.Rows[0].Selected=true;grid.Rows[1].Selected=true;var targets=(List<LocalUser>)typeof(MainForm).GetMethod("Selected",Private)!.Invoke(form,null)!;if(targets.Count!=2)throw new Exception("Selection mismatch");});
                Check("filter removes hidden bulk targets",()=>{var query=Field<TextBox>(form,"userSearch");var name=((LocalUser)grid.Rows[0].DataBoundItem).Name;query.Text=name;if(grid.Rows.Cast<DataGridViewRow>().Any(r=>r.Selected)||grid.Rows.Cast<DataGridViewRow>().Any(r=>!((LocalUser)r.DataBoundItem).Name.Contains(name,StringComparison.OrdinalIgnoreCase)))throw new Exception("Hidden target or search failed");query.Clear();});
                Check("checkbox mode commits explicit target set",()=>{Field<ComboBox>(form,"selectionMode").SelectedIndex=1;grid.Rows[0].Cells[0].Value=true;grid.Rows[1].Selected=true;var targets=(List<LocalUser>)typeof(MainForm).GetMethod("Selected",Private)!.Invoke(form,null)!;if(targets.Count!=1||targets[0].Sid!=((LocalUser)grid.Rows[0].DataBoundItem).Sid)throw new Exception("Checkbox/highlight conflated");Field<ComboBox>(form,"selectionMode").SelectedIndex=0;if(grid.Rows.Cast<DataGridViewRow>().Any(r=>r.Selected))throw new Exception("Mode retained old targets");});
                using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,"preview2.2.png"));
                var tabs=Find<TabControl>(form).First();foreach(TabPage page in tabs.TabPages){tabs.SelectedTab=page;using var shot=new Bitmap(form.Width,form.Height);form.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(output,"tab-"+page.TabIndex+"-"+tabs.SelectedIndex+".png"));}
                Console.WriteLine($"{passed} Windows UI checks passed.");
            }
            catch(Exception e){Console.Error.WriteLine("FAIL "+e);Environment.ExitCode=1;}
            finally{watchdog.Stop();form.Close();}
        };
        Application.Run(form);return Environment.ExitCode;
    }
    static void Enable()=>typeof(MainForm).Assembly.GetType("WinUser.Privileges")!.GetMethod("EnableAdministration",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,null);
}
