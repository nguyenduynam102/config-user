using System.IO.Compression;
namespace WinUser;
public sealed record ExportEntry(string Source,string Relative,bool Directory,long Bytes);
public sealed record ExportPlan(string Root,string Destination,IReadOnlyList<ExportEntry> Entries)
{
    public long Bytes=>Entries.Sum(e=>e.Bytes);public int Files=>Entries.Count(e=>!e.Directory);
}
public static class PrivateExport
{
    public static ExportPlan Preview(string root,string destination,bool folder=false)
    {
        root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));destination=Path.GetFullPath(destination);
        if(root.StartsWith(@"\\")||destination.StartsWith(@"\\"))throw new IOException("Xuất ZIP ra ổ cục bộ trước, rồi gửi ZIP qua mạng.");
        if(!Directory.Exists(root))throw new DirectoryNotFoundException("Không tìm thấy thư mục nguồn.");
        if(!folder&&!destination.EndsWith(".zip",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Tệp đích phải là .zip.");
        if(destination.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||destination.Equals(root,StringComparison.OrdinalIgnoreCase))throw new IOException("Chọn nơi lưu bên ngoài thư mục nguồn.");
        if(File.Exists(destination)||Directory.Exists(destination))throw new IOException("Tệp đích đã tồn tại; không ghi đè.");
        Storage.NoLinks(root);Storage.NoLinks(Path.GetDirectoryName(destination)!);
        var entries=new List<ExportEntry>();Walk(root);return new(root,destination,entries);
        void Walk(string directory)
        {
            Storage.NoLinks(directory);
            foreach(var path in Directory.EnumerateFileSystemEntries(directory))
            {
                var attr=File.GetAttributes(path);
                if((attr & FileAttributes.ReparsePoint)!=0)throw new IOException("Không xuất liên kết/junction: "+Path.GetFileName(path));
                if((attr & FileAttributes.Encrypted)!=0)throw new IOException("Tệp/thư mục có EFS: cần chủ sở hữu giải mã trước khi xuất.");
                var relative=Path.GetRelativePath(root,path).Replace('\\','/');
                if((attr & FileAttributes.Directory)!=0){entries.Add(new(path,relative+"/",true,0));Walk(path);}
                else entries.Add(new(path,relative,false,new FileInfo(path).Length));
            }
        }
    }
    public static void WriteFolder(ExportPlan plan,CancellationToken stop=default)
    {
        var fresh=Preview(plan.Root,plan.Destination,true);
        if(!fresh.Entries.Select(e=>(e.Relative,e.Bytes)).SequenceEqual(plan.Entries.Select(e=>(e.Relative,e.Bytes))))throw new IOException("Nguồn đã thay đổi; xem trước lại.");
        SafeFile.CreateDirectoryData(plan.Destination);
        foreach(var item in fresh.Entries)
        {
            stop.ThrowIfCancellationRequested();var target=Path.Combine(plan.Destination,item.Relative.Replace('/',Path.DirectorySeparatorChar));
            Storage.NoLinks(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(target))!);
            if(item.Directory){SafeFile.CreateDirectoryData(Path.TrimEndingDirectorySeparator(target));continue;}
            using var input=SafeFile.ReadData(item.Source);using var output=SafeFile.CreateData(target);input.CopyTo(output);
        }
        // Newly created objects inherit destination permissions; no owner/DACL/EFS metadata is copied.
    }
    public static void Write(ExportPlan plan,CancellationToken stop=default)
    {
        // Recheck before writing. Every source file is opened without following reparse points.
        var fresh=Preview(plan.Root,plan.Destination);
        if(!fresh.Entries.Select(e=>(e.Relative,e.Bytes)).SequenceEqual(plan.Entries.Select(e=>(e.Relative,e.Bytes))))throw new IOException("Nguồn đã thay đổi; hãy xem trước lại.");
        using var output=SafeFile.CreateData(plan.Destination);
        using var archive=new ZipArchive(output,ZipArchiveMode.Create,leaveOpen:false);
        foreach(var item in fresh.Entries)
        {
            stop.ThrowIfCancellationRequested();var entry=archive.CreateEntry(item.Relative,CompressionLevel.Optimal);entry.ExternalAttributes=0;
            if(item.Directory)continue;
            using var input=SafeFile.ReadData(item.Source);using var target=entry.Open();input.CopyTo(target);
        }
        // ZIP stores data, not NTFS ACLs/owner/EFS keys. Output inherits destination ACL.
    }
}
