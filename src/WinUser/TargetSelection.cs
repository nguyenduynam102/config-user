namespace WinUser;
// One source of truth for operation targets, independent of highlighting/filtering.
public sealed class TargetSelection
{
    readonly HashSet<string> selected=new(StringComparer.OrdinalIgnoreCase);
    public int Count=>selected.Count;
    public bool Contains(string sid)=>selected.Contains(sid);
    public void Set(string sid,bool value){if(value)selected.Add(sid);else selected.Remove(sid);}
    public void Clear()=>selected.Clear();
    public void Retain(IEnumerable<string> existing)=>selected.IntersectWith(existing);
    public IReadOnlyList<T> Resolve<T>(IEnumerable<T> all,Func<T,string> sid)=>all.Where(x=>Contains(sid(x))).ToList();
}
public static class AccountRules
{
    public static string? ProtectedReason(string sid,string currentSid)
    {
        if(sid.Equals(currentSid,StringComparison.OrdinalIgnoreCase))return "Tài khoản quản trị đang sử dụng";
        if(new[]{"-500","-501","-503","-504"}.Any(r=>sid.EndsWith(r,StringComparison.Ordinal)))return "Tài khoản Windows tích hợp";
        return null;
    }
    public static void Password(string password,string confirmation)
    {
        if(string.IsNullOrWhiteSpace(password))throw new ArgumentException("Mật khẩu không được trống.");
        if(password!=confirmation)throw new ArgumentException("Hai lần nhập mật khẩu không khớp.");
    }
}
