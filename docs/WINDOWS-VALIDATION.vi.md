# Bằng chứng Preview 2.2

Mã ứng dụng: `3093b5e1dfbafd66be8d958b0f8d7263b8c21574` (tiếp tục từ bản 2.1; không dùng giao diện bản 3). [CI Windows bản cuối](https://github.com/nguyenduynam102/config-user/actions/runs/40015948148).

Phạm vi kiểm tra tự động:

- 39 ca chính sách đầu vào; 5 ca hồi quy native-contract NetAPI trên Linux.
- 11 ca API/giao diện Windows: danh sách thật, các nút tài khoản/RDP/NAS, cột trạng thái RDP, nguồn không tự chọn, chọn nhiều bằng bôi đen, checkbox độc lập, lọc bỏ đích ẩn, không có nút Shutdown. Ảnh từng tab được lưu trong artifact CI.
- 11 nhóm kiểm tra tích hợp trên Windows VM dùng một lần: tạo user, tên đầy đủ/mô tả/flags, đổi tên giữ SID, đặt lại mật khẩu rồi đăng nhập thật, khóa/mở/gỡ lockout, RDP grant/deny, xóa; xuất ZIP và thư mục thường không mang ACL nguồn; chống đích trong nguồn/ghi đè; file 17 MiB không bị cắt; DPAPI; thêm trùng NAS; sửa giữ mật khẩu; dịch vụ Broker thật và Agent dưới hai user thường, cô lập mapping theo SID, SMB đọc/ghi thật; tên ổ xuất hiện đúng HKCU user; user khác đọc bản sao nhưng không đọc nguồn riêng; xóa mapping giữ dữ liệu NAS.

Không thực hiện Shutdown theo yêu cầu cập nhật của người dùng. Kiểm tra Windows-hosted CI không thay thế nghiệm thu Windows 10 Pro, RDP từ máy khác, TrueNAS, GPO, DPI và các ca phục hồi bên dưới. Trạng thái RDP trong app mô tả grant/deny trực tiếp; chưa khẳng định quyền hiệu lực sau mọi chính sách nhóm/miền.

Chia sẻ dữ liệu: dùng **Sao chép ra thư mục chung** hoặc **Xuất ZIP chia sẻ**. Người nhận cần quyền nơi nhận; app không sửa tự động mọi thao tác Explorer, và không gỡ EFS/DRM/mật khẩu tài liệu.

---

# Kiểm thử Windows 10 Pro x64 / TrueNAS SCALE

Trạng thái: các ca nghiệm thu Windows 10 Pro bên dưới vẫn chưa chạy đầy đủ. Preview 3 đã bổ sung kiểm tra chỉ đọc UI/NetAPI trên Windows Server của GitHub Actions; phạm vi và bằng chứng được tách riêng ở cuối tài liệu. Không nghiệm thu v1.0 trước khi ghi bằng chứng vào bảng này. Cần một VM Windows 10 Pro x64 có snapshot, tài khoản quản trị dự phòng, một máy khách RDP và TrueNAS SMB thử nghiệm. Không thử trên dữ liệu sản xuất.

| Nhóm | Ca kiểm thử, kết quả bắt buộc | Trạng thái |
|---|---|---|
| Khởi động | EXE self-contained mở trên máy không có Visual Studio/.NET; UAC đúng; GUI không bị cắt ở DPI 100/150/200% | Chưa chạy |
| Cài đặt | Install.cmd trên Windows PowerShell 5.1, đường dẫn có khoảng trắng; service chạy LocalSystem; user thường không sửa được EXE/dữ liệu broker | Chưa chạy |
| User | Liệt kê SID đúng; tạo user thường; mật khẩu không đạt policy báo lỗi; không tự thêm Administrators; tên tiếng Việt | Chưa chạy |
| Bảo vệ admin | Từ chối khóa/gỡ RDP của admin đang chạy ứng dụng, tài khoản RID tích hợp; không mất quyền dự phòng | Chưa chạy |
| RDP độc lập | Tắt RDP gán deny theo SID, không disable account; bật gỡ deny trực tiếp và thêm nhóm; quyền từ nhóm/domain có cảnh báo | Chưa chạy |
| Phiên đang chạy | Mở ứng dụng trong RDP user thử; khóa user; phiên chuyển disconnected, ứng dụng vẫn chạy khi mở lại; không logoff; không ngắt console | Chưa chạy |
| Đa lựa chọn | User thành công và user bị bảo vệ cùng batch: báo riêng từng kết quả, không thông báo tất cả thành công | Chưa chạy |
| Profile mới | Tạo user chưa đăng nhập; Desktop/NTUSER.DAT tạo bằng CreateProfile, owner/quyền đúng; lần đăng nhập đầu dùng được | Chưa chạy |
| Desktop | Chỉ .lnk/.url; loại tài liệu; Public Desktop không đổi; trùng giữ nguyên; cảnh báo đích mất; chọn nhiều đích | Chưa chạy |
| Filesystem | Hardlink/symlink/junction và thay đường dẫn đồng thời bị chặn; không sao chép/ghi/xóa tệp ngoài phạm vi; NT native flags tương thích Win10 | Chưa chạy |
| Desktop restore | Thêm/xóa, khôi phục, khôi phục ngược bản BeforeRestore; giữ dữ liệu không thuộc manifest | Chưa chạy |
| Registry | HKCU user đích thay đổi, admin không đổi; hive loaded và unloaded; file UTF-16 BOM/UTF-8; kiểu SZ/DWORD; rollback giá trị cũ và không tồn tại | Chưa chạy |
| Registry lỗi | HKLM, Run, hex, xóa, file sai bị từ chối trước ghi; hive bận báo lỗi; không để hive tạm bị mount sau lỗi | Chưa chạy |
| NTFS | User owner + Full Control; Administrators/SYSTEM Full; user khác Access Denied; không nhận thư mục tồn tại; không xóa dữ liệu | Chưa chạy |
| Cấu hình | Tạo/đổi tên/sao chép/xóa/restore; apply khi tạo và user hiện hữu; lỗi giữa quy trình báo partial; backup có thể mở lại | Chưa chạy |
| NAS lần đầu | Broker cài trước login; cấu hình trước lần login đầu; Agent tự chạy, ổ hiện trong Explorer user; admin không cần nhập lệnh | Chưa chạy |
| NAS tách biệt | Hai SID cùng Z: khác share; tài khoản phòng ban chung và cá nhân; không trả credential của SID khác | Chưa chạy |
| NAS mất mạng | Login khi NAS tắt, bật lại NAS: retry 30 giây; không treo GUI; báo trạng thái có thời gian | Chưa chạy |
| NAS lỗi | Sai password/deny ACL/ổ trùng/xung đột 1219; báo đúng mã, không ngắt ổ ngoài phạm vi | Chưa chạy |
| NAS sửa/xóa | Đổi password, share, ổ; mở tệp gây busy; Agent restart vẫn nhận mapping do nó quản lý; xóa không xóa dữ liệu NAS | Chưa chạy |
| Broker/Agent | Client thường giả SID không được; máy khác không truy cập pipe; pipe giả PID không được tin; service restart tự phục hồi | Chưa chạy |
| Bí mật | Quét JSON/REG/script/log/commandline không có plaintext NAS password; user thường không đọc được ciphertext/backup broker | Chưa chạy |
| Tài khoản NAS chung | Chứng minh người được cấp lấy được bí mật của chính mình; kiểm thử NAS revoke/rotate; chấp nhận giới hạn bằng văn bản | Chưa chạy |
| Nhật ký | Mỗi thao tác thành công/lỗi có actor/target/time/code; không credential; kiểm tra file status user giả mạo không được dùng để ra quyết định quyền | Chưa chạy |

## Phạm vi chưa đáp ứng đầy đủ đặc tả

- Chưa chứng minh ứng dụng thực thi được trên Windows 10 Pro/TrueNAS; Broker/Agent mới biên dịch, chưa chứng minh khả thi trên đích.
- Preview Desktop chưa thể hiện bố trí tọa độ thực tế. Cần đọc định dạng bố trí Explorer phù hợp từng phiên bản hoặc ghi nhận bằng agent trong phiên nguồn; không suy diễn lưới hiện tại là bố trí thật.
- Import REG giới hạn SZ/DWORD và vùng Software an toàn; chưa nhận đầy đủ kiểu REG_BINARY/MULTI_SZ/EXPAND_SZ, file multiline và khóa khác sau cảnh báo.
- HKLM Run không bảo đảm thứ tự chạy ngay trước ứng dụng user. Nếu yêu cầu ổ có trước ứng dụng đầu tiên, cần đánh giá Scheduled Task/Group Policy đồng bộ đăng nhập trong lab; không có bảo đảm mạng luôn sẵn sàng.
- Chưa có bộ kiểm thử Windows tự động, chứng thư ký EXE, bộ cài MSI, tối ưu GUI cho danh sách hàng nghìn tài khoản hay bảo đảm phục hồi giao dịch nhiều bước.
- Giấu tên thư mục local bằng NTFS không được bảo đảm; ACL bảo vệ nội dung. Không thay ACL TrueNAS hoặc vá giới hạn RDP.

Không đánh dấu các dòng trên đã đạt khi chỉ chạy kiểm tra parser hoặc chỉ nhìn thấy cửa sổ GUI.

## Hồi quy Preview 2

- Windows trước đó đã mở được giao diện nhưng danh sách user lỗi “The system call level is not correct” (ảnh do người dùng cung cấp).
- Nguyên nhân trong mã: NetUserEnum level 23 không được API hỗ trợ. Đổi sang Enum level 0 + GetInfo level 23; sửa resume handle nhóm thành UIntPtr cho x64.
- Đã chạy trên Linux: 39 policy checks + 5 native-contract checks với fixture C; build/publish x64. Fixture từ chối enum level khác 0 và getinfo khác 23, kiểm tra trả về nhiều trang/lỗi và số buffer chưa giải phóng.
- Còn phải chạy trên Windows: mở Preview 2, danh sách user hiển thị đúng sau khởi động/Làm mới; nhóm RDP và trạng thái bật khớp Local Users and Groups; không thay đổi tài khoản chỉ khi mở giao diện.
- Tài liệu API: https://learn.microsoft.com/en-us/windows/win32/api/lmaccess/nf-lmaccess-netuserenum và https://learn.microsoft.com/en-us/windows/win32/api/lmaccess/nf-lmaccess-netlocalgroupgetmembers

## Preview 3 — phạm vi kiểm chứng bổ sung

- Linux: 49 policy/selection checks; 10 native-contract checks. Fixture kiểm tra giữ cờ disabled khi xóa lockout, giữ cờ lockout khi bật tài khoản, USER_INFO_1003 UTF-16 cho mật khẩu và giải phóng buffer khi lỗi.
- Workflow `.github/workflows/windows-ui.yml` chạy trên `windows-latest` (Windows Server, không phải Windows 10 Pro), quyền repository chỉ đọc.
- Kiểm tra chỉ đọc: NetAPI/LSA thật trả danh sách, mở MainForm thật, không tự chọn đích, click xem không chọn, bôi đen→tick, giữ đích ẩn khi lọc, nguồn xổ xuống tìm kiếm, nguồn không thành đích Desktop, bảo vệ admin hiện tại, giữ lựa chọn khi refresh và nút hộp mật khẩu không bị cắt. Không tạo/xóa user, đổi mật khẩu, đổi RDP hoặc ngắt phiên trong CI. Một kiểm tra riêng tạo tệp fixture trong thư mục quản trị tạm, xác minh SafeFile đọc/copy/xóa trên Windows rồi dọn đúng thư mục fixture đã tạo; không đụng dữ liệu cá nhân.
- Workflow chụp ảnh tại hai kích thước cửa sổ và lưu artifact windows-ui-evidence. Chụp ảnh không đồng nghĩa đã kiểm tra bằng mắt hoặc đã đạt mọi mức DPI.
- Bằng chứng CI ban đầu: https://github.com/nguyenduynam102/config-user/actions/runs/39931069035 (success). Bản tinh chỉnh cuối: https://github.com/nguyenduynam102/config-user/actions/runs/39932349733 (success, 14 kiểm tra Windows). Kết quả được ghi trong VALIDATION.json của gói phát hành.

### Còn cần thử trên Windows 10 Pro

1. Mở EXE self-contained Preview 3 với UAC, DPI 100/150/200%, resize nhỏ/lớn, không cắt nút và văn bản.
2. Tick user A/B, lọc chỉ A: thanh đích phải báo B ẩn, xác nhận phải liệt kê cả A/B. Click tên C chỉ xem, không vô tình chọn C. Bỏ toàn bộ xóa cả đích ẩn.
3. Desktop chọn nguồn A bằng gõ tên hoặc lấy user đang xem, tick A/B: thao tác chỉ áp dụng B. Đổi nguồn phải đọc lại danh sách; không sao chép shortcut của nguồn cũ.
4. User thử nghiệm: đổi mật khẩu riêng/chung đúng/sai xác nhận, policy từ chối có mã lỗi; không xuất mật khẩu trong audit/settings. Không thử reset EFS trên dữ liệu thật.
5. Vô hiệu hóa khác lockout do nhập sai: mở lockout không được bật tài khoản bị vô hiệu hóa; bật tài khoản không tự gỡ deny RDP.
6. Ngắt phiên riêng: user vẫn enabled, RDP policy không đổi, ứng dụng trong phiên còn chạy. Khóa tài khoản: disabled + disconnect, không logoff.
7. Batch nhiều user: giao diện không treo, dừng chỉ bỏ các user chưa chạy; kết quả phân biệt thành công/lỗi/bỏ qua/chưa chạy. Đóng cửa sổ khi đang làm phải chờ bước hiện tại, không cắt ngang.
8. Kiểm tra RDP không thay firewall/Registry. Rule allow tìm thấy không được coi là chứng minh truy cập hiệu lực khi có block/GPO/NLA/network issue.
9. Khởi động lại app: nhớ cửa sổ/cột/bộ lọc, không tự chọn user hoặc nạp lại mật khẩu cũ.

## Preview 2.1 — rollback và phạm vi thử mới

Theo yêu cầu người dùng, khôi phục giao diện và API quản lý user của tag v1.0.0-preview.2. Các kiểm tra UI Preview 3 không được coi là bằng chứng của phiên bản 2.1. Chương trình test Windows được đổi theo giao diện 2.1 và có fixture SMB/ACL riêng.

Fixture chỉ chạy khi GITHUB_ACTIONS=true trên máy GitHub-hosted dùng một lần. Tạo user A/B/NAS ngẫu nhiên bằng NetAPI, mật khẩu ngẫu nhiên chỉ giữ trong bộ nhớ, share SMB local riêng, cài EXE Broker thật từ gói publish, chạy Agent bằng token user không phải admin và dọn fixture. Không chạy fixture này trên PC sử dụng thật.

Các ca: ZIP metadata không mang ACL, dữ liệu >16 MiB, không ghi đè/không xuất vào trong nguồn; user B không đọc được đường dẫn dữ liệu private của A nhưng đọc được tệp đã giải nén ngoài; password NAS được mã hóa; thêm trùng ổ bị từ chối; sửa không nhập lại password khi cùng server/account; Broker impersonation sau HELLO; A nhận Y/Z, B chỉ X; đọc/ghi thực qua ổ SMB; xung đột ký tự ổ; xóa config không xóa dữ liệu NAS.

Vẫn cần kiểm thử Windows 10 Pro/TrueNAS thực: UAC và bộ cài từ Explorer, user đã RDP khi cài/lưu cấu hình, tự khởi chạy Agent qua WTS token, lần đăng nhập đầu, NAS mất mạng/đổi mật khẩu, SMB credential cache 1219, file đang mở khi xóa mapping, EFS/DRM. Không khẳng định các ca này đã đạt nếu chỉ fixture Windows Server đạt.

Bằng chứng Preview 2.1: https://github.com/nguyenduynam102/config-user/actions/runs/40006966854 — success, source d564e780eebbc93c8fc5395f4226c1b6d23c6b17. 39 input checks, 5 Windows UI checks và 9 integration checks thực thi. Hai tài khoản không phải admin đã chạy Agent thật, gọi Broker service thật và đọc/ghi qua Y/Z hoặc X. User B bị AccessDenied với file private của A nhưng đọc được file đã xuất/giải nén. File 17 MiB đã xuất đầy đủ. Linux chạy thêm 5 native-contract checks cho NetUserEnum. Các ca lỗi của bộ cài và fixture process handle được sửa rồi chạy lại toàn bộ; không tính các lần lỗi là pass.
