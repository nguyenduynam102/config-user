using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using WinUser;
namespace WindowsUiChecks;
internal static partial class Program
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Startup{public int Size;public string? Reserved,Desktop,Title;public int X,Y,W,H,Cx,Cy,Fill,Flags;public short Show,Bytes;public IntPtr ReservedPointer,In,Out,Error;}
    [StructLayout(LayoutKind.Sequential)]struct ProcessInfo{public IntPtr Process,Thread;public int Id,Tid;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Member{public string Name;}
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateProcessWithLogonW(string user,string domain,string password,int flags,string application,StringBuilder command,uint creation,IntPtr environment,string directory,ref Startup startup,out ProcessInfo process);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll",SetLastError=true)]static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetExitCodeProcess(IntPtr handle,out uint code);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool TerminateProcess(IntPtr handle,uint code);
    [DllImport("Netapi32.dll",CharSet=CharSet.Unicode)]static extern int NetUserDel(string? server,string name);
    [DllImport("Netapi32.dll",CharSet=CharSet.Unicode)]static extern int NetLocalGroupAddMembers(string? server,string group,int level,ref Member member,int count);
    [DllImport("mpr.dll",CharSet=CharSet.Unicode)]static extern int WNetCancelConnection2(string name,int flags,bool force);
    static int Integration(string[] args)
    {
        if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new InvalidOperationException("Destructive fixture setup is restricted to a disposable GitHub-hosted runner, never a user's PC.");
        Enable();Storage.Initialize();var suffix=Guid.NewGuid().ToString("N")[..7];var a="WuA"+suffix;var b="WuB"+suffix;var nas="WuN"+suffix;var password="Wu!"+Guid.NewGuid().ToString("N");
        var folder=Path.Combine(Path.GetTempPath(),"WinUserFixture-"+suffix);var share1="WU"+suffix+"A";var share2="WU"+suffix+"B";var created=new List<LocalUser>();bool installed=false;
        var nasFile=Path.Combine(Storage.Root,"nas.json");var before=File.Exists(nasFile)?File.ReadAllBytes(nasFile):null;
        try
        {
            if(ServiceController.GetServices().Any(s=>s.ServiceName==Nas.ServiceName))throw new Exception("Refusing to replace a pre-existing service on fixture runner.");
            foreach(var name in new[]{a,b,nas})
            {
                WindowsAccounts.Create(name,password);var user=WindowsAccounts.List().Single(u=>u.Name==name);created.Add(user);
                var usersGroup=((NTAccount)new SecurityIdentifier("S-1-5-32-545").Translate(typeof(NTAccount))).Value.Split('\\').Last();var member=new Member{Name=Environment.MachineName+"\\"+name};var code=NetLocalGroupAddMembers(null,usersGroup,3,ref member,1);if(code is not (0 or 1378))throw new Win32Exception(code);
            }
            Directory.CreateDirectory(folder);var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);
            foreach(var sid in new[]{"S-1-5-18","S-1-5-32-544"}.Concat(created.Select(u=>u.Sid)))acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));new DirectoryInfo(folder).SetAccessControl(acl);
            var privateRoot=Path.Combine(folder,"Private");Directory.CreateDirectory(privateRoot);Storage.SecureDirectory(privateRoot,created[0].Sid);Directory.CreateDirectory(Path.Combine(privateRoot,"deep"));File.WriteAllText(Path.Combine(privateRoot,"deep","data.txt"),"fixture-data");
            using(var big=new FileStream(Path.Combine(privateRoot,"deep","large.bin"),FileMode.CreateNew))big.SetLength(17*1024*1024);
            Check("export rejects destination inside private source",()=>{try{PrivateExport.Preview(privateRoot,Path.Combine(privateRoot,"bad.zip"));throw new Exception("Unsafe destination accepted");}catch(IOException){}});
            var zip=Path.Combine(folder,"shared.zip");PrivateExport.Write(PrivateExport.Preview(privateRoot,zip));var extracted=Path.Combine(folder,"Extracted");ZipFile.ExtractToDirectory(zip,extracted);
            Check("ZIP entry contains no NTFS ACL metadata and preserves contents",()=>{using var z=ZipFile.OpenRead(zip);if(z.Entries.Any(e=>e.ExternalAttributes!=0))throw new Exception("Attributes retained");if(File.ReadAllText(Path.Combine(extracted,"deep","data.txt"))!="fixture-data")throw new Exception("Wrong export");if(new FileInfo(Path.Combine(extracted,"deep","large.bin")).Length!=17*1024*1024)throw new Exception("Large document export truncated");});
            Check("export refuses overwriting existing output",()=>{try{PrivateExport.Preview(privateRoot,zip);throw new Exception("Overwrite accepted");}catch(IOException){}});
            var smb1=Path.Combine(folder,"SMB1");var smb2=Path.Combine(folder,"SMB2");Directory.CreateDirectory(smb1);Directory.CreateDirectory(smb2);
            RunPowerShell($"New-SmbShare -Name '{share1}' -Path '{smb1}' -FullAccess '{Environment.MachineName}\\{nas}' | Out-Null; New-SmbShare -Name '{share2}' -Path '{smb2}' -FullAccess '{Environment.MachineName}\\{nas}' | Out-Null");
            var unc1=@"\\127.0.0.1\"+share1;var unc2=@"\\127.0.0.1\"+share2;var login=Environment.MachineName+"\\"+nas;
            Nas.Save(created[0],"Y:",unc1,login,password,false);Nas.Save(created[0],"Z:",unc2,login,password,false);Nas.Save(created[1],"X:",unc2,login,password,false);
            Check("NAS config keeps password encrypted",()=>{if(File.ReadAllText(nasFile).Contains(password))throw new Exception("Plaintext credential");});
            Check("Add rejects duplicate drive without overwriting",()=>{try{Nas.Save(created[0],"Y:",unc2,login,password,false);throw new Exception("Duplicate accepted");}catch(ArgumentException){}if(Nas.Load().Single(m=>m.Sid==created[0].Sid&&m.Drive=="Y:").Unc!=unc1)throw new Exception("Changed existing mapping");});
            Check("editing can keep encrypted password for same server/account",()=>Nas.Update(created[0],"Z:","Z:",unc2,login,null));
            var payload=Path.GetFullPath(args[1]);RunPowerShell("& '"+Path.Combine(payload,"Install.ps1")+"'");installed=true;
            using(var service=new ServiceController(Nas.ServiceName))service.WaitForStatus(ServiceControllerStatus.Running,TimeSpan.FromSeconds(30));
            RunPowerShell("Start-Service seclogon");
            RunClient(a,password,folder,created[0].Sid,"Y,Z");Check("user A Broker impersonation Agent maps two real SMB drives",()=>RequireReport(folder,a));
            RunClient(b,password,folder,created[1].Sid,"X");Check("user B receives only own mapping and reads exported data, not private data",()=>RequireReport(folder,b));
            Nas.Remove(created[1],"X:");Check("remove deletes config without NAS data deletion",()=>{if(Nas.Load().Any(m=>m.Sid==created[1].Sid)||!Directory.Exists(smb2))throw new Exception("Remove failed");});
            Console.WriteLine($"{passed} integration checks passed on real Windows SMB fixture. TrueNAS and Windows 10 remain separate acceptance tests.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine("FAIL integration: "+e.ToString().Replace(password,"[redacted]"));return 1;}
        finally
        {
            if(installed){try{using var service=new ServiceController(Nas.ServiceName);service.Stop();service.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(30));RunPowerShell("sc.exe delete WinUserBroker | Out-Null; Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run' -Name WinUserNasAgent -ErrorAction SilentlyContinue");}catch{}}
            try{RunPowerShell($"Remove-SmbShare -Name '{share1}' -Force -ErrorAction SilentlyContinue; Remove-SmbShare -Name '{share2}' -Force -ErrorAction SilentlyContinue");}catch{}
            foreach(var user in created){NetUserDel(null,user.Name);}
            if(before!=null)File.WriteAllBytes(nasFile,before);else if(File.Exists(nasFile))File.Delete(nasFile);
            try{Directory.Delete(folder,true);}catch{}
        }
    }
    static void RequireReport(string folder,string user)
    {
        var report=Path.Combine(folder,user+".json");if(!File.Exists(report)||!JsonDocument.Parse(File.ReadAllText(report)).RootElement.GetProperty("ok").GetBoolean())throw new Exception("Client check failed: "+user);
    }
    static void RunPowerShell(string script)
    {
        var info=new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};info.ArgumentList.Add("-NoProfile");info.ArgumentList.Add("-ExecutionPolicy");info.ArgumentList.Add("Bypass");info.ArgumentList.Add("-Command");info.ArgumentList.Add("$ErrorActionPreference='Stop'; "+script);
        using var process=Process.Start(info)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();if(!process.WaitForExit(90000)){process.Kill();throw new Exception("Fixture command timed out");}Task.WaitAll(output,error);if(process.ExitCode!=0)throw new Exception("Fixture command failed: "+error.Result);
    }
    static void RunClient(string name,string password,string folder,string sid,string drives)
    {
        var exe=Environment.ProcessPath!;var startup=new Startup{Size=Marshal.SizeOf<Startup>()};var command=new StringBuilder($"\"{exe}\" --client \"{folder}\" {sid} {drives}");
        if(!CreateProcessWithLogonW(name,Environment.MachineName,password,1,exe,command,0,IntPtr.Zero,Path.GetDirectoryName(exe)!,ref startup,out var pi))throw new Win32Exception();
        CloseHandle(pi.Thread);
        try
        {
            if(WaitForSingleObject(pi.Process,60000)!=0){TerminateProcess(pi.Process,1);throw new Exception("Client timed out");}
            if(!GetExitCodeProcess(pi.Process,out var exit))throw new Win32Exception();
            if(exit!=0){var report=Path.Combine(folder,name+".json");throw new Exception("Client failed: "+(File.Exists(report)?File.ReadAllText(report):"no report"));}
        }
        finally{CloseHandle(pi.Process);}

    }
    static int Client(string[] args)
    {
        var folder=args[1];var sid=args[2];var drives=args[3].Split(',').Select(d=>d+":").ToArray();bool ok=false;string error="";
        using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(25));Task? agent=null;
        try
        {
            if(WindowsAccounts.CurrentSid!=sid||WindowsAccounts.IsAdmin)throw new Exception("Client not the intended non-admin SID");
            var mappings=Nas.FetchMappings(cancel.Token).GetAwaiter().GetResult();if(!mappings.Select(m=>m.Drive).Order().SequenceEqual(drives.Order()))throw new Exception("SID isolation or mapping count wrong");
            agent=Nas.Agent(cancel.Token);var status=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WinUser","nas-status.json");var deadline=DateTime.UtcNow.AddSeconds(20);
            while(!File.Exists(status)&&DateTime.UtcNow<deadline)Thread.Sleep(200);
            if(!File.Exists(status))throw new Exception("Agent produced no status");
            foreach(var drive in drives){var file=drive+"\\client-"+sid.Split('-').Last()+".txt";File.WriteAllText(file,"smb-fixture");if(File.ReadAllText(file)!="smb-fixture")throw new Exception("SMB read/write failed");}
            if(File.ReadAllText(Path.Combine(folder,"Extracted","deep","data.txt"))!="fixture-data")throw new Exception("Exported file not readable");
            if(drives.Length==1){try{File.ReadAllText(Path.Combine(folder,"Private","deep","data.txt"));throw new Exception("Private data leaked to another user");}catch(UnauthorizedAccessException){}}
            else if(File.ReadAllText(Path.Combine(folder,"Private","deep","data.txt"))!="fixture-data")throw new Exception("Owner cannot read private data");
            var first=mappings[0];if(Nas.Connect(first with{Unc=first.Unc+"-other"})!=85)throw new Exception("Drive collision not detected");
            ok=true;return 0;
        }
        catch(Exception e){error=e.GetType().Name+": "+e.Message;return 1;}
        finally
        {
            cancel.Cancel();try{agent?.GetAwaiter().GetResult();}catch(OperationCanceledException){}catch{}
            foreach(var drive in drives)WNetCancelConnection2(drive,0,false);
            File.WriteAllText(Path.Combine(folder,Environment.UserName+".json"),JsonSerializer.Serialize(new{ok,error}));
        }
    }
}
