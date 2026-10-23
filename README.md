# WinUser RDP Manager — Preview 2.2

Ứng dụng C# WinForms cho Windows x64, tiếp tục từ giao diện bản 2.1. Quản lý user/RDP, shortcut Desktop, Registry theo SID, thư mục riêng, bộ cấu hình và ánh xạ NAS. Không thay đổi giới hạn phiên của Windows.

## Tải và cài

- [Tải ZIP chứa WinUser.exe và bộ cài](https://github.com/nguyenduynam102/config-user/raw/refs/heads/main/downloads/WinUser-v1.0-preview2.2-win-x64.zip)
- [Bản phát hành và mã nguồn](https://github.com/nguyenduynam102/config-user/releases/tag/v1.0.0-preview.2.2)

Giải nén ZIP. Đóng WinUser/Agent cũ, chạy **Install.cmd → Run as administrator** để cập nhật cả ứng dụng, Broker và Agent. Nếu chỉ chạy EXE mới từ Downloads, dịch vụ NAS cũ chưa được cập nhật. Bộ cài giữ cấu hình/bản sao lưu ở `%ProgramData%\WinUser`.

## Chọn nguồn và đích

Bảng trái là **USER ĐÍCH**. Mặc định click chọn một, Ctrl chọn rời, Shift chọn dải hoặc kéo chọn nhiều dòng. Có thể chuyển sang **Chọn bằng ô tick**; hai chế độ được tách rõ, không cộng dồn ngầm. Chọn tất cả chỉ áp dụng các dòng đang hiển thị. Số lượng luôn hiện phía trên.

Tìm theo tên/SID, lọc theo trạng thái tài khoản/RDP/phiên. **Đổi bộ lọc, làm mới hoặc đổi chế độ sẽ bỏ chọn đích** để tránh tác động user ẩn. Kiểm tra danh sách tên trong xác nhận trước thao tác hàng loạt.

Tab Desktop có **USER NGUỒN (đọc)** riêng, gõ ký tự để tìm trong danh sách. Không tự chọn nguồn đầu tiên. Đồng bộ không cho sửa chính user nguồn. Thêm shortcut bỏ qua tên trùng; Public Desktop và profile chuyển hướng không thuộc phạm vi.

## Quản lý tài khoản / RDP

- Tạo user thường và thêm nhóm Users; sửa tên đầy đủ, mô tả, mật khẩu không hết hạn; xem SID, nhóm, trạng thái phiên.
- Đổi tên giữ SID và đường dẫn profile, không đổi `C:\Users\...`. Đặt lại mật khẩu cần nhập lại; admin reset có thể ảnh hưởng EFS/DPAPI của user.
- Khóa tài khoản ngăn đăng nhập mới và ngắt phiên RDP, không đăng xuất. Có nút ngắt phiên riêng và gỡ lockout do sai mật khẩu.
- **Mở RDP / Đóng RDP** và cột **Trạng thái RDP** phân biệt đã cấp, đã đóng bằng Deny, chưa cấp. Mở RDP cấp nhóm Remote Desktop Users và bỏ Deny trực tiếp của SID; đóng RDP đặt Deny trực tiếp và gỡ nhóm, không vô hiệu hóa tài khoản.
- Trạng thái “Đã mở RDP*” là cấu hình trực tiếp do app quản lý, chưa chứng minh đăng nhập được: còn phụ thuộc dịch vụ, edition Windows, mật khẩu, nhóm/chính sách miền. GPO có thể ghi đè.
- Xóa tài khoản phải gõ tên, chặn user còn phiên, thu hồi cấu hình NAS nhưng giữ profile/tệp. Tạo lại cùng tên không khôi phục SID/quyền cũ.
- Thêm/gỡ nhóm cục bộ; nút **Quản lý Windows…** mở `lusrmgr.msc` cho tùy chọn Windows nâng cao, bao gồm quản trị nhóm Administrators. Bảo vệ tài khoản hiện tại và tài khoản Windows tích hợp.

Không thêm chức năng Shutdown theo yêu cầu cập nhật của người dùng.

## NAS

Chọn user đích → NAS → **Thêm ánh xạ**. Nhập ký tự ổ D:–Z:, **tên hiển thị tùy chọn**, UNC, tài khoản/mật khẩu NAS. Bảng NAS có nút thêm/sửa/xóa/kiểm tra rõ ràng. Sửa cùng máy chủ/tài khoản có thể để trống mật khẩu để giữ lại. Không cho thêm trùng ổ hoặc dùng nhiều tài khoản SMB trên cùng máy chủ trong một phiên.

Tên ổ áp dụng qua Explorer MountPoints2 trong **HKCU của user đích**, không đổi tên volume trên NAS. Explorer dùng một tên cho mỗi share; các ổ cùng share trong cùng user phải có cùng tên. Có thể cần mở lại cửa sổ Explorer để thấy thay đổi. Bộ cấu hình mặc định cũng lưu tên ổ.

Broker LocalSystem dùng named pipe lấy SID thật qua impersonation. Mật khẩu mã hóa DPAPI gắn với máy, không ghi plaintext/command line. Agent chạy trong phiên user, thử lại mỗi 30 giây; Broker tìm phiên để khởi chạy Agent mỗi 15 giây. HKLM Run hỗ trợ đăng nhập sau. Không ép đóng tệp hay đăng xuất user để tháo ổ. Kiểm tra SMB trong ứng dụng dùng phiên Administrator; không thay thế kiểm tra Explorer của user đích.

## Thư mục riêng và bản sao chia sẻ

Dữ liệu gốc giữ quyền user sở hữu + Administrators + SYSTEM, kế thừa xuống cây con. Không cấp Everyone cho tệp con: Windows thường cho bỏ qua kiểm tra traverse, nên chỉ khóa thư mục gốc không bảo vệ được tệp khi biết đường dẫn.

- **Sao chép ra thư mục chung**: chọn nguồn, thư mục đích mà người nhận có quyền đọc, đặt tên thư mục bản sao mới. Sao chép nội dung, **không chép ACL/owner nguồn**; toàn bộ bản sao nhận quyền thư mục đích. Không sửa/xóa nguồn, không ghi đè đích có sẵn.
- **Xuất ZIP chia sẻ**: ZIP không mang ACL NTFS; tệp giải nén nhận quyền nơi giải nén. Có thể gửi sang máy khác mà không mang quyền SID của máy nguồn.
- Copy Explorer thông thường thường nhận quyền nơi nhận; **di chuyển cùng ổ hoặc công cụ bảo toàn ACL có thể giữ quyền cũ**. App không tự theo dõi/sửa mọi thao tác bên ngoài: dùng một trong hai chức năng trên để tạo bản sao chia sẻ.
- Người nhận vẫn cần quyền nơi nhận. Không gỡ mật khẩu tài liệu/DRM; từ chối EFS, junction/symlink/hardlink. Nếu xuất lỗi giữa chừng có thể còn bản sao dở dang: kiểm tra trước khi gửi. Xuất ra ổ local trước rồi gửi qua mạng.

## Module và kiểm thử

Kế hoạch: [PLAN-2.2.vi.md](docs/PLAN-2.2.vi.md). Tài khoản: `AccountManagement.cs`; giao diện tài khoản: `MainForm.Accounts.cs`; chọn/lọc: `MainForm.Selection.cs`; NAS: `MainForm.Nas.cs`, `Nas.cs`, `NasLabels.cs`; bản sao dữ liệu: `PrivateExport.cs`, `MainForm.PrivateExport.cs`.

Windows: chạy `Build.cmd` với .NET SDK 8+. Linux cloud: `bash scripts/cloud-setup.sh`. Quy trình chạy kiểm tra chính sách, hồi quy NetAPI, build EXE và CI Windows UI/SMB/ACL/tài khoản dùng một lần. Bằng chứng và phạm vi kiểm chứng trong [WINDOWS-VALIDATION.vi.md](docs/WINDOWS-VALIDATION.vi.md) và `VALIDATION.json` trong gói tải. CI Windows không thay thế nghiệm thu RDP Windows 10 Pro/TrueNAS/GPO tại máy thật.
