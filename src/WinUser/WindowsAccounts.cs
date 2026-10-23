using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
namespace WinUser;
public record LocalUser(string Name, string Sid, bool Enabled, bool Rdp);
public static class WindowsAccounts
{
    const uint Disabled = 2;
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct User1 { public string Name, Password; public uint PasswordAge, Privilege; public string? Home, Comment; public uint Flags; public string? Script; }
    [StructLayout(LayoutKind.Sequential)] struct FlagsInfo { public uint Flags; }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct Member { public string Name; }
    [StructLayout(LayoutKind.Sequential)] struct Session { public int Id; public IntPtr Station; public int State; }
    [DllImport("Netapi32.dll")] static extern int NetApiBufferFree(IntPtr buffer);
    [DllImport("Netapi32.dll", CharSet=CharSet.Unicode)] static extern int NetUserAdd(string? server, int level, ref User1 info, out int error);
    [DllImport("Netapi32.dll", CharSet=CharSet.Unicode)] static extern int NetUserSetInfo(string? server, string name, int level, ref FlagsInfo info, out int error);
    [DllImport("Netapi32.dll", CharSet=CharSet.Unicode)] static extern int NetLocalGroupAddMembers(string? server, string group, int level, ref Member member, int count);
    [DllImport("Netapi32.dll", CharSet=CharSet.Unicode)] static extern int NetLocalGroupDelMembers(string? server, string group, int level, ref Member member, int count);
    [DllImport("Netapi32.dll", CharSet=CharSet.Unicode)] static extern int NetLocalGroupGetMembers(string? server, string group, int level, out IntPtr buffer, int max, out int count, out int total, ref nuint resume);
    [DllImport("wtsapi32.dll", SetLastError=true)] static extern bool WTSEnumerateSessions(IntPtr server, int reserved, int version, out IntPtr sessions, out int count);
    [DllImport("wtsapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool WTSQuerySessionInformation(IntPtr server, int session, int info, out IntPtr buffer, out int size);
    [DllImport("wtsapi32.dll")] static extern void WTSFreeMemory(IntPtr memory);
    [DllImport("wtsapi32.dll", SetLastError=true)] static extern bool WTSDisconnectSession(IntPtr server, int session, bool wait);
    static string Group(string sid) => ((NTAccount)new SecurityIdentifier(sid).Translate(typeof(NTAccount))).Value.Split('\\').Last();
    public static string CurrentSid => WindowsIdentity.GetCurrent().User!.Value;
    public static bool IsAdmin => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    static void Check(int code) { if (code != 0) throw new Win32Exception(code); }
    public static List<LocalUser> List()
    {
        var members = new HashSet<string>(); nuint resume = 0; int status;
        do
        {
            status = NetLocalGroupGetMembers(null, Group("S-1-5-32-555"), 0, out var p, -1, out var n, out _, ref resume);
            try { if (status is not (0 or 234)) Check(status); for (int i=0;i<n;i++) members.Add(new SecurityIdentifier(Marshal.ReadIntPtr(p,i*IntPtr.Size)).Value); }
            finally { if (p != IntPtr.Zero) NetApiBufferFree(p); }
        } while (status == 234);
        var result = new List<LocalUser>();
        foreach(var u in AccountEnumeration.Read())
        {
            var sid=new SecurityIdentifier(u.Sid,0).Value;
            result.Add(new(u.Name,sid,(u.Flags & Disabled)==0,members.Contains(sid) && !RdpRights.Denied(sid)));
        }
        return result.OrderBy(u=>u.Name).ToList();
    }
    public static LocalUser Fresh(LocalUser user) => List().Single(u => u.Sid == user.Sid && u.Name == user.Name);
    public static void Create(string name, string password)
    {
        name=Policy.UserName(name);
        if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Mật khẩu không được trống.");
        var u=new User1 { Name=name, Password=password, Privilege=1, Flags=0x201 };
        Check(NetUserAdd(null,1,ref u,out _));
    }
    public static void Enabled(LocalUser user, bool enabled)
    {
        user=Fresh(user);
        Protect(user);
        // Read level 1 to preserve all existing account flags.
        Check(NetUserGetInfo(null,user.Name,1,out var p));
        try { var old=Marshal.PtrToStructure<User1>(p); var flags=new FlagsInfo { Flags=enabled ? old.Flags & ~Disabled : old.Flags | Disabled }; Check(NetUserSetInfo(null,user.Name,1008,ref flags,out _)); }
        finally { NetApiBufferFree(p); }
        if (!enabled) Disconnect(user);
    }
    [DllImport("Netapi32.dll", CharSet=CharSet.Unicode)] static extern int NetUserGetInfo(string? server,string name,int level,out IntPtr buffer);
    static void Protect(LocalUser u)
    {
        if (u.Sid == CurrentSid || u.Sid.EndsWith("-500",StringComparison.Ordinal) || u.Sid.EndsWith("-501",StringComparison.Ordinal) || u.Sid.EndsWith("-503",StringComparison.Ordinal) || u.Sid.EndsWith("-504",StringComparison.Ordinal))
            throw new InvalidOperationException("Bảo vệ tài khoản hiện tại hoặc tài khoản Windows tích hợp.");
    }
    public static void Rdp(LocalUser user,bool enabled)
    {
        user=Fresh(user); Protect(user);
        if (!enabled) RdpRights.SetDenied(user.Sid,true);
        var member=new Member { Name=Environment.MachineName+"\\"+user.Name };
        var code=enabled ? NetLocalGroupAddMembers(null,Group("S-1-5-32-555"),3,ref member,1) : NetLocalGroupDelMembers(null,Group("S-1-5-32-555"),3,ref member,1);
        if (code is not (1378 or 1377)) Check(code);
        if (enabled) RdpRights.SetDenied(user.Sid,false);
    }
    public static void Disconnect(LocalUser user)
    {
        if (!WTSEnumerateSessions(IntPtr.Zero,0,1,out var p,out var count)) throw new Win32Exception();
        try
        {
            for(int i=0;i<count;i++)
            {
                var s=Marshal.PtrToStructure<Session>(p+i*Marshal.SizeOf<Session>());
                string Query(int type) { if(!WTSQuerySessionInformation(IntPtr.Zero,s.Id,type,out var b,out _)) throw new Win32Exception(); try { return Marshal.PtrToStringUni(b)??""; } finally { WTSFreeMemory(b); } }
                bool IsRdp()
                {
                    if(!WTSQuerySessionInformation(IntPtr.Zero,s.Id,16,out var b,out var size))throw new Win32Exception();
                    try { return size>=2 && Marshal.ReadInt16(b)==2; } finally { WTSFreeMemory(b); }
                }
                if (IsRdp() && string.Equals(Query(5),user.Name,StringComparison.OrdinalIgnoreCase) && string.Equals(Query(7),Environment.MachineName,StringComparison.OrdinalIgnoreCase))
                    if(!WTSDisconnectSession(IntPtr.Zero,s.Id,true)) throw new Win32Exception();
            }
        }
        finally { WTSFreeMemory(p); }
    }
}
