using System.ComponentModel;
using System.Runtime.InteropServices;
namespace WinUser;
internal static class AccountUpdates
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct User1 {public string Name;public string? Password;public uint PasswordAge,Privilege;public string? Home,Comment;public uint Flags;public string? Script;}
    [StructLayout(LayoutKind.Sequential)] struct FlagsInfo {public uint Flags;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct PasswordInfo {public string Password;}
    [DllImport("Netapi32.dll",CharSet=CharSet.Unicode)] static extern int NetUserGetInfo(string? server,string name,int level,out IntPtr buffer);
    [DllImport("Netapi32.dll")] static extern int NetApiBufferFree(IntPtr buffer);
    [DllImport("Netapi32.dll",CharSet=CharSet.Unicode,EntryPoint="NetUserSetInfo")] static extern int SetFlags(string? server,string name,int level,ref FlagsInfo info,out int error);
    [DllImport("Netapi32.dll",CharSet=CharSet.Unicode,EntryPoint="NetUserSetInfo")] static extern int SetPassword(string? server,string name,int level,ref PasswordInfo info,out int error);
    static void Check(int code,string operation){if(code!=0)throw new Win32Exception(code,operation+": "+new Win32Exception(code).Message+" (mã Windows "+code+")");}
    public static void Password(string name,string password)
    {
        AccountRules.Password(password,password);var info=new PasswordInfo{Password=password};Check(SetPassword(null,name,1003,ref info,out _),"NetUserSetInfo(password)");
    }
    public static void Flags(string name,Func<uint,uint> change)
    {
        var code=NetUserGetInfo(null,name,1,out var p);
        try{Check(code,"NetUserGetInfo(flags)");var old=Marshal.PtrToStructure<User1>(p);var info=new FlagsInfo{Flags=change(old.Flags)};Check(SetFlags(null,name,1008,ref info,out _),"NetUserSetInfo(flags)");}
        finally{if(p!=IntPtr.Zero)NetApiBufferFree(p);}
    }
}
