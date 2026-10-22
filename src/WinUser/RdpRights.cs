using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
namespace WinUser;
public static class RdpRights
{
    const string Deny="SeDenyRemoteInteractiveLogonRight";
    [StructLayout(LayoutKind.Sequential)] struct Attributes {public int Length;public IntPtr Root,Name;public uint Flags;public IntPtr Descriptor,Qos;}
    [StructLayout(LayoutKind.Sequential)] struct Text {public ushort Length,MaximumLength;public IntPtr Buffer;}
    [DllImport("advapi32.dll")] static extern uint LsaOpenPolicy(IntPtr system,ref Attributes attr,uint access,out IntPtr handle);
    [DllImport("advapi32.dll")] static extern uint LsaAddAccountRights(IntPtr handle,byte[] sid,Text[] rights,uint count);
    [DllImport("advapi32.dll")] static extern uint LsaRemoveAccountRights(IntPtr handle,byte[] sid,[MarshalAs(UnmanagedType.U1)] bool all,Text[] rights,uint count);
    [DllImport("advapi32.dll")] static extern uint LsaEnumerateAccountRights(IntPtr handle,byte[] sid,out IntPtr rights,out uint count);
    [DllImport("advapi32.dll")] static extern uint LsaNtStatusToWinError(uint status);
    [DllImport("advapi32.dll")] static extern uint LsaFreeMemory(IntPtr pointer);
    [DllImport("advapi32.dll")] static extern uint LsaClose(IntPtr handle);
    static byte[] Sid(string value){var sid=new SecurityIdentifier(value);var bytes=new byte[sid.BinaryLength];sid.GetBinaryForm(bytes,0);return bytes;}
    static IntPtr Open(bool write){var attr=new Attributes {Length=Marshal.SizeOf<Attributes>()};Check(LsaOpenPolicy(IntPtr.Zero,ref attr,write?0x810u:0x800u,out var handle));return handle;}
    static void Check(uint status){if(status!=0)throw new Win32Exception((int)LsaNtStatusToWinError(status));}
    public static bool Denied(string sid)
    {
        var handle=Open(false);
        try
        {
            var status=LsaEnumerateAccountRights(handle,Sid(sid),out var p,out var count);
            if(status==0xC0000034)return false;
            Check(status);
            try {for(int i=0;i<count;i++){var t=Marshal.PtrToStructure<Text>(p+i*Marshal.SizeOf<Text>());if(Marshal.PtrToStringUni(t.Buffer,t.Length/2)==Deny)return true;}return false;}
            finally {LsaFreeMemory(p);}
        }
        finally {LsaClose(handle);}
    }
    public static void SetDenied(string sid,bool deny)
    {
        var handle=Open(true);var buffer=Marshal.StringToHGlobalUni(Deny);
        try {var text=new Text {Buffer=buffer,Length=(ushort)(Deny.Length*2),MaximumLength=(ushort)((Deny.Length+1)*2)};var status=deny?LsaAddAccountRights(handle,Sid(sid),new[]{text},1):LsaRemoveAccountRights(handle,Sid(sid),false,new[]{text},1);if(status!=0xC0000034)Check(status);}
        finally {Marshal.FreeHGlobal(buffer);LsaClose(handle);}
    }
}
