using Microsoft.Win32;
using System.ServiceProcess;
namespace WinUser;
internal static class RdpDiagnostics
{
    public static string Read()
    {
        var lines=new List<string>{"KIỂM TRA RDP — không thay đổi cấu hình","Máy: "+Environment.MachineName,"Thời điểm: "+DateTime.Now.ToString("g"),""};
        try{using var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Terminal Server");lines.Add("RDP máy: "+(key?.GetValue("fDenyTSConnections") is int n?(n==0?"Đã bật":"Đang tắt"):"Chưa xác định"));}catch(Exception e){lines.Add("RDP: "+e.Message);}
        int port=3389;
        try{using var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Terminal Server\WinStations\RDP-Tcp");if(key?.GetValue("PortNumber") is int p)port=p;lines.Add("Cổng RDP: "+port);lines.Add("NLA: "+(key?.GetValue("UserAuthentication") is int n?(n==1?"Đang yêu cầu":"Không yêu cầu"):"Chưa xác định"));}catch(Exception e){lines.Add("NLA/cổng: "+e.Message);}
        try{using var service=new ServiceController("TermService");lines.Add("Dịch vụ Remote Desktop: "+service.Status);}catch(Exception e){lines.Add("Dịch vụ: "+e.Message);}
        object? policy=null;object? rules=null;
        try
        {
            policy=Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2")!);dynamic firewall=policy!;int profiles=firewall.CurrentProfileTypes;rules=firewall.Rules;var matches=new List<string>();
            foreach(object rule in (System.Collections.IEnumerable)rules)
            {
                try{dynamic r=rule;if(!(bool)r.Enabled||(int)r.Direction!=1||(int)r.Action!=1||((int)r.Profiles&profiles)==0)continue;int protocol=r.Protocol;if(protocol is not (6 or 17))continue;string ports=r.LocalPorts??"";if(ports.Split(',').Select(s=>s.Trim()).Contains(port.ToString()))matches.Add((string)r.Name+" ("+(protocol==6?"TCP":"UDP")+")");}
                finally{if(System.Runtime.InteropServices.Marshal.IsComObject(rule))System.Runtime.InteropServices.Marshal.FinalReleaseComObject(rule);}
            }
            lines.Add("Firewall — rule allow inbound bật cho cổng cụ thể và profile hiện tại: "+matches.Count);lines.AddRange(matches.Select(n=>"  • "+n));lines.Add("Không tính đầy đủ rule block, dải cổng, địa chỉ, IPsec hoặc chính sách miền.");
        }
        catch(Exception e){lines.Add("Không kiểm tra được firewall: "+e.Message);}
        finally{if(rules!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(rules);if(policy!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(policy);}
        lines.AddRange(new[]{"","Danh sách user hiển thị membership/deny trực tiếp, không phải quyền hiệu lực đầy đủ.","Deny từ nhóm hoặc GPO có thể chặn RDP dù user đã có nhóm Remote Desktop Users.","Kiểm tra mật khẩu và đăng nhập từ máy khách RDP để xác nhận thực tế.","Windows 10 Pro vẫn giữ giới hạn phiên tương tác; ứng dụng không sửa hệ thống để vượt giới hạn."});return string.Join(Environment.NewLine,lines);
    }
}
