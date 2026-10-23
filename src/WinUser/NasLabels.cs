using Microsoft.Win32;
using System.Runtime.InteropServices;
namespace WinUser;
public static class NasLabels
{
    public static string Validate(string label)
    {
        label=label.Trim();if(label.Length>64||label.Any(char.IsControl))throw new ArgumentException("Tên ổ tối đa 64 ký tự, không chứa ký tự điều khiển.");return label;
    }
    static string Key(string unc)=>@"Software\Microsoft\Windows\CurrentVersion\Explorer\MountPoints2\"+Policy.Unc(unc).Replace('\\','#');
    [DllImport("shell32.dll")] static extern void SHChangeNotify(uint eventId,uint flags,IntPtr item1,IntPtr item2);
    public static void Apply(string unc,string label)
    {
        label=Validate(label);if(label.Length==0)return;
        using var key=Registry.CurrentUser.CreateSubKey(Key(unc));if((string?)key.GetValue("_LabelFromReg")==label)return;key.SetValue("_LabelFromReg",label,RegistryValueKind.String);SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
    }
    public static void Clear(string unc,string label)
    {
        if(string.IsNullOrEmpty(label))return;using var key=Registry.CurrentUser.OpenSubKey(Key(unc),true);if((string?)key?.GetValue("_LabelFromReg")==label)key.DeleteValue("_LabelFromReg",false);
    }
}
