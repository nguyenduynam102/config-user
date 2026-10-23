# WinUser RDP Manager v1.0

Ứng dụng Windows thật, C# WinForms, giao diện tiếng Việt. Đặc tả chính thức: [docs/SPEC.vi.txt](docs/SPEC.vi.txt).

**Trạng thái: bản triển khai đầu tiên để kiểm thử, chưa nghiệm thu v1.0.** Đã biên dịch chéo trên Linux và chạy kiểm tra logic độc lập; chưa chạy EXE trên Windows 10 Pro, chưa thử RDP/SMB/TrueNAS thật. Không coi việc biên dịch thành công là chứng minh các API hệ điều hành đã hoạt động. Các giới hạn và ca kiểm thử còn thiếu ở [WINDOWS-VALIDATION.vi.md](docs/WINDOWS-VALIDATION.vi.md).


## Tải chương trình

- [Tải WinUser cho Windows x64 (ZIP chứa EXE và bộ cài)](https://github.com/nguyenduynam102/config-user/raw/refs/heads/main/downloads/WinUser-v1.0-preview3-win-x64.zip)
- [Xem bản phát hành và tải mã nguồn](https://github.com/nguyenduynam102/config-user/releases/tag/v1.0.0-preview.3)

Giải nén ZIP trước khi chạy. Đây là **bản thử nghiệm, chưa kiểm thử thực tế trên Windows/TrueNAS**. Không dùng trên dữ liệu sản xuất trước khi hoàn thành các ca kiểm thử trong tài liệu.

## Preview 3 — giao diện quản trị mới

- Biểu tượng kèm chữ, nhóm **Tài khoản / Quyền RDP / Phiên**, phông Segoe UI và hỗ trợ DPI PerMonitorV2.
- Danh sách tìm kiếm Ctrl+F, bộ lọc, sắp xếp cột; click tên chỉ xem chi tiết. Tick checkbox để chọn user đích; Ctrl/Shift + click bôi đen rồi Enter/Space hoặc nút Chọn dòng bôi đen để tick hàng loạt. Lựa chọn được giữ khi chuyển tab, lọc hoặc làm mới. User bị ẩn do lọc vẫn là đích, có cảnh báo số lượng và xác nhận danh sách đầy đủ.
- Nguồn Desktop là danh sách xổ xuống có tìm kiếm; nút Lấy user đang xem và menu chuột phải giúp chọn nhanh. Nguồn được loại khỏi các đích Desktop. Thay nguồn phải đọc lại shortcut.
- Đổi mật khẩu riêng từng user (mặc định) hoặc chung; nhập xác nhận, hiện/ẩn tùy chọn, cảnh báo EFS/chứng chỉ/credential. Không tự buộc đổi mật khẩu ở lần đăng nhập sau; không ghi mật khẩu vào log/cấu hình.
- Khóa tài khoản vẫn vô hiệu hóa + ngắt RDP không logoff. Nút **Ngắt phiên RDP** độc lập không khóa user. **Bật tài khoản** khác **Mở khóa nhập sai**; thao tác sau chỉ xóa lockout, giữ nguyên cờ vô hiệu hóa và quyền RDP.
- Tác vụ nhiều user chạy nền, báo tiến độ và kết quả từng user. Dừng chỉ bỏ các user chưa chạy, không hủy ngược bước đã thực hiện. Tài khoản đang quản trị và RID Windows tích hợp được bảo vệ; xem trước các user bị bỏ qua.
- Chi tiết user gồm SID, profile đã tồn tại, trạng thái tài khoản, nhóm/deny RDP trực tiếp, phiên và cấu hình NAS. Chỉ xem chi tiết không tạo profile hoặc đổi quyền.
- Kiểm tra RDP chỉ đọc: trạng thái bật máy, dịch vụ, NLA, cổng và rule firewall cơ bản. Không tự bật RDP/firewall, không kết luận quyền hiệu lực đầy đủ.
- Nhớ kích thước cửa sổ/cột, bộ lọc và thư mục gốc; không nhớ mật khẩu, user đích hoặc nguồn giữa hai lần mở. F5 làm mới; mục Trợ giúp mở trang cập nhật chính thức.
- Kiểm chứng: 49 kiểm tra logic, 10 kiểm tra native-contract trên Linux; workflow Windows UI checks thực thi kiểm tra chỉ đọc trên Windows Server GitHub-hosted. Xem kết quả CI và tài liệu Windows; không thay thế nghiệm thu Windows 10 Pro/RDP/TrueNAS thật.

## Bản Preview 2 — sửa lỗi tải danh sách user

Sửa lỗi Windows 124 “The system call level is not correct”: `NetUserEnum` dùng level 0 để liệt kê tên; `NetUserGetInfo` level 23 đọc SID/flags từng user. Sửa `NetLocalGroupGetMembers.resumehandle` thành con trỏ có kích thước theo kiến trúc x64. Có 5 kiểm tra hồi quy native-contract bằng thư viện C giả lập trên Linux: phân trang, SID/flags, tài khoản bị xóa giữa chừng, lỗi native, giải phóng bộ nhớ và gọi lặp. Đây không thay thế kiểm thử Windows thật. Tiêu đề cửa sổ bản mới hiển thị **Preview 2**.

## Biên dịch và cài đặt

1. Máy build Windows x64 cần .NET SDK 8 hoặc mới hơn; không cần Visual Studio IDE. Chạy `Build.cmd`.
2. Kết quả tại `artifacts\win-x64\WinUser.exe`, bản self-contained x64. Máy sử dụng không cần cài Visual Studio hoặc runtime .NET riêng. Không phải bản .NET Framework 4.8.
3. Với chức năng User/RDP/Desktop/Registry/NTFS, chạy EXE, chấp thuận UAC Administrator. Không tự bật Remote Desktop, firewall hoặc vượt giới hạn phiên Windows.
4. Với NAS, giải nén đầy đủ bộ phát hành và chạy `Install.cmd` bằng **Run as administrator**. Cài vào `%ProgramFiles%\WinUser`, đăng ký dịch vụ `WinUserBroker` LocalSystem và Agent tại khóa Run của Windows. Khi cập nhật, đóng giao diện và đăng xuất các phiên đang chạy Agent trước; bộ cài không tự đăng xuất hoặc kết thúc ứng dụng của user.
5. Agent khởi chạy trong ngữ cảnh user ở lần đăng nhập Windows/RDP tiếp theo; không hiện hộp nhập mật khẩu. Windows có thể trì hoãn chương trình Run; chưa bảo đảm ổ xuất hiện ngay trước Explorer/ứng dụng đầu tiên. Agent thử lại mỗi 30 giây nếu Broker/NAS chưa sẵn sàng.
6. `Uninstall.cmd` gỡ dịch vụ và tự khởi động; giữ nguyên tài khoản, profile, dữ liệu NAS, thư mục riêng và dữ liệu quản trị. Không tự xóa dữ liệu.

Máy Linux cloud: chạy `bash scripts/cloud-setup.sh` để cài công cụ trong `/workspace/tools`, kiểm tra logic và build EXE. Không thể chạy WinForms, API tài khoản Windows, dịch vụ Windows hoặc kiểm thử NTFS/RDP trên Linux.

## Sử dụng

- Checkbox bên trái chọn user đích; click tên để xem chi tiết. Bôi đen bằng Ctrl/Shift rồi Enter/Space để chọn hàng loạt. Thanh xanh luôn hiện user đích, kể cả user bị ẩn do bộ lọc. Chuyển tab không mất lựa chọn; mở lại app bắt đầu với danh sách đích trống. Thao tác thay đổi có xem trước/xác nhận; xem kết quả từng user ở thanh dưới. Một thao tác nhiều bước có thể thành công một phần.
- **User/RDP:** tạo user thường có mật khẩu theo chính sách máy; không cấp Administrator. Khóa bằng cờ disabled rồi ngắt các phiên RDP local bằng WTSDisconnectSession, không logoff. Không tắt hoặc đổi RDP của SID đang chạy giao diện và các tài khoản tích hợp. Tắt RDP gán `SeDenyRemoteInteractiveLogonRight` theo SID, gỡ nhóm Remote Desktop Users; bật làm ngược lại. Domain GPO và quyền deny từ nhóm vẫn có thể ngăn đăng nhập. Cột nhóm RDP chỉ phản ánh thành viên trực tiếp cùng deny trực tiếp, không tính đầy đủ chính sách hiệu lực.
- **Desktop:** chọn nguồn, đọc danh sách, chọn shortcut rồi xem trước/thêm/xóa cho user đích. Chỉ `.lnk/.url`, không Public Desktop, không ghi đè bản trùng. Biểu tượng phân biệt shortcut file và URL; không tự truy xuất biểu tượng từ đích mạng. Lưới chỉ là danh sách xem trước, **chưa hiển thị đúng tọa độ Desktop thật**. Cảnh báo đích `.lnk` không tồn tại không chặn sao chép. Desktop chuyển hướng ngoài profile chưa hỗ trợ. Dùng CreateProfile để chuẩn bị profile mới khi Windows cho phép.
- **Registry:** kiểm tra toàn bộ file trước khi ghi. Chỉ REG Version 5.00, `HKCU\Software`, kiểu REG_SZ/REG_DWORD. Không nhận HKLM/HKCR, xóa khóa/giá trị, hex, multiline, Run/RunOnce/Policies/Classes/Winlogon. Đây là phạm vi hỗ trợ có chủ ý; các file khác cần được đánh giá và bổ sung sau. Mount hive bằng SID khi chưa được Windows nạp; không sửa HKCU của Administrator. Hive đang bận/profile roaming có thể gây lỗi và được báo.
- **Thư mục riêng:** tạo thư mục mới trên NTFS, owner=user, quyền Full Control cho SID user/Administrators/SYSTEM, bỏ kế thừa. Không tự sửa ACL thư mục đã tồn tại, không xóa dữ liệu. Không thể ẩn tên thư mục bằng ACL trên Explorer cục bộ; dùng Access-Based Enumeration của SMB nếu cần ẩn khi duyệt share. ACL TrueNAS phải do quản trị viên NAS quản lý.
- **Bộ cấu hình:** lưu các lựa chọn hiện tại cùng NAS của user nguồn; đặt tên, sao chép, đổi tên/xóa có sao lưu. Nút Chỉnh sửa cho phép thay tên, RDP, thư mục gốc, Registry, thêm/bỏ shortcut và sửa/thêm/bỏ NAS cùng mật khẩu; không hiển thị mật khẩu cũ. Xem trước rồi lưu bộ, chưa áp dụng lên user. Chọn áp dụng bộ lúc tạo user hoặc áp dụng cho user tồn tại. Tạo user thành công nhưng áp dụng bộ lỗi sẽ giữ user và báo phần chưa hoàn tất.
- **NAS:** nhập ổ/UNC/tài khoản NAS/mật khẩu và lưu cho các SID đã chọn. Nhiều user có thể dùng cùng tài khoản NAS; mỗi SID có danh sách ánh xạ riêng. Agent ánh xạ trong phiên không nâng quyền của user bằng WNetAddConnection2; ổ của giao diện Administrator không phải ổ của user. Kiểm tra kết nối trên giao diện chỉ thử trong phiên Administrator. Mã 85: trùng ổ; 1219: xung đột credential cùng máy chủ; 1326: xác thực; 5: quyền; 2401: đang chờ ngắt kết nối cũ. Không tự sửa NAS ACL hoặc dùng tài khoản admin NAS.
- **Sao lưu/khôi phục:** tại `%ProgramData%\WinUser\Backups`. Chọn một thư mục chứa `desktop.json`, `registry.json`, `nas.json` hoặc `preset.json`. Sao lưu trước import/copy/delete/đổi cấu hình và trước khôi phục. Shortcut trùng khi thêm được giữ nguyên. Khôi phục có thể ghi đè thay đổi phát sinh sau sao lưu, cần xem kỹ trước xác nhận. Không có khôi phục toàn hệ thống, mật khẩu Windows hoặc dữ liệu NAS.

## Bảo mật và vận hành

- Giao diện tự nâng quyền, Agent giữ manifest asInvoker. Cùng một EXE chạy chế độ dịch vụ `--broker` hoặc phiên user `--agent`.
- `%ProgramData%\WinUser` chỉ SYSTEM và Administrators truy cập. Mật khẩu NAS được bảo vệ bằng Windows DPAPI LocalMachine **kèm ACL bắt buộc**; DPAPI LocalMachine riêng lẻ không cô lập các user. Không lưu plaintext vào JSON/Registry/script/log/tham số lệnh. Sao lưu cấu hình vẫn chứa ciphertext nhạy cảm và gắn với máy này.
- Named pipe từ chối Network SID, cấp user đã xác thực truy cập, Broker dùng impersonation lấy SID thực, không nhận SID do client gửi. Chỉ trả ánh xạ của SID đó khi tài khoản còn enabled. Agent đối chiếu PID máy chủ pipe với PID dịch vụ do Service Control Manager cung cấp để chống giả mạo pipe. Chưa kiểm chứng chống giả mạo/xuyên SID trên Windows thật.
- User được cấp NAS **có thể lấy mật khẩu của chính mình** từ bộ nhớ Agent/pipe trong phiên; Windows phải có bí mật để xác thực SMB. Không thể bảo đảm giấu mật khẩu của tài khoản NAS dùng chung khỏi chính những user được cấp. Tài khoản NAS riêng theo user hoặc Kerberos/AD là phương án cô lập tốt hơn.
- Xóa cấu hình thu hồi việc cấp phát ở lần Agent đọc tiếp; Agent chỉ ngắt mapping mình quản lý, không ép đóng tệp. Nếu NAS còn handle/SMB session hay user đã lấy mật khẩu, cần đóng phiên/đổi mật khẩu/thu hồi ACL phía NAS. Không tự động ngắt mọi SMB session của user. Đổi mật khẩu thử ngắt mapping cũ rồi kết nối lại; Windows credential cache và mapping khác cùng máy chủ có thể gây 1219, được báo để xử lý.
- Nhật ký JSONL ghi UTC, actor SID, chức năng, target, mã kết quả; không ghi password, payload Registry hoặc exception stack. Trạng thái Agent là dữ liệu user có thể sửa, không phải chứng cứ bảo mật.
- Tệp shortcut dùng mở NT path với OBJ_DONT_REPARSE, từ chối hardlink/reparse point và giới hạn 16 MiB khi sao chép. Cần kiểm thử đường dẫn, ACL và các cuộc đua filesystem trước triển khai. EXE chưa ký số, chưa đánh giá bảo mật độc lập.

## Cấu trúc

`src/WinUser`: GUI, NetAPI/WTS/LSA, profile/hive, ACL, backup, Broker/Agent. `tests/PolicyChecks`: kiểm tra logic đầu vào chạy được trên Linux. `Install.ps1`: cài dịch vụ; không chứa credential. `docs`: đặc tả, quy trình kiểm thử và trạng thái kiểm chứng.
