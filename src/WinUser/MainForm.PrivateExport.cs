namespace WinUser;
public sealed partial class MainForm
{
    async void ExportPrivateFolder()
    {
        try
        {
            using var source=new FolderBrowserDialog(){Description="Chọn thư mục cá nhân cần sao chép"};if(source.ShowDialog(this)!=DialogResult.OK)return;
            using var parent=new FolderBrowserDialog(){Description="Chọn thư mục chung nơi người nhận đã có quyền đọc"};if(parent.ShowDialog(this)!=DialogResult.OK)return;
            var name=Prompt("Tên thư mục bản sao mới",new DirectoryInfo(source.SelectedPath).Name+"-chia-se");if(string.IsNullOrWhiteSpace(name))return;
            if(name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name is "." or "..")throw new ArgumentException("Tên thư mục không hợp lệ.");
            var plan=PrivateExport.Preview(source.SelectedPath,Path.Combine(parent.SelectedPath,name),true);
            if(!Confirm($"Sao chép {plan.Files} tệp, {plan.Bytes:N0} byte\r\n{plan.Root} → {plan.Destination}\r\nBản sao nhận quyền thư mục đích, không mang ACL nguồn. Người nhận cần có quyền đọc thư mục đích. Không thay đổi dữ liệu gốc."))return;
            Enabled=false;UseWaitCursor=true;await Task.Run(()=>PrivateExport.WriteFolder(plan));Storage.Log("ExportPrivateFolder","data-copy",true);Append("Đã sao chép theo quyền nơi nhận: "+plan.Destination);
        }
        catch(Exception e){Append("LỖI sao chép: "+e.Message);MessageBox.Show(this,e.Message+"\r\nNếu có bản sao dở dang, kiểm tra trước khi sử dụng; ứng dụng không tự xóa dữ liệu.","Chưa hoàn tất");}
        finally{Enabled=true;UseWaitCursor=false;}
    }
    async void ExportPrivateZip()
    {
        try
        {
            using var source=new FolderBrowserDialog(){Description="Chọn thư mục riêng hoặc thư mục con cần xuất bản sao chia sẻ"};if(source.ShowDialog(this)!=DialogResult.OK)return;
            using var dest=new SaveFileDialog(){Filter="ZIP chia sẻ|*.zip",FileName=new DirectoryInfo(source.SelectedPath).Name+"-chia-se.zip",OverwritePrompt=true};if(dest.ShowDialog(this)!=DialogResult.OK)return;
            var plan=PrivateExport.Preview(source.SelectedPath,dest.FileName);
            if(!Confirm($"Xuất bản sao ZIP: {plan.Files} tệp, {plan.Bytes:N0} byte\\r\\nNguồn: {plan.Root}\\r\\nĐích: {plan.Destination}\\r\\n\\r\\nKhông thay đổi quyền hoặc xóa dữ liệu nguồn. ZIP không mang ACL NTFS của nguồn. Người nhận cần quyền truy cập nơi nhận ZIP; tệp khi giải nén nhận quyền của nơi giải nén. EFS/DRM và mật khẩu tài liệu vẫn có thể chặn mở.".Replace("\\r\\n","\r\n")))return;
            UseWaitCursor=true;Enabled=false;
            await Task.Run(()=>PrivateExport.Write(plan));Storage.Log("ExportPrivateZip","data-copy",true);Append("Đã xuất ZIP chia sẻ: "+plan.Destination);MessageBox.Show(this,"Đã xuất ZIP. Gửi tệp này ra ngoài hoặc giải nén tại nơi người nhận có quyền đọc.","Hoàn tất");
        }
        catch(Exception e){Append("LỖI xuất ZIP: "+e.Message);MessageBox.Show(this,e.Message+"\r\nNếu có ZIP dở dang, không gửi tệp đó; chọn tên mới khi thử lại.","Chưa hoàn tất",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        finally{UseWaitCursor=false;Enabled=true;}
    }
}
