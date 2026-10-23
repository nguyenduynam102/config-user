namespace WinUser;
public record Preset(string Id,string Name,bool Rdp,string? PrivateRoot,bool HasRegistry,string[] Shortcuts,List<NasMapping> Nas);
public static class Presets
{
    static string Root=>Path.Combine(Storage.Root,"Presets");
    public static List<Preset> List(){Directory.CreateDirectory(Root);return Directory.GetDirectories(Root).Where(p=>Guid.TryParseExact(Path.GetFileName(p),"N",out _)).Select(p=>Storage.Read<Preset?>(Path.Combine(p,"preset.json"),null)).OfType<Preset>().OrderBy(p=>p.Name).ToList();}
    public static string PathOf(string id){if(!Guid.TryParseExact(id,"N",out _))throw new ArgumentException("ID cấu hình không hợp lệ.");return Path.Combine(Root,id);}
    public static Preset Create(string name,LocalUser? source,string[] shortcuts,string? registry,string? privateRoot,bool rdp)
    {
        if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Cần tên cấu hình.");
        var id=Guid.NewGuid().ToString("N");var folder=PathOf(id);Directory.CreateDirectory(folder);
        if(registry!=null){Policy.ParseRegistry(File.ReadAllText(registry));File.Copy(registry,Path.Combine(folder,"settings.reg"));}
        if(source!=null)foreach(var n in shortcuts){Policy.Shortcut(n);SafeFile.Copy(Path.Combine(UserData.Desktop(source),n),Path.Combine(folder,n));}
        var p=new Preset(id,name,rdp,privateRoot,registry!=null,shortcuts,source==null?new():Nas.Load().Where(m=>m.Sid==source.Sid).ToList());
        Storage.Write(Path.Combine(folder,"preset.json"),p);return p;
    }
    public static void Rename(Preset p,string name){if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Tên không được trống.");Backup(p);Storage.Write(Path.Combine(PathOf(p.Id),"preset.json"),p with{Name=name});}
    static void Backup(Preset p){var backup=Storage.BackupFolder("Preset",p.Id);foreach(var file in Directory.GetFiles(PathOf(p.Id)))File.Copy(file,Path.Combine(backup,Path.GetFileName(file)));}
    public static void Delete(Preset p){Backup(p);Directory.Delete(PathOf(p.Id),true);}
    public static void Clone(Preset p,string name){var id=Guid.NewGuid().ToString("N");var folder=PathOf(id);Directory.CreateDirectory(folder);foreach(var file in Directory.GetFiles(PathOf(p.Id)))File.Copy(file,Path.Combine(folder,Path.GetFileName(file)));Storage.Write(Path.Combine(folder,"preset.json"),p with{Id=id,Name=name});}
    public static void Update(Preset p,Dictionary<string,string> assets,string? registry)
    {
        var old=List().Single(x=>x.Id==p.Id);Backup(old);
        // Stage the entire replacement so editing an asset from the same folder is safe.
        var stage=Path.Combine(Root,".stage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        try
        {
            foreach(var pair in assets){Policy.Shortcut(pair.Key);SafeFile.Copy(pair.Value,Path.Combine(stage,pair.Key));}
            if(registry!=null){Policy.ParseRegistry(File.ReadAllText(registry));SafeFile.Copy(registry,Path.Combine(stage,"settings.reg"));}
            Storage.Write(Path.Combine(stage,"preset.json"),p);
            var dest=PathOf(p.Id);var retired=dest+".retired-"+Guid.NewGuid().ToString("N");
            Directory.Move(dest,retired);
            try{Directory.Move(stage,dest);}catch{Directory.Move(retired,dest);throw;}
            Directory.Delete(retired,true);
        }
        finally{if(Directory.Exists(stage))Directory.Delete(stage,true);}
    }
    public static void Apply(Preset p,LocalUser user)
    {
        WindowsAccounts.Rdp(user,p.Rdp);
        var desktop=UserData.Desktop(user);var added=p.Shortcuts.Where(n=>!File.Exists(Path.Combine(desktop,Policy.Shortcut(n)))).ToArray();
        var backup=Storage.BackupFolder("Desktop",user.Sid);Storage.Write(Path.Combine(backup,"desktop.json"),new DesktopBackup(user.Sid,desktop,added,Array.Empty<string>()));
        foreach(var n in added)SafeFile.Copy(Path.Combine(PathOf(p.Id),n),Path.Combine(desktop,n),false);
        if(p.HasRegistry)UserData.ApplyRegistry(user,Path.Combine(PathOf(p.Id),"settings.reg"));
        if(!string.IsNullOrWhiteSpace(p.PrivateRoot))UserData.PrivateFolder(user,p.PrivateRoot);
        foreach(var m in p.Nas)Nas.Save(user,m.Drive,m.Unc,m.User,Storage.Decrypt(m.ProtectedPassword),true,m.Label);
    }
}
