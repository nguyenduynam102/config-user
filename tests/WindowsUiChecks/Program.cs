using System.Drawing;
using System.Reflection;
using WinUser;
namespace WindowsUiChecks;
internal static class Program
{
    static int passed;
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object form,string name)=>(T)form.GetType().GetField(name,Private)!.GetValue(form)!;
    static void Invoke(object form,string method,params object[] args)=>form.GetType().GetMethod(method,Private)!.Invoke(form,args);
    static void Check(string name,Action action){action();passed++;Console.WriteLine("PASS "+name);}
    static IEnumerable<T> Find<T>(Control control)where T:Control=>control.Controls.Cast<Control>().SelectMany(c=>Find<T>(c).Concat(c is T item?new[]{item}:Array.Empty<T>()));
    [STAThread]static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();var output=Path.GetFullPath(args.Length>0?args[0]:"artifacts/ui-checks");Directory.CreateDirectory(output);
        if(!WindowsAccounts.IsAdmin)throw new InvalidOperationException("Read-only UI checks require an elevated runner to read local account policy.");
        typeof(MainForm).Assembly.GetType("WinUser.Privileges")!.GetMethod("EnableAdministration",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,null);Storage.Initialize();
        Check("real Windows local account API and policy preflight",()=>{if(WindowsAccounts.List().Count==0)throw new Exception("No local users");});
        using var form=new MainForm();
        var watchdog=new System.Windows.Forms.Timer(){Interval=120000};watchdog.Tick+=(_,_)=>{Console.Error.WriteLine("FAIL UI watchdog timed out");Environment.Exit(1);};watchdog.Start();
        form.Shown+=async(_,_)=>
        {
            try
            {
                while(Field<bool>(form,"busy"))await Task.Delay(100);
                var grid=Field<DataGridView>(form,"users");var targets=Field<TargetSelection>(form,"targets");var search=Field<TextBox>(form,"search");var source=Field<ComboBox>(form,"source");
                Check("real Windows NetAPI populates account grid",()=>{if(grid.Rows.Count==0)throw new Exception("No users loaded");});
                Check("startup never preselects operation targets",()=>{if(targets.Count!=0)throw new Exception("Unexpected target");});
                Check("highlight inspects without selecting targets",()=>{grid.CurrentCell=grid.Rows[0].Cells["Name"];grid.Rows[0].Selected=true;if(targets.Count!=0)throw new Exception("Highlight changed targets");});
                Check("highlighted rows can become checked targets",()=>{Invoke(form,"MarkRows",true,true);if(targets.Count!=grid.SelectedRows.Count||targets.Count==0)throw new Exception("Selection mismatch");});
                var chosen=targets.Count;
                Check("filter preserves hidden targets and displays count",()=>{search.Text="__no_such_user_fixture__";if(grid.Rows.Count!=0||targets.Count!=chosen||!Field<Label>(form,"selectionLabel").Text.Contains("ẩn"))throw new Exception("Hidden target lost");});
                Check("clearing filter restores checked users",()=>{search.Clear();if(grid.Rows.Cast<DataGridViewRow>().Count(r=>r.Cells[0].Value is true)!=chosen)throw new Exception("Checks lost");});
                Check("source supports searchable dropdown",()=>{if(source.DropDownStyle!=ComboBoxStyle.DropDown||source.AutoCompleteMode!=AutoCompleteMode.SuggestAppend)throw new Exception("Source not searchable");source.SelectedIndex=0;});
                Check("account actions separated",()=>{var buttons=Find<Button>(form).Select(b=>b.Text).ToArray();foreach(var caption in new[]{"Khóa tài khoản","Ngắt phiên RDP","Đổi mật khẩu","Mở khóa nhập sai"})if(!buttons.Any(t=>t.StartsWith(caption)))throw new Exception("Missing "+caption);});
                Check("source is excluded from Desktop destination actions",()=>
                {
                    var user=(LocalUser)source.SelectedItem!;targets.Clear();targets.Set(user.Sid,true);Invoke(form,"SyncChecks");
                    if(Find<Button>(form).Single(b=>b.Text.StartsWith("Thêm shortcut (")).Enabled)throw new Exception("Source remains a destination");
                });
                Check("current administrator account controls are disabled",()=>
                {
                    targets.Clear();targets.Set(WindowsAccounts.CurrentSid,true);Invoke(form,"SyncChecks");
                    if(Find<Button>(form).Single(b=>b.Text.StartsWith("Khóa tài khoản (")).Enabled)throw new Exception("Current administrator not protected");
                });
                var refresh=(Task)form.GetType().GetMethod("ReloadAsync",Private)!.Invoke(form,null)!;await refresh;
                Check("refresh preserves explicit targets and source SID",()=>{if(!targets.Contains(WindowsAccounts.CurrentSid)||source.SelectedItem is not LocalUser)throw new Exception("Refresh lost selection");});
                Check("password confirmation buttons visible",()=>
                {
                    var type=typeof(MainForm).Assembly.GetType("WinUser.PasswordDialog")!;using var dialog=(Form)Activator.CreateInstance(type,"Fixture","No account will be changed",true)!;dialog.Show(form);dialog.PerformLayout();
                    var button=Find<Button>(dialog).Single(b=>b.Text=="Tiếp tục");var rect=new Rectangle(dialog.PointToClient(button.PointToScreen(Point.Empty)),button.Size);
                    if(!button.Visible||!dialog.ClientRectangle.Contains(rect))throw new Exception("Password dialog action clipped");dialog.Close();
                });
                Check("native guarded file read copy delete within disposable fixture only",()=>
                {
                    var folder=Path.Combine(Storage.Root,"UiFixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
                    try
                    {
                        var file=Path.Combine(folder,"source.txt");var copy=Path.Combine(folder,"copy.txt");File.WriteAllText(file,"fixture-only");
                        var type=typeof(MainForm).Assembly.GetType("WinUser.SafeFile")!;
                        var text=(string)type.GetMethod("ReadText",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,new object[]{file,65536})!;
                        if(text!="fixture-only")throw new Exception("Wrong file data");
                        type.GetMethod("Copy",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,new object[]{file,copy,false});
                        if(File.ReadAllText(copy)!="fixture-only")throw new Exception("Copy failed");
                        type.GetMethod("Delete",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,new object[]{copy});
                        if(File.Exists(copy))throw new Exception("Delete failed");
                    }
                    finally{Directory.Delete(folder,true);}
                });
                foreach(var width in new[]{1180,1360})
                {
                    form.Size=new Size(width,Math.Min(850,Screen.PrimaryScreen!.WorkingArea.Height));form.PerformLayout();await Task.Delay(100);
                    using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,$"main-{width}.png"));
                }
                targets.Clear();Invoke(form,"SyncChecks");Console.WriteLine($"{passed} read-only Windows UI checks passed. No accounts/passwords/RDP settings changed.");
            }
            catch(Exception e){Console.Error.WriteLine("FAIL "+e);Environment.ExitCode=1;}
            finally{watchdog.Stop();form.Close();}
        };
        Application.Run(form);
    }
}
