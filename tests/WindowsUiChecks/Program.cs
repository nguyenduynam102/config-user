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
                Check("Preview 2 interface and correct version",()=>{if(!form.Text.Contains("Preview 2.1")||grid.Rows.Count==0)throw new Exception("Wrong UI or empty accounts");});
                Check("NAS has explicit add edit remove and health actions",()=>{var labels=Find<Button>(form).Select(b=>b.Text).ToArray();foreach(var title in new[]{"Thêm ánh xạ","Sửa ánh xạ","Xóa ánh xạ đã chọn","Kiểm tra hệ thống NAS"})if(!labels.Contains(title))throw new Exception("Missing "+title);});
                Check("NAS supports row-based mapping management",()=>{var mappingGrid=Field<DataGridView>(form,"nasGrid");if(mappingGrid.MultiSelect||mappingGrid.Columns.Count!=4)throw new Exception("Wrong NAS grid");});
                Check("private folder has export without rewriting Preview 2 workflow",()=>{if(!Find<Button>(form).Any(b=>b.Text=="Xuất ZIP chia sẻ"))throw new Exception("Missing export action");});
                using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,"preview2.1.png"));
                Console.WriteLine($"{passed} Windows UI checks passed.");
            }
            catch(Exception e){Console.Error.WriteLine("FAIL "+e);Environment.ExitCode=1;}
            finally{watchdog.Stop();form.Close();}
        };
        Application.Run(form);return Environment.ExitCode;
    }
    static void Enable()=>typeof(MainForm).Assembly.GetType("WinUser.Privileges")!.GetMethod("EnableAdministration",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,null);
}
