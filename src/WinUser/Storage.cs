using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace WinUser;
public static class Storage
{
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"WinUser");
    public static readonly JsonSerializerOptions Json = new() { WriteIndented=true };
    public static void Initialize()
    {
        Directory.CreateDirectory(Root);
        SecureDirectory(Root, null);
        Directory.CreateDirectory(Path.Combine(Root,"Backups"));
    }
    public static void SecureDirectory(string path,string? owner)
    {
        NoLinks(path);
        var security=new DirectorySecurity(); security.SetAccessRuleProtection(true,false);
        foreach(var sid in new[]{"S-1-5-18","S-1-5-32-544"}.Concat(owner is null?Array.Empty<string>():new[]{owner}))
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        security.SetOwner(new SecurityIdentifier(owner??"S-1-5-32-544"));
        new DirectoryInfo(path).SetAccessControl(security);
    }
    public static void NoLinks(string path)
    {
        for(var p=new DirectoryInfo(Path.GetFullPath(path));p!=null;p=p.Parent)
            if(p.Exists && (p.Attributes & FileAttributes.ReparsePoint)!=0) throw new IOException("Từ chối đường dẫn reparse point: "+p.FullName);
    }
    public static void Write<T>(string path,T value)
    {
        NoLinks(Path.GetDirectoryName(path)!);
        if(File.Exists(path) && (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0) throw new IOException("Tệp là liên kết.");
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try { File.WriteAllText(temp,JsonSerializer.Serialize(value,Json),Encoding.UTF8); File.Move(temp,path,true); }
        finally { if(File.Exists(temp))File.Delete(temp); }
    }
    public static T Read<T>(string path,T fallback) => File.Exists(path)?JsonSerializer.Deserialize<T>(File.ReadAllText(path))??fallback:fallback;
    public static string BackupFolder(string kind,string sid)
    {
        var p=Path.Combine(Root,"Backups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+"-"+kind+"-"+sid);Directory.CreateDirectory(p);return p;
    }
    public static void Log(string action,string target,bool ok,int? code=null)
    {
        // Deliberately exclude exception messages, paths and secret-bearing payloads.
        File.AppendAllText(Path.Combine(Root,"audit.jsonl"),JsonSerializer.Serialize(new{utc=DateTime.UtcNow,actor=WindowsAccounts.CurrentSid,action,target,ok,code})+Environment.NewLine);
    }
    public static string Encrypt(string password) => Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(password),null,DataProtectionScope.LocalMachine));
    public static string Decrypt(string ciphertext) => Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ciphertext),null,DataProtectionScope.LocalMachine));
}
