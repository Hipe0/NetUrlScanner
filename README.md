# NetURLScanner

NetURLScanner là đồ án lập trình web được xây dựng bằng ASP.NET Core MVC và SQL Server. Hệ thống cho phép người dùng nhập một URL bất kỳ để kiểm tra trạng thái hoạt động, đo thời gian phản hồi và đánh giá mức độ rủi ro dựa trên các rule phân tích bảo mật.

## Chức năng chính

- Quét URL và kiểm tra trạng thái hoạt động.
- Ghi nhận HTTP status code như 200, 301, 302, 403, 404, 500.
- Đo thời gian phản hồi của URL.
- Kiểm tra URL có sử dụng HTTPS hay không.
- Đánh giá rủi ro bằng phương pháp rule-based scoring.
- Phát hiện các dấu hiệu đáng ngờ như URL quá dài, query string bất thường, domain có nhiều dấu gạch ngang, nhiều subdomain hoặc sử dụng IP thay domain.
- Phát hiện lookalike domain như go0gle.com, faceb00k.com, paypa1.com.
- Kiểm tra Unicode bất thường, punycode và TLD rủi ro như .xyz, .top, .click, .site, .online.
- Lưu lịch sử quét vào SQL Server.
- Hiển thị trang chi tiết kết quả quét URL.

## Cách hệ thống quét URL

Khi người dùng nhập URL và bấm Scan, hệ thống sẽ thực hiện các bước:

1. Chuẩn hóa URL. Nếu người dùng nhập `google.com`, hệ thống tự động chuyển thành `https://google.com`.
2. Sử dụng `HttpClient` trong ASP.NET Core để gửi request đến URL.
3. Đo thời gian phản hồi bằng bộ đếm thời gian.
4. Ghi nhận HTTP status code.
5. Phân loại trạng thái URL thành Online, Redirect, Client Error, Server Error hoặc Offline.
6. Áp dụng các rule phân tích rủi ro.
7. Lưu kết quả vào SQL Server.
8. Hiển thị kết quả cho người dùng trên giao diện web.

## Cách tính điểm rủi ro

NetURLScanner sử dụng phương pháp rule-based scoring. Mỗi dấu hiệu bất thường sẽ được cộng điểm rủi ro. Tổng điểm tối đa là 100.

| Nhóm rule | Dấu hiệu kiểm tra | Điểm cộng |
|---|---|---:|
| Bảo mật kết nối | URL không sử dụng HTTPS | +15 |
| Trạng thái hoạt động | URL không phản hồi hoặc bị lỗi kết nối | +20 |
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
| Từ khóa đáng ngờ | URL chứa login, verify, account, bank, password, otp, token... | +8 mỗi từ |
| Đường dẫn nhạy cảm | Path chứa admin, wp-admin, phpmyadmin, shell, cmd... | +12 mỗi từ |
| Homograph Attack | Domain chứa ký tự Unicode bất thường | +30 |
| Punycode | Domain chứa tiền tố `xn--` | +25 |
| Giả mạo thương hiệu | Domain chứa thương hiệu nổi tiếng nhưng không phải domain chính thức | +25 đến +35 |
| Lookalike domain | Domain gần giống thương hiệu nổi tiếng, ví dụ `go0gle.com` | +25 đến +30 |
| TLD rủi ro | Domain dùng đuôi như .xyz, .top, .click, .site, .online | +10 |

## Phân loại mức rủi ro

| Điểm | Mức rủi ro | Ý nghĩa |
|---:|---|---|
| 0 - 30 | Safe | URL có ít hoặc không có dấu hiệu bất thường. |
| 31 - 60 | Warning | URL có một số dấu hiệu cần chú ý. |
| 61 - 100 | Suspicious | URL có nhiều dấu hiệu rủi ro, cần kiểm tra kỹ trước khi truy cập. |

## Công nghệ sử dụng

- C#
- ASP.NET Core MVC
- Entity Framework Core
- SQL Server
- SQL Server Management Studio
- Bootstrap
- HTML, CSS, Razor View
- Visual Studio

## Cấu trúc project

```text
NetUrlScanner
├── Controllers
│   └── UrlScannerController.cs
├── Data
│   └── ApplicationDbContext.cs
├── Models
│   ├── UrlScan.cs
│   └── RiskResult.cs
├── Services
│   └── UrlScannerService.cs
├── Views
│   ├── UrlScanner
│   └── Home
├── wwwroot
├── Program.cs
└── appsettings.json