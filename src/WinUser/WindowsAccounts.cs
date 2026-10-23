using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
namespace WinUser;
public record LocalUser(string Name, string Sid, bool Enabled, bool Rdp, bool LockedOut=false, bool RdpDenied=false, bool RdpMember=false)
{
    public string AccountStatus=>!Enabled?(LockedOut?"Vô hiệu hóa + lockout":"Đã vô hiệu hóa"):LockedOut?"Khóa do nhập sai":"Đang bật";
    public string RdpStatus=>RdpDenied?"Đã chặn trực tiếp":RdpMember?"Trong nhóm RDP":"Không trong nhóm";
}
public record LocalSession(int Id,string User,string Domain,ushort Protocol,int State);
public static class WindowsAccounts
{
    const uint Disabled = 2;
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct User1 { public string Name, Password; public uint PasswordAge, Privilege; public string? Home, Comment; public uint Flags; public string? Script; }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct Member { public string Name; }
    [StructLayout(LayoutKind.Sequential)] struct Session { public int Id; public IntPtr Station; public int State; }
    [DllImport("Netapi32.dll")] static extern int NetApiBufferFree(IntPtr buffer);
    [DllImport("Netapi32.dll", CharSet=CharSet.Unicode)] static extern int NetUserAdd(string? server, int level, ref User1 info, out int error);
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
            var denied=RdpRights.Denied(sid);
            result.Add(new(u.Name,sid,(u.Flags & Disabled)==0,members.Contains(sid) && !denied,(u.Flags & 0x10)!=0,denied,members.Contains(sid)));
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
        AccountUpdates.Flags(user.Name,flags=>enabled?flags & ~Disabled:flags | Disabled);
        if (!enabled) Disconnect(user);
    }
    static void Protect(LocalUser u)
    {
        var reason=AccountRules.ProtectedReason(u.Sid,CurrentSid);
        if(reason!=null)throw new InvalidOperationException("Được bảo vệ: "+reason+".");
    }
    public static void ResetPassword(LocalUser user,string password)
    {
        user=Fresh(user);Protect(user);AccountUpdates.Password(user.Name,password);
    }
    public static void Unlock(LocalUser user)
    {
        user=Fresh(user);Protect(user);AccountUpdates.Flags(user.Name,flags=>flags & ~0x10u);
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
    public static List<LocalSession> Sessions()
    {
        if(!WTSEnumerateSessions(IntPtr.Zero,0,1,out var p,out var count))throw new Win32Exception();
        var result=new List<LocalSession>();
        try
        {
            for(int i=0;i<count;i++)
            {
                var session=Marshal.PtrToStructure<Session>(p+i*Marshal.SizeOf<Session>());
                string Query(int type)
                {
                    if(!WTSQuerySessionInformation(IntPtr.Zero,session.Id,type,out var b,out _))throw new Win32Exception();
                    try{return Marshal.PtrToStringUni(b)??"";}finally{WTSFreeMemory(b);}
                }
                var user=Query(5);if(string.IsNullOrEmpty(user))continue;
                var domain=Query(7);
                if(!WTSQuerySessionInformation(IntPtr.Zero,session.Id,16,out var buffer,out var size))throw new Win32Exception();
                ushort protocol;try{if(size<2)throw new IOException("Windows không trả loại phiên.");protocol=unchecked((ushort)Marshal.ReadInt16(buffer));}finally{WTSFreeMemory(buffer);}
                result.Add(new(session.Id,user,domain,protocol,session.State));
            }
        }
        finally{WTSFreeMemory(p);}
        return result;
    }
    public static int Disconnect(LocalUser user)
    {
        user=Fresh(user);Protect(user);int count=0;
        foreach(var session in Sessions().Where(s=>s.Protocol==2 && s.State!=4 && s.User.Equals(user.Name,StringComparison.OrdinalIgnoreCase) && s.Domain.Equals(Environment.MachineName,StringComparison.OrdinalIgnoreCase)))
        {
            if(!WTSDisconnectSession(IntPtr.Zero,session.Id,true))throw new Win32Exception();
            count++;
        }
        return count;
    }
}
