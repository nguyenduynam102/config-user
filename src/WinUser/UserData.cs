using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
namespace WinUser;
public sealed class UserHive : IDisposable
{
    readonly string mount; readonly bool temporary;
    public RegistryKey Root { get; }
    public UserHive(LocalUser user)
    {
        WindowsAccounts.Fresh(user);
        var loaded=Registry.Users.OpenSubKey(user.Sid,true);
        if(loaded!=null) { mount=user.Sid; Root=loaded; return; }
        var profile=UserData.Profile(user);
        mount="WinUser_"+Guid.NewGuid().ToString("N");
        RunReg("load",@"HKU\"+mount,Path.Combine(profile,"NTUSER.DAT"));
        temporary=true;
        Root=Registry.Users.OpenSubKey(mount,true)??throw new IOException("Không mở được hive.");
    }
    public static void RunReg(params string[] args)
    {
        var si=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"reg.exe")) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
        foreach(var a in args)si.ArgumentList.Add(a);
        using var p=Process.Start(si)!;
        var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
        if(!p.WaitForExit(60000)) { p.Kill(); throw new IOException("Registry hết thời gian chờ."); }
        Task.WaitAll(output,error);
        if(p.ExitCode!=0)throw new IOException("reg.exe thất bại, mã "+p.ExitCode+". Hive có thể đang được sử dụng.");
    }
    public void Dispose() { Root.Dispose(); if(temporary)RunReg("unload",@"HKU\"+mount); }
}
public record ValueBackup(string Key,string Name,bool KeyExisted,bool Existed,RegistryValueKind Kind,JsonElement Value);
public record DesktopBackup(string Sid,string Desktop,string[] Added,string[] Removed);
public static class UserData
{
    [DllImport("userenv.dll",CharSet=CharSet.Unicode)] static extern int CreateProfile(string sid,string name,StringBuilder path,uint length);
    public static string Profile(LocalUser user)
    {
        using var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\"+user.Sid);
        var path=key?.GetValue("ProfileImagePath") as string;
        if(path==null)
        {
            var buffer=new StringBuilder(1024); var result=CreateProfile(user.Sid,user.Name,buffer,1024);
            if(result<0)Marshal.ThrowExceptionForHR(result);
            path=buffer.ToString();
        }
        path=Environment.ExpandEnvironmentVariables(path); Storage.NoLinks(path); return path;
    }
    public static string Desktop(LocalUser user)
    {
        var profile=Profile(user); using var hive=new UserHive(user);
        using var folders=hive.Root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders");
        var raw=folders?.GetValue("Desktop",null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string??@"%USERPROFILE%\Desktop";
        var path=raw.Replace("%USERPROFILE%",profile,StringComparison.OrdinalIgnoreCase);
        // Avoid administrator environment expansion accidentally targeting its desktop.
        if(path.Contains('%') || !Path.GetFullPath(path).StartsWith(Path.GetFullPath(profile)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            throw new IOException("Desktop chuyển hướng ra ngoài profile chưa được hỗ trợ.");
        Storage.NoLinks(path);Directory.CreateDirectory(path);return path;
    }
    public static string[] Shortcuts(LocalUser user) => Directory.GetFiles(Desktop(user)).Where(p=>new[]{".lnk",".url"}.Contains(Path.GetExtension(p),StringComparer.OrdinalIgnoreCase)).Order().ToArray();
    public static string Sync(LocalUser source,LocalUser target,string[] names,bool remove)
    {
        var from=Desktop(source); var to=Desktop(target);
        if(source.Sid==target.Sid)throw new ArgumentException("Nguồn và đích phải khác nhau.");
        var backup=Storage.BackupFolder("Desktop",target.Sid);var added=new List<string>();var removed=new List<string>();
        foreach(var n in names)
        {
            Policy.Shortcut(n); var dest=Path.Combine(to,n);Storage.NoLinks(dest);
            if(remove) { if(File.Exists(dest)) {SafeFile.Copy(dest,Path.Combine(backup,n));removed.Add(n);} }
            else if(!File.Exists(dest)) { var src=Path.Combine(from,n);Storage.NoLinks(src);if(!File.Exists(src))throw new FileNotFoundException("Shortcut nguồn không còn tồn tại."); added.Add(n); }
        }
        Storage.Write(Path.Combine(backup,"desktop.json"),new DesktopBackup(target.Sid,to,added.ToArray(),removed.ToArray()));
        foreach(var n in removed)SafeFile.Delete(Path.Combine(to,n));
        foreach(var n in added)SafeFile.Copy(Path.Combine(from,n),Path.Combine(to,n),false);
        return backup;
    }
    public static string ApplyRegistry(LocalUser user,string file)
    {
        var changes=Policy.ParseRegistry(File.ReadAllText(file));using var hive=new UserHive(user);
        var folder=SaveRegistryBackup(user,hive,changes);
        foreach(var c in changes) {using var k=hive.Root.CreateSubKey(c.Key,true);if(c.Number.HasValue)k.SetValue(c.Name,c.Number.Value,RegistryValueKind.DWord);else k.SetValue(c.Name,c.Text!,RegistryValueKind.String);}
        return folder;
    }
    static string SaveRegistryBackup(LocalUser user,UserHive hive,IReadOnlyList<RegChange> changes)
    {
        var backup=new List<ValueBackup>();
        foreach(var c in changes)
        {
            using var k=hive.Root.OpenSubKey(c.Key);var exists=k?.GetValueNames().Contains(c.Name,StringComparer.OrdinalIgnoreCase)==true;
            var kind=exists?k!.GetValueKind(c.Name):RegistryValueKind.None;
            var old=exists?k!.GetValue(c.Name,null,RegistryValueOptions.DoNotExpandEnvironmentNames):null;
            backup.Add(new(c.Key,c.Name,k!=null,exists,kind,JsonSerializer.SerializeToElement(old)));
        }
        var folder=Storage.BackupFolder("Registry",user.Sid);
        Storage.Write(Path.Combine(folder,"registry.json"),backup);File.WriteAllText(Path.Combine(folder,"sid.txt"),user.Sid);
        return folder;
    }
    public static void Restore(string folder)
    {
        Storage.NoLinks(folder);
        if(!Path.GetFullPath(folder).StartsWith(Path.Combine(Storage.Root,"Backups")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Chọn bản sao lưu trong thư mục WinUser/Backups.");
        if(File.Exists(Path.Combine(folder,"desktop.json")))
        {
            var b=Storage.Read<DesktopBackup?>(Path.Combine(folder,"desktop.json"),null)!;
            var user=WindowsAccounts.List().Single(u=>u.Sid==b.Sid);var dest=Desktop(user);
            if(!string.Equals(dest,b.Desktop,StringComparison.OrdinalIgnoreCase))throw new IOException("Desktop đã chuyển vị trí.");
            // Preserve the current state before restoring too.
            var before=Storage.BackupFolder("BeforeRestore",b.Sid);
            var all=b.Added.Concat(b.Removed).Distinct().ToArray();
            foreach(var n in all) {Policy.Shortcut(n);Storage.NoLinks(Path.Combine(dest,n));if(File.Exists(Path.Combine(dest,n)))SafeFile.Copy(Path.Combine(dest,n),Path.Combine(before,n));}
            Storage.Write(Path.Combine(before,"desktop.json"),new DesktopBackup(b.Sid,dest,b.Removed.Where(n=>!File.Exists(Path.Combine(dest,n))).ToArray(),all.Where(n=>File.Exists(Path.Combine(dest,n))).ToArray()));
            foreach(var n in b.Added)SafeFile.Delete(Path.Combine(dest,n));
            foreach(var n in b.Removed)SafeFile.Copy(Path.Combine(folder,n),Path.Combine(dest,n),true);
        }
        else if(File.Exists(Path.Combine(folder,"nas.json")))
        {
            var list=Storage.Read(Path.Combine(folder,"nas.json"),new List<NasMapping>());
            var users=WindowsAccounts.List();
            foreach(var m in list){if(!users.Any(u=>u.Sid==m.Sid))throw new IOException("Bản sao chứa SID không còn tồn tại.");Policy.Drive(m.Drive);Policy.Unc(m.Unc);Storage.Decrypt(m.ProtectedPassword);}
            var dest=Path.Combine(Storage.Root,"nas.json");
            if(File.Exists(dest))SafeFile.Copy(dest,Path.Combine(Storage.BackupFolder("NAS","config"),"nas.json"));
            Storage.Write(dest,list);
        }
        else if(File.Exists(Path.Combine(folder,"preset.json")))
        {
            var p=Storage.Read<Preset?>(Path.Combine(folder,"preset.json"),null)??throw new IOException("Bản sao cấu hình không hợp lệ.");
            var dest=Presets.PathOf(p.Id);
            if(Directory.Exists(dest)){var before=Storage.BackupFolder("Preset",p.Id);foreach(var file in Directory.GetFiles(dest))SafeFile.Copy(file,Path.Combine(before,Path.GetFileName(file)));}
            Directory.CreateDirectory(dest);
            foreach(var n in p.Shortcuts){Policy.Shortcut(n);SafeFile.Copy(Path.Combine(folder,n),Path.Combine(dest,n),true);}
            if(p.HasRegistry){Policy.ParseRegistry(File.ReadAllText(Path.Combine(folder,"settings.reg")));SafeFile.Copy(Path.Combine(folder,"settings.reg"),Path.Combine(dest,"settings.reg"),true);}
            Storage.Write(Path.Combine(dest,"preset.json"),p);
        }
        else
        {
            var user=WindowsAccounts.List().Single(u=>u.Sid==File.ReadAllText(Path.Combine(folder,"sid.txt")));
            var backup=Storage.Read(Path.Combine(folder,"registry.json"),new List<ValueBackup>());using var hive=new UserHive(user);
            SaveRegistryBackup(user,hive,backup.Select(b=>new RegChange(b.Key,b.Name,null,null)).ToArray());
            foreach(var b in backup.AsEnumerable().Reverse())
            {
                using var k=hive.Root.CreateSubKey(b.Key,true);
                if(!b.Existed) {k.DeleteValue(b.Name,false);continue;}
                object value=b.Kind switch { RegistryValueKind.DWord=>b.Value.GetInt32(),RegistryValueKind.QWord=>b.Value.GetInt64(),RegistryValueKind.Binary or RegistryValueKind.None=>b.Value.GetBytesFromBase64(),RegistryValueKind.MultiString=>b.Value.Deserialize<string[]>()!,_=>b.Value.GetString()! };
                k.SetValue(b.Name,value,b.Kind);
            }
            foreach(var b in backup.Where(x=>!x.KeyExisted).OrderByDescending(x=>x.Key.Length))
            {using var k=hive.Root.OpenSubKey(b.Key);if(k!=null && k.ValueCount==0 && k.SubKeyCount==0) { k.Dispose();hive.Root.DeleteSubKey(b.Key,false); }}
        }
    }
    public static void PrivateFolder(LocalUser user,string root)
    {
        user=WindowsAccounts.Fresh(user);root=Path.GetFullPath(root);Storage.NoLinks(root);
        if(root.StartsWith(@"\\") || new DriveInfo(Path.GetPathRoot(root)!).DriveFormat!="NTFS") throw new IOException("Thư mục riêng phải nằm trên ổ NTFS cục bộ.");
        var path=Path.Combine(root,Policy.UserName(user.Name));Storage.NoLinks(path);
        if(Directory.Exists(path))throw new IOException("Thư mục đã tồn tại; không tự ý thay ACL dữ liệu hiện có.");
        Directory.CreateDirectory(path);Storage.SecureDirectory(path,user.Sid);
    }
}
