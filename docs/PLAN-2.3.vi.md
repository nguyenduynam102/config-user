# Preview 2.3 — bảng user và điều hướng

1. Mặc định lọc Đang bật. Làm mới giữ bộ lọc nhưng bỏ lựa chọn đích, không thao tác ngầm user ẩn.
2. Cột RDP: Mở*, Đóng, Chưa cấp. Dấu * chỉ cấu hình cấp trực tiếp, không khẳng định quyền đăng nhập hiệu lực trước GPO.
3. Lấy FullName từ USER_INFO_23 sẵn có, không thêm một lần gọi API cho mỗi dòng; tìm kiếm cả tên đầy đủ. Tên chưa đặt để trống.
4. Lệnh cần user ẩn khi chưa chọn. Lệnh sửa/đổi tên/mật khẩu/xóa/nhóm chỉ bật khi chọn một user. Lệnh chung luôn dùng được. Các trang khác cũng ẩn lệnh cần user đích; quản lý mapping NAS hiện hữu vẫn dùng dòng mapping riêng.
5. Cột chia độ rộng theo dữ liệu: ưu tiên user/tên đầy đủ/phiên, RDP ngắn; tên đầy đủ xuống dòng và cột có thể kéo chỉnh. Giữ mức tối thiểu, có cuộn ngang khi cửa sổ quá hẹp.
6. Một danh sách chọn chức năng theo nhóm thay dải tab cuộn. Nhóm lệnh tài khoản: thông tin, truy cập, RDP/phiên. Giữ toàn bộ chức năng Desktop/Registry/thư mục/NAS/cấu hình/sao lưu.

Kiểm tra: native fixture Unicode/null FullName và giải phóng bộ nhớ; Windows API thật đọc FullName sau sửa; UI trạng thái ban đầu, nhóm lệnh theo số đích, tìm tên đầy đủ, điều hướng đủ bảy trang, ảnh từng trang. Chạy lại kiểm tra SMB/ACL/tài khoản hiện có; không đổi chức năng phân quyền và không thêm Shutdown.
