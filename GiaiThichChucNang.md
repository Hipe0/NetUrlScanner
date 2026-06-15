# Hướng dẫn chi tiết các chức năng đã được thêm vào NetURLScanner

Dưới đây là giải thích chi tiết, đơn giản và dễ hiểu về các chức năng mới được thêm vào. Tất cả code đều được chú thích bằng tiếng Việt trong các file để bạn dễ theo dõi.

## 1. Tính năng Đăng nhập, Đăng ký và Phân quyền (RBAC)

**Mục đích:** Bảo vệ các trang quản lý và lịch sử quét khỏi những người dùng chưa đăng nhập, đồng thời phân cấp quyền hạn rõ ràng.

- **Cookie Authentication:** Đây là cơ chế xác thực nhẹ nhàng và dễ hiểu. Khi bạn đăng nhập thành công, hệ thống sẽ lưu một "Cookie" trên trình duyệt của bạn để nhận diện bạn là ai trong các lần tải trang tiếp theo. Đoạn cấu hình nằm ở file `Program.cs`.
- **Model `User`:** Được thêm vào database để lưu trữ `Email`, `PasswordHash` (mật khẩu đã được mã hóa an toàn), và `Role` (quyền hạn). File cấu trúc nằm ở `Models/User.cs`.
- **Mã hoá Mật khẩu:** Sử dụng `PasswordHasher` của ASP.NET Core. Mật khẩu bạn nhập (ví dụ: `admin123`) sẽ được băm (hash) thành một chuỗi ký tự loằng ngoằng không thể dịch ngược. Logic mã hóa nằm ở `Controllers/AccountController.cs`.
- **Phân 2 cấp bậc (Role):**
  - **Manager:** Quản lý cấp trung, được truy cập trang `Whitelist`, `Blacklist`, Lịch sử, Quét URL. 
  - **User:** Người dùng thường, chỉ được Quét URL và Xem lịch sử cá nhân.
  - **Admin (Tài khoản duy nhất):** Có quyền cao nhất. Khi đăng nhập bằng tài khoản `Admin` (`admin123@gmail.com`), hệ thống sẽ hiển thị thêm nút **"Phân quyền"** trên thanh Menu để Admin có thể thay đổi quyền cho người khác. Chức năng này xử lý ở `Controllers/UserController.cs`.
  - **Chưa đăng nhập:** Chỉ có thể quét URL và xem trang chủ. Các menu khác đều bị ẩn đi (xử lý ẩn ở `Views/Shared/_Layout.cshtml`).
- **Vị trí Nút bấm:** Các nút **Đăng nhập**, **Đăng ký** đã được thiết kế nằm góc phải trên cùng của Navbar, ngay bên cạnh nút Sáng/Tối giống hệt các mạng xã hội.

## 2. Thông báo nổi (Toast Notifications) khi Xóa Lịch sử

**Mục đích:** Tăng trải nghiệm người dùng, xóa mà không bị load lại trang gây khó chịu.

- **AJAX (Asynchronous JavaScript and XML):** Thay vì gửi dữ liệu đi và làm tải lại toàn bộ trang web (cách cổ điển), khi bạn bấm nút "Xóa", một đoạn mã JavaScript `fetch()` (ở cuối file `Views/UrlScanner/History.cshtml`) sẽ âm thầm gọi xuống Controller `Delete` ở dưới ngầm.
- **Bootstrap Toasts:** Sau khi server báo xóa thành công, JavaScript sẽ kích hoạt một cái "Toast" (một hộp thông báo nhỏ màu xanh góc phải dưới màn hình) ghi chữ "Đã xóa thành công", đồng thời dòng dữ liệu vừa bị xóa sẽ mờ dần và tự động biến mất khỏi bảng một cách mượt mà.

## 3. Xem Chi tiết Quét bằng Modal (Popup)

**Mục đích:** Cho phép người dùng xem thông tin chi tiết một cách nhanh chóng mà không phải nhảy sang một trang web khác.

- **Partial View (`_ScanDetails.cshtml`):** Tôi đã tách riêng phần giao diện chứa thông tin chi tiết thành một "view nhỏ" (gọi là Partial View nằm trong thư mục `Views/UrlScanner/`).
- **Cách hoạt động:** Khi bạn bấm "Chi tiết" ở trang lịch sử, một hộp thoại Popup (Bootstrap Modal) sẽ lập tức hiện ra với hiệu ứng "Đang tải...". Cùng lúc đó, AJAX sẽ gọi xuống hàm `DetailsPartial` trong `UrlScannerController` để lấy HTML của giao diện chi tiết và nhúng thẳng vào giữa Modal. 

## 4. Xuất Báo Cáo PDF

**Mục đích:** Xuất thông tin kiểm tra ra file PDF trông thật chuyên nghiệp và có thể dùng để làm báo cáo thực tế.

- **Thư viện `iText7`:** Đây là một công cụ mạnh mẽ dùng để vẽ và tạo file PDF bằng code C#. Gói thư viện này đã được thêm vào file `NetUrlScanner.csproj`.
- **Hỗ trợ Tiếng Việt:** Sử dụng trực tiếp bộ font chữ Arial của Windows (`arial.ttf`) kết hợp với chuẩn mã hóa `IDENTITY_H` để PDF nhận diện hoàn hảo dấu Tiếng Việt.
- **Cách hoạt động:** Bấm nút **Xuất báo cáo PDF** bên trong cửa sổ Modal Chi tiết, hàm `ExportPdf` trong `UrlScannerController` sẽ chạy, lấy toàn bộ dữ liệu của URL đó và vẽ ra văn bản PDF. Cuối cùng, hàm trả file đó về và trình duyệt sẽ tự động tải xuống file `ScanReport.pdf`.
