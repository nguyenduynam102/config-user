using System.ComponentModel;
using System.Runtime.InteropServices;
namespace WinUser;
public sealed record AccountDetails(string FullName,string Description,uint Flags,string[] Groups);
public static partial class WindowsAccounts
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct User10 {public string Name,Comment,UserComment,FullName;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct TextInfo {public string Value;}
    [DllImport("Netapi32.dll",CharSet=CharSet.Unicode,EntryPoint="NetUserSetInfo")] static extern int SetText(string? server,string name,int level,ref TextInfo info,out int error);
    [DllImport("Netapi32.dll",CharSet=CharSet.Unicode)] static extern int NetUserDel(string? server,string name);
    [DllImport("Netapi32.dll",CharSet=CharSet.Unicode)] static extern int NetUserGetLocalGroups(string? server,string name,int level,int flags,out IntPtr buffer,int max,out int count,out int total);
    public static AccountDetails Details(LocalUser user)
    {
        user=Fresh(user);Check(NetUserGetInfo(null,user.Name,10,out var p));string full,comment;
        try {var v=Marshal.PtrToStructure<User10>(p);full=v.FullName??"";comment=v.Comment??"";}finally{NetApiBufferFree(p);}
        Check(NetUserGetInfo(null,user.Name,1,out p));uint flags;try{flags=Marshal.PtrToStructure<User1>(p).Flags;}finally{NetApiBufferFree(p);}
        Check(NetUserGetLocalGroups(null,user.Name,0,1,out p,-1,out var count,out _));
        try{return new(full,comment,flags,Enumerable.Range(0,count).Select(i=>Marshal.PtrToStringUni(Marshal.ReadIntPtr(p,i*IntPtr.Size))??"").ToArray());}finally{NetApiBufferFree(p);}
    }
    static void ChangeText(LocalUser user,int level,string value){var info=new TextInfo{Value=value};Check(SetText(null,user.Name,level,ref info,out _));}
    public static void Rename(LocalUser user,string name){user=Fresh(user);Protect(user);ChangeText(user,0,Policy.UserName(name));}
    public static void Password(LocalUser user,string password){user=Fresh(user);Protect(user);if(string.IsNullOrWhiteSpace(password))throw new ArgumentException("Mật khẩu không được trống.");ChangeText(user,1003,password);}
    public static void Edit(LocalUser user,string fullName,string description,bool neverExpires)
    {
        user=Fresh(user);Protect(user);ChangeText(user,1011,fullName);ChangeText(user,1007,description);
        SetFlags(user,flags=>neverExpires?flags|0x10000:flags&~0x10000u);
    }
    static void SetFlags(LocalUser user,Func<uint,uint> change)
    {
        Check(NetUserGetInfo(null,user.Name,1,out var p));try{var info=new FlagsInfo{Flags=change(Marshal.PtrToStructure<User1>(p).Flags)};Check(NetUserSetInfo(null,user.Name,1008,ref info,out _));}finally{NetApiBufferFree(p);}
    }
    public static void Unlock(LocalUser user){user=Fresh(user);Protect(user);SetFlags(user,f=>f&~0x10u);}
    public static void Delete(LocalUser user)
    {
        user=Fresh(user);Protect(user);if(SessionSummary(user.Name)!="Chưa đăng nhập")throw new InvalidOperationException("User còn phiên đăng nhập. Hãy đăng xuất từ phiên của user trước khi xóa.");
        // Revoke broker mappings first; a partially failed deletion never leaves reusable credentials.
        foreach(var m in Nas.Load().Where(m=>m.Sid==user.Sid).ToArray())Nas.Remove(user,m.Drive);
        Check(NetUserDel(null,user.Name));
    }
    public static void Membership(LocalUser user,string group,bool add)
    {
        user=Fresh(user);Protect(user);if(string.IsNullOrWhiteSpace(group))throw new ArgumentException("Nhập tên nhóm cục bộ.");
        // Privileged membership is deliberately delegated to Windows' native confirmation UI.
        if(group.Equals(Group("S-1-5-32-544"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Thay đổi Administrators bằng lusrmgr.msc để kiểm tra quyền quản trị trực tiếp.");
        var member=new Member{Name=Environment.MachineName+"\\"+user.Name};var code=add?NetLocalGroupAddMembers(null,group,3,ref member,1):NetLocalGroupDelMembers(null,group,3,ref member,1);if(code is not (1378 or 1377))Check(code);
    }
    public static string SessionSummary(string name)
    {
        if(!WTSEnumerateSessions(IntPtr.Zero,0,1,out var p,out var count))throw new Win32Exception();
        var states=new List<string>();try{for(int i=0;i<count;i++){var s=Marshal.PtrToStructure<Session>(p+i*Marshal.SizeOf<Session>());if(!WTSQuerySessionInformation(IntPtr.Zero,s.Id,5,out var b,out _))continue;string n;try{n=Marshal.PtrToStringUni(b)??"";}finally{WTSFreeMemory(b);}if(!n.Equals(name,StringComparison.OrdinalIgnoreCase))continue;if(!WTSQuerySessionInformation(IntPtr.Zero,s.Id,7,out b,out _))continue;string domain;try{domain=Marshal.PtrToStringUni(b)??"";}finally{WTSFreeMemory(b);}if(domain.Equals(Environment.MachineName,StringComparison.OrdinalIgnoreCase))states.Add(s.State==0?"Đang hoạt động":s.State==4?"Đã ngắt phiên":"Phiên "+s.Id+" ("+s.State+")");}}finally{WTSFreeMemory(p);}return states.Count==0?"Chưa đăng nhập":string.Join(", ",states);
    }
}
