using System.ComponentModel;
using System.Runtime.InteropServices;
namespace WinUser;
internal static class Privileges
{
    [StructLayout(LayoutKind.Sequential)] struct Luid {public uint Low;public int High;}
    [StructLayout(LayoutKind.Sequential)] struct TokenPrivileges {public uint Count;public Luid Luid;public uint Attributes;}
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool LookupPrivilegeValue(string? system,string name,out Luid luid);
    [DllImport("advapi32.dll",SetLastError=true)] static extern bool AdjustTokenPrivileges(IntPtr token,bool disableAll,ref TokenPrivileges state,int length,IntPtr previous,IntPtr returned);
    public static void EnableAdministration()
    {
        if(!OpenProcessToken(GetCurrentProcess(),0x28,out var token))throw new Win32Exception();
        try
        {
            foreach(var name in new[]{"SeBackupPrivilege","SeRestorePrivilege"})
            {
                if(!LookupPrivilegeValue(null,name,out var luid))throw new Win32Exception();
                var state=new TokenPrivileges{Count=1,Luid=luid,Attributes=2};
                if(!AdjustTokenPrivileges(token,false,ref state,0,IntPtr.Zero,IntPtr.Zero))throw new Win32Exception();
                var error=Marshal.GetLastWin32Error();if(error!=0)throw new Win32Exception(error,"Không bật được quyền "+name);
            }
        }
        finally{CloseHandle(token);}
    }
}
