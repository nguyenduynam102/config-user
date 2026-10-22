using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace WinUser;
// Open the entire NT path with OBJ_DONT_REPARSE; never follow user-controlled
// junctions/symlinks between validation and the actual privileged file operation.
internal static class SafeFile
{
    [StructLayout(LayoutKind.Sequential)] struct Unicode {public ushort Length,Maximum;public IntPtr Buffer;}
    [StructLayout(LayoutKind.Sequential)] struct Attributes {public uint Length;public IntPtr Root,Name;public uint Flags;public IntPtr Security,Qos;}
    [StructLayout(LayoutKind.Sequential)] struct IoStatus {public IntPtr Status;public UIntPtr Information;}
    [StructLayout(LayoutKind.Sequential)] struct Info {public uint Attributes;public System.Runtime.InteropServices.ComTypes.FILETIME Created,Access,Written;public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;}
    [DllImport("ntdll.dll")] static extern int NtCreateFile(out SafeFileHandle handle,uint access,ref Attributes attributes,out IoStatus status,IntPtr allocation,uint fileAttributes,uint share,uint disposition,uint options,IntPtr ea,uint eaLength);
    [DllImport("ntdll.dll")] static extern uint RtlNtStatusToDosError(int status);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetFileInformationByHandle(SafeFileHandle handle,out Info info);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetFileInformationByHandle(SafeFileHandle handle,int type,ref int information,int size);
    static SafeFileHandle Open(string path,uint access,bool create)
    {
        path=Path.GetFullPath(path);if(path.StartsWith(@"\\"))throw new IOException("Chỉ thao tác shortcut cục bộ.");
        var text=@"\??\"+path;var buffer=Marshal.StringToHGlobalUni(text);var up=Marshal.AllocHGlobal(Marshal.SizeOf<Unicode>());
        try
        {
            Marshal.StructureToPtr(new Unicode{Length=checked((ushort)(text.Length*2)),Maximum=checked((ushort)((text.Length+1)*2)),Buffer=buffer},up,false);
            var attr=new Attributes{Length=(uint)Marshal.SizeOf<Attributes>(),Name=up,Flags=0x1040};
            var status=NtCreateFile(out var h,access|0x100080,ref attr,out _,IntPtr.Zero,0x80,1,create?3u:1u,0x60,IntPtr.Zero,0);
            if(status<0){h?.Dispose();throw new Win32Exception((int)RtlNtStatusToDosError(status));}
            if(!GetFileInformationByHandle(h,out var info)){h.Dispose();throw new Win32Exception();}
            if(info.Links!=1 || (info.Attributes & 0x400)!=0){h.Dispose();throw new IOException("Từ chối tệp hardlink/reparse point.");}
            return h;
        }
        finally{Marshal.FreeHGlobal(up);Marshal.FreeHGlobal(buffer);}
    }
    public static void Copy(string source,string dest,bool overwrite=false)
    {
        using var input=new FileStream(Open(source,0x80000000,false),FileAccess.Read);
        if(input.Length>16*1024*1024)throw new IOException("Shortcut/bản sao lưu lớn hơn 16 MiB bị từ chối.");
        if(!overwrite && File.Exists(dest))throw new IOException("Không ghi đè shortcut trùng.");
        // For create-only use FILE_CREATE to make collision rejection atomic.
        using var output=new FileStream(OpenForWrite(dest,overwrite),FileAccess.Write);output.SetLength(0);input.CopyTo(output);output.Flush(true);
    }
    static SafeFileHandle OpenForWrite(string path,bool overwrite)
    {
        if(overwrite)return Open(path,0x40000000,true);
        // Open supports create-if-missing; serialize with an exclusively-created placeholder
        // is insufficient in user-controlled directories. Use the native create disposition.
        return CreateOnly(path);
    }
    static SafeFileHandle CreateOnly(string path)
    {
        path=Path.GetFullPath(path);if(path.StartsWith(@"\\"))throw new IOException("Chỉ hỗ trợ đường dẫn cục bộ.");
        var text=@"\??\"+path;var buffer=Marshal.StringToHGlobalUni(text);var up=Marshal.AllocHGlobal(Marshal.SizeOf<Unicode>());
        try
        {
            Marshal.StructureToPtr(new Unicode{Length=checked((ushort)(text.Length*2)),Maximum=checked((ushort)((text.Length+1)*2)),Buffer=buffer},up,false);
            var attr=new Attributes{Length=(uint)Marshal.SizeOf<Attributes>(),Name=up,Flags=0x1040};
            var status=NtCreateFile(out var h,0x40100080,ref attr,out _,IntPtr.Zero,0x80,1,2,0x60,IntPtr.Zero,0);
            if(status<0){h?.Dispose();throw new Win32Exception((int)RtlNtStatusToDosError(status));}return h;
        }
        finally{Marshal.FreeHGlobal(up);Marshal.FreeHGlobal(buffer);}
    }
    public static void Delete(string path)
    {
        if(!File.Exists(path))return;
        using var handle=Open(path,0x10000,false);int delete=1;
        if(!SetFileInformationByHandle(handle,4,ref delete,4))throw new Win32Exception();
    }
}
