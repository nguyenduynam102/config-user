using System.Diagnostics;
using System.ServiceProcess;
namespace WinUser;
internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        if(!OperatingSystem.IsWindows())return;
        if(args.Contains("--broker")){ServiceBase.Run(new BrokerService());return;}
        if(args.Contains("--agent"))
        {
            using var mutex=new Mutex(true,@"Local\WinUser.NasAgent",out var created);if(!created)return;
            try {Nas.Agent(CancellationToken.None).GetAwaiter().GetResult();}catch { }
            return;
        }
        ApplicationConfiguration.Initialize();
        if(!WindowsAccounts.IsAdmin)
        {
            try {Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true,Verb="runas"});}
            catch {MessageBox.Show("Cần quyền Administrator để quản lý tài khoản Windows.","WinUser");}
            return;
        }
        try {Privileges.EnableAdministration();Storage.Initialize();Application.Run(new MainForm());}
        catch(Exception e){MessageBox.Show(e.Message,"Không khởi động được WinUser",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
}
