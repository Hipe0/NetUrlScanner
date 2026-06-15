# NetURLScanner

NetURLScanner là đồ án lập trình web được xây dựng bằng **ASP.NET Core MVC**, **Entity Framework Core 8.0** và **SQL Server**. Hệ thống cho phép người dùng nhập một URL bất kỳ để kiểm tra trạng thái hoạt động, đo thời gian phản hồi và đánh giá mức độ rủi ro dựa trên các rule phân tích bảo mật.

Ngoài chức năng quét URL cơ bản, hệ thống còn hỗ trợ phát hiện các dấu hiệu giả mạo thương hiệu, lookalike domain, punycode, Unicode bất thường, TLD rủi ro và các URL liên quan đến nội dung rủi ro cao như cá cược/cờ bạc trực tuyến.

## Chức năng chính

- Quét URL và kiểm tra trạng thái hoạt động.
- Ghi nhận HTTP status code như 200, 301, 302, 403, 404, 500.
- Đo thời gian phản hồi của URL.
- Kiểm tra URL có sử dụng HTTPS hay không.
- Phân loại trạng thái URL: Online, Redirect, Client Error, Server Error, Offline.
- Đánh giá rủi ro bằng phương pháp rule-based scoring.
- Phát hiện các dấu hiệu đáng ngờ như URL quá dài, query string bất thường, domain có nhiều dấu gạch ngang, nhiều subdomain hoặc sử dụng IP thay domain.
- Phát hiện lookalike domain như `go0gle.com`, `faceb00k.com`, `paypa1.com`.
- Kiểm tra Unicode bất thường, punycode và TLD rủi ro như `.xyz`, `.top`, `.click`, `.site`, `.online`, `.vip`.
- Phát hiện URL có dấu hiệu liên quan đến cá cược/cờ bạc và cảnh báo nguy cơ mất tài sản, lừa đảo tài chính hoặc thu thập thông tin cá nhân.
- Đối chiếu URL với danh sách thương hiệu/domain uy tín trong nhiều lĩnh vực như ngân hàng, ví điện tử, thương mại điện tử, chứng khoán, dịch vụ công, vận chuyển và nền tảng công nghệ phổ biến.
- Quản lý danh sách Whitelist (URL uy tín) và Blacklist (Domain độc hại).
- Tự động phân giải địa chỉ IP máy chủ của URL được quét.
- Tích hợp xác định vị trí địa lý máy chủ (Quốc gia, Thành phố) và Nhà mạng quản lý (ISP).
- Hiển thị bản đồ máy chủ địa lý tương tác bằng thư viện **Leaflet.js**.
- Hiển thị ảnh chụp xem trước website trực quan chất lượng cao trong khung **Mockup Trình duyệt có thanh cuộn**.
- Lưu lịch sử quét và hiển thị báo cáo phân tích chi tiết.
- Hiển thị biểu đồ thống kê mức độ rủi ro trực quan bằng **Chart.js** trên trang lịch sử.
- Giao diện tối ưu hiện đại hỗ trợ chuyển đổi chế độ **Sáng/Tối (Light/Dark Mode)** toàn diện.

## Cách hệ thống quét URL

Khi người dùng nhập URL và bấm **Scan**, hệ thống sẽ thực hiện các bước:

1. Chuẩn hóa URL. Nếu người dùng nhập `google.com`, hệ thống tự động chuyển thành `https://google.com`.
2. Sử dụng `HttpClient` trong ASP.NET Core để gửi request đến URL.
3. Đo thời gian phản hồi bằng bộ đếm thời gian.
4. Ghi nhận HTTP status code.
5. Phân loại trạng thái URL thành Online, Redirect, Client Error, Server Error hoặc Offline.
6. Lấy danh sách thương hiệu uy tín mặc định và danh sách domain uy tín được lưu trong SQL Server.
7. Áp dụng các rule phân tích rủi ro.
8. Lưu kết quả vào SQL Server.
9. Hiển thị kết quả cho người dùng trên giao diện web.

## Cách tính điểm rủi ro

