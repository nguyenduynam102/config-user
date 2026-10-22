using System.ComponentModel;
using System.Runtime.InteropServices;
namespace WinUser;
internal record AccountInfo(string Name, uint Flags, byte[] Sid);
internal static class AccountEnumeration
{
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    struct User23 { public string Name, FullName, Comment; public uint Flags; public IntPtr Sid; }
    [DllImport("Netapi32.dll", CharSet=CharSet.Unicode)]
    static extern int NetUserEnum(string? server, uint level, uint filter, out IntPtr buffer, uint max, out uint count, out uint total, ref uint resume);
    [DllImport("Netapi32.dll", CharSet=CharSet.Unicode)]
    static extern int NetUserGetInfo(string? server, string name, uint level, out IntPtr buffer);
    [DllImport("Netapi32.dll")] static extern int NetApiBufferFree(IntPtr buffer);
    static void Check(int code, string operation)
    {
        if (code != 0) throw new Win32Exception(code, operation+": "+new Win32Exception(code).Message+" (mã Windows "+code+")");
    }
    public static List<AccountInfo> Read()
    {
        var result=new List<AccountInfo>();uint resume=0;int status;
        do
        {
            // NetUserEnum accepts levels 0/1/2/3/10/11/20, not 23.
            // Enumerate USER_INFO_0 names, then ask NetUserGetInfo for USER_INFO_23.
            status=NetUserEnum(null,0,2,out var names,uint.MaxValue,out var count,out _,ref resume);
            try
            {
                if(status is not (0 or 234))Check(status,"NetUserEnum(level=0)");
                for(uint i=0;i<count;i++)
                {
                    var name=Marshal.PtrToStringUni(Marshal.ReadIntPtr(names,checked((int)i*IntPtr.Size)))??throw new IOException("NetUserEnum trả tên rỗng.");
                    var infoStatus=NetUserGetInfo(null,name,23,out var info);
                    try
                    {
                        if(infoStatus==2221)continue; // Account deleted after enumeration.
                        Check(infoStatus,"NetUserGetInfo(level=23)");
                        var user=Marshal.PtrToStructure<User23>(info);
                        if(user.Sid==IntPtr.Zero)throw new IOException("Windows không trả SID tài khoản.");
                        var subAuthorities=Marshal.ReadByte(user.Sid,1);
                        if(subAuthorities>15)throw new IOException("SID tài khoản không hợp lệ.");
                        var sid=new byte[8+4*subAuthorities];Marshal.Copy(user.Sid,sid,0,sid.Length);
                        result.Add(new(user.Name,user.Flags,sid));
                    }
                    finally {if(info!=IntPtr.Zero)NetApiBufferFree(info);}
                }
            }
            finally {if(names!=IntPtr.Zero)NetApiBufferFree(names);}
        } while(status==234);
        return result;
    }
}
