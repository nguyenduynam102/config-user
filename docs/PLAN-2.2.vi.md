# Kế hoạch Preview 2.2

Giữ nền giao diện bản 2.1, không lấy lại bản 3. Tách module theo trách nhiệm:

1. AccountManagement: đọc thông tin chi tiết, đổi tên, mật khẩu, mô tả, tùy chọn mật khẩu, mở khóa và xóa tài khoản. Bảo vệ tài khoản hiện tại/tích hợp; không xóa profile hay dữ liệu khi xóa user.
2. MainForm.Selection: tìm kiếm/lọc, trạng thái, chọn bằng checkbox hoặc Ctrl/Shift và bôi đen. Nguồn độc lập, không tự chọn mặc định. Đích luôn lấy từ lựa chọn hiện tại; hiển thị số lượng.
3. MainForm.Accounts: nút mở/đóng RDP, khóa/mở tài khoản, ngắt phiên riêng; chi tiết và xác nhận trước thay đổi.
4. NAS: thêm tên hiển thị, tương thích cấu hình cũ; Agent áp dụng trong HKCU của user đích, không đổi tên volume NAS.
5. Shutdown: bỏ khỏi phạm vi theo yêu cầu cập nhật của người dùng; người dùng tự cấu hình thủ công.
6. PrivateExport: sao chép dữ liệu sang thư mục mới kế thừa quyền nơi nhận, bên cạnh ZIP. Không sao chép ACL nguồn, không nới ACL dữ liệu riêng. Chặn EFS, reparse và đích nằm trong nguồn. Explorer di chuyển cùng ổ/công cụ giữ ACL không thể tự đảm bảo nhận quyền nơi đích.
7. Kiểm chứng: build, kiểm tra chính sách/NetAPI trên Linux; chạy UI và tích hợp Windows bằng CI với tài khoản/SMB dùng một lần. Kiểm tra chức năng mới bằng API thật khi có thể; không thử shutdown máy. Ghi rõ giới hạn chưa xác minh Windows 10/TrueNAS thực tế.

Mỗi thao tác nhiều user báo kết quả từng SID; bí mật không xuất vào log. Không đổi giới hạn phiên Windows, không xóa dữ liệu NAS, không ép đăng xuất phiên đang làm việc.