NetURLScanner sử dụng phương pháp **rule-based scoring**. Mỗi dấu hiệu bất thường sẽ được cộng điểm rủi ro. Tổng điểm tối đa là 100.

| Nhóm rule | Dấu hiệu kiểm tra | Điểm cộng |
|---|---|---:|
| Bảo mật kết nối | URL không sử dụng HTTPS | +15 |
| Trạng thái hoạt động | URL không phản hồi hoặc bị lỗi kết nối | +35 |
| Chuyển hướng | URL trả về mã redirect như 301 hoặc 302 | +10 |
| Lỗi máy chủ | URL trả về lỗi nhóm 5xx | +15 |
| Hiệu năng | Thời gian phản hồi lớn hơn 3 giây | +10 |
| Hiệu năng | Thời gian phản hồi lớn hơn 7 giây | +15 |
| Cấu trúc URL | URL quá dài, lớn hơn 100 ký tự | +10 |
| Cấu trúc URL | Query string dài bất thường | +10 |
| Cấu trúc domain | Domain có nhiều dấu gạch ngang | +10 |
| Cấu trúc domain | Domain có quá nhiều subdomain | +15 |
| Địa chỉ IP | URL sử dụng địa chỉ IP thay vì tên miền | +20 |
| Port bất thường | URL sử dụng port không phổ biến như 8080, 4444 | +10 |
| Từ khóa đáng ngờ | URL chứa `login`, `verify`, `account`, `password`, `otp`, `token`, `payment`... | +8 mỗi từ |
| Đường dẫn nhạy cảm | Path chứa `admin`, `wp-admin`, `phpmyadmin`, `shell`, `cmd`... | +12 mỗi từ |
| Homograph Attack | Domain chứa ký tự Unicode bất thường | +30 |
| Punycode | Domain chứa tiền tố `xn--` | +25 |
| Giả mạo thương hiệu | Domain chứa hoặc gần giống thương hiệu uy tín nhưng không phải domain chính thức | +25 đến +35 |
| Brand trong path/query | URL chứa tên thương hiệu trong đường dẫn hoặc tham số nhưng domain thật không phải domain chính thức | +35 |
| Lookalike domain | Domain gần giống thương hiệu nổi tiếng, ví dụ `go0gle.com` | +25 đến +30 |
| TLD rủi ro | Domain dùng đuôi như `.xyz`, `.top`, `.click`, `.site`, `.online`, `.vip` | +10 |
| Nội dung rủi ro cao | URL chứa từ khóa liên quan đến cá cược/cờ bạc như `bet`, `casino`, `gambling`, `nhacai`, `taixiu`, `f8bet`... | +60 |
| Rủi ro tài chính | URL cá cược/cờ bạc có liên quan đến đăng nhập, tài khoản, nạp tiền, rút tiền hoặc giao dịch tài chính | +20 |

## Phân loại mức rủi ro

| Điểm | Mức rủi ro | Ý nghĩa |
|---:|---|---|
| 0 - 30 | Safe | URL có ít hoặc không có dấu hiệu bất thường. |
| 31 - 60 | Warning | URL có một số dấu hiệu cần chú ý. |
| 61 - 100 | Suspicious | URL có nhiều dấu hiệu rủi ro, cần kiểm tra kỹ trước khi truy cập. |

## Danh sách thương hiệu uy tín

NetURLScanner sử dụng danh sách thương hiệu/domain uy tín để phát hiện các URL có dấu hiệu giả mạo hoặc nhìn giống domain chính thức.

Danh sách mặc định bao gồm nhiều lĩnh vực người dùng Việt Nam thường truy cập:

- Ngân hàng: Vietcombank, BIDV, VietinBank, Agribank, Techcombank, MB Bank, ACB, Sacombank, VPBank, TPBank, VIB, HDBank, OCB, MSB, SHB...
- Ví điện tử và thanh toán: MoMo, ZaloPay, VNPAY, Viettel Money.
- Thương mại điện tử: Shopee, Lazada, Tiki, Sendo.
- Chứng khoán và tài chính giao dịch: SSI, VNDirect, VPS, TCBS, HSC, VCBS, MBS, DNSE, FPTS.
- Dịch vụ công: Cổng Dịch vụ công, Tổng cục Thuế, Thuế điện tử, Bảo hiểm xã hội, VNeID, Ngân hàng Nhà nước Việt Nam.
- Công nghệ, mạng xã hội và vận chuyển: Google, Facebook, Microsoft, Apple, GitHub, PayPal, Zalo, YouTube, Viettel Post, GHN, GHTK, Grab.

