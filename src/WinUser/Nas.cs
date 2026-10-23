using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
namespace WinUser;
public record NasMapping(string Sid,string Drive,string Unc,string User,string ProtectedPassword,string Label="");
public record AgentMapping(string Drive,string Unc,string User,string Password,string Revision="",string Label="");
public record ManagedMapping(string Unc,string Revision,string Label="");
public static class Nas
{
    public const string ServiceName="WinUserBroker";
    const string PipeName="WinUser.Credentials.v1";
    static string FilePath=>Path.Combine(Storage.Root,"nas.json");
    public static List<NasMapping> Load()=>Storage.Read(FilePath,new List<NasMapping>());
    public static void Save(LocalUser user,string drive,string unc,string login,string password,bool overwrite=true,string label="")
    {
        label=NasLabels.Validate(label);user=WindowsAccounts.Fresh(user);drive=Policy.Drive(drive);unc=Policy.Unc(unc);
        if(string.IsNullOrWhiteSpace(login)||string.IsNullOrEmpty(password))throw new ArgumentException("Cần tài khoản và mật khẩu NAS.");
        var all=Load();if(!overwrite&&all.Any(m=>m.Sid==user.Sid&&m.Drive==drive))throw new ArgumentException("Ổ này đã có cấu hình cho user; chọn Sửa thay vì Thêm.");var server=unc.Split('\\')[2];
        if(all.Any(m=>m.Sid==user.Sid && m.Drive!=drive && m.Unc.Split('\\')[2].Equals(server,StringComparison.OrdinalIgnoreCase) && !m.User.Equals(login,StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Windows không hỗ trợ nhiều thông tin đăng nhập SMB cho cùng máy chủ trong một phiên.");
        if(all.Any(m=>m.Sid==user.Sid&&m.Drive!=drive&&m.Unc.Equals(unc,StringComparison.OrdinalIgnoreCase)&&m.Label!=label))throw new ArgumentException("Các ổ cùng share phải dùng cùng tên hiển thị trong Explorer.");
        Backup();all.RemoveAll(m=>m.Sid==user.Sid&&m.Drive==drive);all.Add(new(user.Sid,drive,unc,login,Storage.Encrypt(password),label));Storage.Write(FilePath,all);
    }
    public static void Remove(LocalUser user,string drive)
    {var all=Load();drive=Policy.Drive(drive);Backup();all.RemoveAll(m=>m.Sid==user.Sid&&m.Drive==drive);Storage.Write(FilePath,all);}
    static void Backup() {if(File.Exists(FilePath))File.Copy(FilePath,Path.Combine(Storage.BackupFolder("NAS","config"),"nas.json"));}
    public static void Broker(CancellationToken stop)
    {
        while(!stop.IsCancellationRequested)
        {
            var security=new PipeSecurity();security.SetAccessRuleProtection(true,false);
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier("S-1-5-2"),PipeAccessRights.FullControl,AccessControlType.Deny));
            foreach(var sid in new[]{"S-1-5-18","S-1-5-32-544"})security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(sid),PipeAccessRights.FullControl,AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier("S-1-5-11"),PipeAccessRights.ReadWrite,AccessControlType.Allow));
            try
            {
                using var pipe=NamedPipeServerStreamAcl.Create(PipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,16384,16384,security);
                pipe.WaitForConnectionAsync(stop).GetAwaiter().GetResult();
                using var helloTimeout=CancellationTokenSource.CreateLinkedTokenSource(stop);helloTimeout.CancelAfter(5000);
                var expected=Encoding.ASCII.GetBytes("WINUSER/1\n");var hello=new byte[expected.Length];
                pipe.ReadExactlyAsync(hello,helloTimeout.Token).AsTask().GetAwaiter().GetResult();
                if(!hello.SequenceEqual(expected))continue;
                // Impersonation must follow a read: Windows uses the last message's client context.
                string? sid=null;
                pipe.RunAsClient(()=> { using var identity=WindowsIdentity.GetCurrent(true);sid=identity?.User?.Value; });
                if(sid==null)continue;
                // No requested SID is accepted from the caller.
                var user=WindowsAccounts.List().SingleOrDefault(u=>u.Sid==sid);
                var payload=user is { Enabled:true }?Load().Where(m=>m.Sid==sid).Select(m=>new AgentMapping(m.Drive,m.Unc,m.User,Storage.Decrypt(m.ProtectedPassword),Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(m.ProtectedPassword))),m.Label)).ToArray():Array.Empty<AgentMapping>();
                var bytes=JsonSerializer.SerializeToUtf8Bytes(payload);
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop);timeout.CancelAfter(TimeSpan.FromSeconds(5));
                pipe.WriteAsync(bytes,timeout.Token).AsTask().GetAwaiter().GetResult();
                pipe.WriteAsync(new byte[]{10},timeout.Token).AsTask().GetAwaiter().GetResult();
                CryptographicOperations.ZeroMemory(bytes);
            }
            catch(OperationCanceledException) { }
            catch { if(!stop.IsCancellationRequested)stop.WaitHandle.WaitOne(2000); }
        }
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Resource { public int Scope,Type,Display,Usage;public string? Local,Remote,Comment,Provider; }
    [DllImport("mpr.dll",CharSet=CharSet.Unicode)] static extern int WNetAddConnection2(ref Resource resource,string password,string user,int flags);
    [DllImport("mpr.dll",CharSet=CharSet.Unicode)] static extern int WNetGetConnection(string local,StringBuilder remote,ref int length);
    [DllImport("mpr.dll",CharSet=CharSet.Unicode)] static extern int WNetCancelConnection2(string name,int flags,bool force);
    public static int Connect(AgentMapping m,bool test=false)
    {
        Policy.Drive(m.Drive);Policy.Unc(m.Unc);
        if(!test)
        {
            var buffer=new StringBuilder(4096);var length=buffer.Capacity;var status=WNetGetConnection(m.Drive,buffer,ref length);
            if(status==0)return buffer.ToString().Equals(m.Unc,StringComparison.OrdinalIgnoreCase)?0:85;
            if(Directory.Exists(m.Drive+"\\"))return 85;
        }
        var r=new Resource {Type=1,Local=test?null:m.Drive,Remote=m.Unc};
        return WNetAddConnection2(ref r,m.Password,m.User,0); // No command line, no persistent credential files.
    }
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe,out uint pid);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr OpenSCManager(string? machine,string? database,uint access);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr OpenService(IntPtr manager,string name,uint access);
    [DllImport("advapi32.dll",SetLastError=true)] static extern bool QueryServiceStatusEx(IntPtr service,int level,out ServiceStatus status,int size,out int needed);
    [DllImport("advapi32.dll")] static extern bool CloseServiceHandle(IntPtr handle);
    [StructLayout(LayoutKind.Sequential)] struct ServiceStatus {public uint Type,State,Accepted,Exit,Specific,Checkpoint,Wait,Pid,Flags;}
    static bool GenuineBroker(NamedPipeClientStream pipe)
    {
        if(!GetNamedPipeServerProcessId(pipe.SafePipeHandle,out var pid))return false;
        var manager=OpenSCManager(null,null,1);if(manager==IntPtr.Zero)return false;
        try {var service=OpenService(manager,ServiceName,4);if(service==IntPtr.Zero)return false;
            try { return QueryServiceStatusEx(service,0,out var status,Marshal.SizeOf<ServiceStatus>(),out _) && status.State==4 && status.Pid==pid; }
            finally { CloseServiceHandle(service); }}
        finally { CloseServiceHandle(manager); }
    }
    public static async Task<AgentMapping[]> FetchMappings(CancellationToken stop)
    {
        using var pipe=new NamedPipeClientStream(".",PipeName,PipeDirection.InOut,PipeOptions.Asynchronous,TokenImpersonationLevel.Impersonation);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop);timeout.CancelAfter(10000);
        await pipe.ConnectAsync(timeout.Token);
        if(!GenuineBroker(pipe))throw new IOException("Broker không hợp lệ hoặc không chạy từ dịch vụ đã đăng ký.");
        await pipe.WriteAsync(Encoding.ASCII.GetBytes("WINUSER/1\n"),timeout.Token);await pipe.FlushAsync(timeout.Token);
        using var reader=new StreamReader(pipe);var text=await reader.ReadLineAsync(timeout.Token);
        if(text==null||text.Length>1024*1024)throw new IOException("Dữ liệu Broker không hợp lệ.");
        return JsonSerializer.Deserialize<AgentMapping[]>(text)??Array.Empty<AgentMapping>();
    }
    public static void Update(LocalUser user,string oldDrive,string drive,string unc,string login,string? password,string label="")
    {
        var old=Load().Single(m=>m.Sid==user.Sid&&m.Drive==Policy.Drive(oldDrive));
        drive=Policy.Drive(drive);unc=Policy.Unc(unc);
        if(drive!=old.Drive&&Load().Any(m=>m.Sid==user.Sid&&m.Drive==drive))throw new ArgumentException("Ổ đích đã có cấu hình khác.");
        if(string.IsNullOrEmpty(password))
        {
            if(!login.Equals(old.User,StringComparison.OrdinalIgnoreCase)||!unc.Split('\\')[2].Equals(old.Unc.Split('\\')[2],StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Đổi máy chủ/tài khoản NAS cần nhập mật khẩu mới.");
            password=Storage.Decrypt(old.ProtectedPassword);
        }
        Save(user,drive,unc,login,password,true,label);
        if(old.Drive!=drive)Remove(user,old.Drive);
    }
    public static string Health()
    {
        var lines=new List<string>();
        try{using var service=new ServiceController(ServiceName);lines.Add("Broker: "+service.Status);}catch{lines.Add("Broker: chưa cài hoặc không truy vấn được. Đóng WinUser rồi chạy Install.cmd bằng Administrator.");}
        using var run=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
        var installed=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WinUser","WinUser.exe");
        if(File.Exists(installed))
        {
            var version=System.Diagnostics.FileVersionInfo.GetVersionInfo(installed).FileVersion;lines.Add("Bản NAS đã cài: "+version);
            if(version!="1.0.0.22")lines.Add("Bản cài cũ: đóng WinUser/Agent cũ rồi chạy Install.cmd của Preview 2.1 để cập nhật Broker/Agent.");
        }
        lines.Add("Agent khi đăng nhập: "+(run?.GetValue("WinUserNasAgent") is string?"đã đăng ký":"chưa đăng ký — cần Install.cmd"));
        lines.Add("Broker sẽ khởi chạy Agent cho user có mapping đang đăng nhập, thử lại mỗi 15 giây. User chưa đăng nhập nhận mapping ở lần đăng nhập sau.");
        return string.Join(Environment.NewLine,lines);
    }
    public static async Task Agent(CancellationToken stop)
    {
        var statusFile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WinUser","nas-status.json");Directory.CreateDirectory(Path.GetDirectoryName(statusFile)!);
        var managedFile=Path.Combine(Path.GetDirectoryName(statusFile)!,"managed.json");
        Dictionary<string,ManagedMapping> managed;
        try {managed=Storage.Read(managedFile,new Dictionary<string,ManagedMapping>(StringComparer.OrdinalIgnoreCase));}
        catch {managed=new(StringComparer.OrdinalIgnoreCase);}
        while(!stop.IsCancellationRequested)
        {
            var results=new List<object>();
            try
            {
                var mappings=await FetchMappings(stop);
                foreach(var old in managed.ToArray())
                {
                    if(mappings.Any(m=>m.Drive==old.Key&&m.Unc==old.Value.Unc&&m.Revision==old.Value.Revision))continue;
                    var remote=new StringBuilder(4096);var length=remote.Capacity;
                    if(WNetGetConnection(old.Key,remote,ref length)==0 && remote.ToString().Equals(old.Value.Unc,StringComparison.OrdinalIgnoreCase))
                    {var code=WNetCancelConnection2(old.Key,0,false);if(code!=0){results.Add(new{drive=old.Key,code});continue;}}
                    NasLabels.Clear(old.Value.Unc,old.Value.Label);managed.Remove(old.Key);
                }
                foreach(var m in mappings)
                {
                    if(managed.TryGetValue(m.Drive,out var old) && (old.Unc!=m.Unc || old.Revision!=m.Revision))
                    {results.Add(new{drive=m.Drive,code=2401});continue;}
                    var code=Connect(m);if(code==0){if(managed.TryGetValue(m.Drive,out var previous)&&previous.Label!=m.Label)NasLabels.Clear(previous.Unc,previous.Label);NasLabels.Apply(m.Unc,m.Label);managed[m.Drive]=new(m.Unc,m.Revision,m.Label);}results.Add(new{drive=m.Drive,code});
                }
                Storage.Write(managedFile,managed);
                Storage.Write(statusFile,new{utc=DateTime.UtcNow,broker=true,results});
            }
            catch(OperationCanceledException)when(stop.IsCancellationRequested){break;}
            catch {Storage.Write(statusFile,new{utc=DateTime.UtcNow,broker=false,results});}
            await Task.Delay(30000,stop);
        }
    }
}
public sealed class BrokerService:ServiceBase
{
    CancellationTokenSource? cancellation;Task? worker,launcher;
    public BrokerService(){ServiceName=Nas.ServiceName;CanStop=true;}
    protected override void OnStart(string[] args){Privileges.EnableAdministration();Storage.Initialize();cancellation=new();worker=Task.Run(()=>Nas.Broker(cancellation.Token));launcher=Task.Run(()=>AgentLauncher.Run(cancellation.Token));}
    protected override void OnStop(){cancellation?.Cancel();worker?.Wait(TimeSpan.FromSeconds(10));launcher?.Wait(TimeSpan.FromSeconds(5));}
}
