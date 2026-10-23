namespace WinUser;
public sealed partial class MainForm
{
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