Danh sách này không dùng để khẳng định URL chắc chắn an toàn. Mục tiêu chính là phát hiện các URL có dấu hiệu giả mạo như:

- `vietcombank-login.xyz`
- `momo-verify.top`
- `shopee-gift.click`
- `paypa1.com`
- `tichphan.vip/DoidiemVietcombank`

## Quản lý URL uy tín

Hệ thống hỗ trợ trang quản lý URL/domain uy tín. Người dùng có thể thêm domain chính thức của một thương hiệu vào hệ thống.

Khi thêm domain uy tín, hệ thống sẽ kiểm tra:

- Domain có bị rỗng hay không.
- Domain có chứa `http://` hoặc `https://` hay không.
- Domain có chứa đường dẫn như `/login` hay không.
- Domain có chứa query string như `?id=123` hay không.
- Domain có chứa khoảng trắng hay ký tự không hợp lệ hay không.
- Domain có đúng định dạng hay không.
- Domain đã tồn tại trong danh sách mặc định hay chưa.
- Domain đã được lưu trong SQL Server hay chưa.

Nếu domain không hợp lệ, hệ thống sẽ hiển thị lý do cụ thể, ví dụ:

```text
Chỉ nhập domain, không nhập giao thức http:// hoặc https://. Ví dụ đúng: vietcombank.com.vn

hoặc:

Chỉ nhập domain chính, không nhập đường dẫn phía sau. Ví dụ đúng: vietcombank.com.vn
- C#
- ASP.NET Core MVC (.NET 8.0)
- Entity Framework Core 8.0 (Code First)
- SQL Server LocalDB / SQL Server Management Studio
- Bootstrap 5.3 & Bootstrap Icons
- Thư viện bản đồ tương tác **Leaflet.js**
- Dịch vụ xác định vị trí địa lý **ip-api.com** (IP Geolocation)
- Dịch vụ ảnh chụp website **WordPress mShots API**
- Thư viện biểu đồ **Chart.js**
- HTML, CSS, JavaScript (Razor View)
- Visual Studio / VS Code
- Git và GitHub
Kiến trúc tổng quát
ASP.NET Core MVC
        |
        | Controller
        v
UrlScannerService
        |
        | Rule-based Scoring
        v
Entity Framework Core 8.0
        |
        v
SQL Server
Cấu trúc project
NetUrlScanner
├── Controllers
│   ├── UrlScannerController.cs
│   ├── TrustedBrandsController.cs
│   ├── BlacklistedDomainsController.cs
│   └── HomeController.cs
├── Data
│   └── ApplicationDbContext.cs
├── Models
│   ├── UrlScan.cs
│   ├── RiskResult.cs
│   ├── TrustedBrand.cs
│   └── BlacklistedDomain.cs
├── Services
│   ├── UrlScannerService.cs
│   └── TrustedBrandDefaults.cs
├── Views
│   ├── UrlScanner
│   ├── TrustedBrands
│   ├── BlacklistedDomains
│   ├── Home
│   └── Shared
├── wwwroot
│   ├── css
│   ├── js
│   └── lib
├── Program.cs
├── appsettings.json
└── README.md
Một số URL dùng để kiểm thử
URL hợp lệ / ít rủi ro
https://google.com
https://github.com
https://vietcombank.com.vn
https://momo.vn
https://shopee.vn
URL giả mạo / đáng ngờ
https://go0gle.com
https://paypa1.com
https://vietcombank-login.xyz
https://momo-verify.top
https://shopee-gift.click
https://tichphan.vip/DoidiemVietcombank
URL cá cược/cờ bạc
https://f8bet.vn
https://casino-online.xyz
https://casino-login-payment.xyz
## Các tính năng nâng cao (Mới cập nhật)

### Đăng nhập, Đăng ký và Phân quyền 
- **Cơ chế xác thực (Cookie Authentication):** Sử dụng xác thực bằng Cookie an toàn. Khi người dùng đăng nhập, hệ thống lưu một "Cookie" để định danh phiên làm việc.
- **Mã hoá Mật khẩu:** Tất cả mật khẩu đều được băm (hash) bằng công nghệ `PasswordHasher` của ASP.NET Core Identity. Mật khẩu lưu vào cơ sở dữ liệu sẽ ở dạng mã hóa không thể đảo ngược, đảm bảo an toàn tối đa chống lại các cuộc tấn công dò mật khẩu.
- **Phân 3 cấp quyền:**
  - **Admin (Tài khoản duy nhất):** Có toàn quyền truy cập. Mặc định là `admin123@gmail.com`. Admin có thêm tính năng **Phân quyền** để quản lý cấp bậc của những tài khoản khác. Hệ thống chặn việc tạo nhiều Admin để đảm bảo an toàn.
  - **Manager:** Được cấp phép quản lý danh sách Whitelist, Blacklist, xem Lịch sử chung.
  - **User:** Chỉ được phép Quét URL và xem lịch sử, không có quyền vào các trang cấu hình hệ thống.
  - **Guest (Chưa đăng nhập):** Chỉ được phép thao tác ở màn hình Quét URL cơ bản. Các thanh điều hướng (Navbar) tự động thay đổi, ẩn đi các tính năng nhạy cảm.

### Thông báo nổi (Toast Notifications) với AJAX
- Tính năng xóa trong Lịch sử quét đã được nâng cấp bằng kỹ thuật **AJAX (Asynchronous JavaScript and XML)** qua hàm `fetch()`.
- Thay vì phải tải lại (reload) toàn bộ trang web khi xóa một bản ghi, hệ thống sẽ xóa ngầm. Sau đó, màn hình hiển thị một **Bootstrap Toast** (thông báo nhỏ trượt ra ở góc màn hình) màu xanh lá để báo hiệu thành công, đồng thời hàng chứa dữ liệu bị xóa sẽ mờ dần và tự động biến mất một cách mượt mà.

### Xem Chi tiết Quét bằng Modal (Popup)
- Hệ thống không cần chuyển hướng sang trang web khác để xem chi tiết kết quả quét như trước đây.
- Các thông tin chi tiết (Mức độ rủi ro, Thông tin Server, Lý do rủi ro) được thiết kế thành một **Partial View**. Khi người dùng nhấn nút "Chi tiết", AJAX sẽ tải nội dung của Partial View này và đắp trực tiếp lên một **Bootstrap Modal** (Hộp thoại Popup) ngay tại trang hiện tại, tạo cảm giác chuyên nghiệp giống như một ứng dụng (Single-Page Application).

### Xuất Báo Cáo sang PDF
- Tích hợp bộ thư viện **iText7** tiên tiến nhất.
- Tại cửa sổ Modal chi tiết, người dùng có nút **Xuất báo cáo PDF**. Khi nhấn vào, Controller sẽ tạo một tập tin PDF động ngay tức thì chứa đầy đủ mọi thông số bảo mật, điểm rủi ro, và chi tiết IP/Quốc gia của Server.
- Đặc biệt, hệ thống đã được tinh chỉnh để tải **Font Arial (Windows)** và mã hóa **IDENTITY_H**, giúp hiển thị tiếng Việt có dấu chuẩn xác 100% khi in thành file báo cáo, phục vụ trực tiếp cho các tác vụ kiểm tra an ninh mạng.

## Thành viên thực hiện
Hồ Đắc Hiệp
Trần Gia Bảo
Phan Gia Bảo

## Ghi chú
Kết quả đánh giá của NetURLScanner dựa trên các rule đã định nghĩa. Hệ thống không khẳng định tuyệt đối một URL là an toàn hoặc độc hại, mà cung cấp điểm rủi ro để người dùng có thêm cơ sở xem xét trước khi truy cập.