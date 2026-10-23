using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
namespace WinUser;
internal static class AgentLauncher
{
    [StructLayout(LayoutKind.Sequential)]struct Session{public int Id;public IntPtr Name;public int State;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Startup{public int Size;public string? Reserved,Desktop,Title;public int X,Y,Width,Height,CharsX,CharsY,Fill,Flags;public short Show,ReservedBytes;public IntPtr ReservedPointer,In,Out,Error;}
    [StructLayout(LayoutKind.Sequential)]struct ProcessInfo{public IntPtr Process,Thread;public int Id,ThreadId;}
    [DllImport("wtsapi32.dll",SetLastError=true)]static extern bool WTSEnumerateSessions(IntPtr server,int reserved,int version,out IntPtr sessions,out int count);
    [DllImport("wtsapi32.dll")]static extern void WTSFreeMemory(IntPtr pointer);
    [DllImport("wtsapi32.dll",SetLastError=true)]static extern bool WTSQueryUserToken(uint session,out IntPtr token);
    [DllImport("userenv.dll",SetLastError=true)]static extern bool CreateEnvironmentBlock(out IntPtr environment,IntPtr token,bool inherit);
    [DllImport("userenv.dll")]static extern bool DestroyEnvironmentBlock(IntPtr environment);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateProcessAsUser(IntPtr token,string application,StringBuilder command,IntPtr processSecurity,IntPtr threadSecurity,bool inherit,uint flags,IntPtr environment,string directory,ref Startup startup,out ProcessInfo process);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
    public static void Run(CancellationToken stop)
    {
        var running=new Dictionary<int,Process>();var attempts=new Dictionary<int,DateTime>();
        while(!stop.IsCancellationRequested)
        {
            try
            {
                var eligible=Nas.Load().Select(m=>m.Sid).ToHashSet();eligible.IntersectWith(WindowsAccounts.List().Where(u=>u.Enabled).Select(u=>u.Sid));
                if(eligible.Count>0&&WTSEnumerateSessions(IntPtr.Zero,0,1,out var buffer,out var count))
                {
                    try
                    {
                        for(int i=0;i<count;i++)
                        {
                            var s=Marshal.PtrToStructure<Session>(buffer+i*Marshal.SizeOf<Session>());if(s.State is not (0 or 1))continue;
                            if(running.TryGetValue(s.Id,out var p)&&!p.HasExited)continue;
                            if(attempts.TryGetValue(s.Id,out var last)&&DateTime.UtcNow-last<TimeSpan.FromMinutes(2))continue;
                            if(!WTSQueryUserToken((uint)s.Id,out var token))continue;
                            try
                            {
                                using var identity=new WindowsIdentity(token);var sid=identity.User?.Value;if(sid==null||!eligible.Contains(sid))continue;
                                attempts[s.Id]=DateTime.UtcNow;var exe=Environment.ProcessPath!;
                                if(!Path.GetFullPath(exe).StartsWith(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WinUser")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Agent phải chạy từ bản cài Program Files\\WinUser.");
                                if(!CreateEnvironmentBlock(out var environment,token,false))throw new System.ComponentModel.Win32Exception();
                                try
                                {
                                    var startup=new Startup{Size=Marshal.SizeOf<Startup>(),Desktop=@"winsta0\default"};
                                    if(!CreateProcessAsUser(token,exe,new StringBuilder("\""+exe+"\" --agent"),IntPtr.Zero,IntPtr.Zero,false,0x400|0x08000000,environment,Path.GetDirectoryName(exe)!,ref startup,out var pi))throw new System.ComponentModel.Win32Exception();
                                    CloseHandle(pi.Thread);CloseHandle(pi.Process);if(running.Remove(s.Id,out var old))old.Dispose();running[s.Id]=Process.GetProcessById(pi.Id);Storage.Log("AgentLaunch",sid,true);
                                }
                                finally{DestroyEnvironmentBlock(environment);}
                            }
                            finally{CloseHandle(token);}
                        }
                    }
                    finally{WTSFreeMemory(buffer);}
                }
            }
            catch(Exception e){try{Storage.Log("AgentLaunch","service",false,e is System.ComponentModel.Win32Exception w?w.NativeErrorCode:e.HResult);}catch{}}
            stop.WaitHandle.WaitOne(TimeSpan.FromSeconds(15));
        }
        foreach(var p in running.Values)p.Dispose(); // Do not terminate user processes.
    }
}
