# NetURLScanner

NetURLScanner là đồ án lập trình web được xây dựng bằng **ASP.NET Core MVC**, **Entity Framework Core 8.0** và **SQL Server**. Hệ thống cho phép người dùng nhập một URL bất kỳ để kiểm tra trạng thái hoạt động, đo thời gian phản hồi và đánh giá mức độ rủi ro dựa trên các rule phân tích bảo mật.

Ngoài chức năng quét URL cơ bản, hệ thống còn hỗ trợ phát hiện các dấu hiệu giả mạo thương hiệu, lookalike domain, punycode, Unicode bất thường, TLD rủi ro và các URL liên quan đến nội dung rủi ro cao như cá cược/cờ bạc trực tuyến.

## Chức năng chính

- Quét URL và kiểm tra trạng thái hoạt động (kể cả khi chưa đăng nhập).
- Ghi nhận HTTP status code như 200, 301, 302, 403, 404, 500.
- Đo thời gian phản hồi của URL.
- Kiểm tra URL có sử dụng HTTPS hay không.
- Phân loại trạng thái URL: Online, Redirect, Client Error, Server Error, Offline.
- Đánh giá rủi ro bằng phương pháp rule-based scoring.
- Phát hiện lookalike domain, punycode, Unicode bất thường, TLD rủi ro.
- Đối chiếu Whitelist (thương hiệu uy tín) và Blacklist (domain độc hại).
- Tra cứu IP, vị trí địa lý (ip-api.com) và hiển thị bản đồ **Leaflet.js**.
- Lịch sử quét với filter, phân trang, biểu đồ **Chart.js**.
- Kết quả quét hiển thị inline trên trang `/Scan` (tiến trình 5 bước + báo cáo nhanh).
- Đăng ký / đăng nhập, phân quyền 3 cấp (Admin, Manager, User).
- Modal xem chi tiết, xóa AJAX + toast, xuất báo cáo **PDF** (iText7).
- REST API + Swagger (chỉ Admin đăng nhập mới mở được giao diện Swagger).
- Giao diện **Light/Dark Mode**.

## Cài đặt và chạy project

### Yêu cầu

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (hoặc .NET mới hơn với `RollForward` — xem `global.json`)
- SQL Server LocalDB hoặc SQL Server
- Visual Studio 2022 / VS Code / Rider

### Các bước

```bash
git clone https://github.com/Hipe0/NetUrlScanner.git
cd NetUrlScanner
dotnet restore
dotnet ef database update
dotnet run --launch-profile http
```

Mở trình duyệt: **http://localhost:5213**

### Cấu hình kết nối database

Chỉnh `ConnectionStrings:DefaultConnection` trong `appsettings.json` nếu cần:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=NetURLScannerDb;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

### Tài khoản Admin mặc định

Admin được tạo tự động lần đầu chạy app (nếu chưa tồn tại), cấu hình trong `appsettings.json`:

```json
"AdminSeed": {
  "Enabled": true,
  "Email": "admin123@gmail.com",
  "Password": "admin123"
}
```

**Cách hoạt động:** Thông tin trên chỉ dùng để **tạo admin lần đầu** khi chạy app (nếu email chưa có trong DB). Sau khi tạo, tài khoản **lưu trong database** (`AppUsers`) — đăng nhập bình thường qua `/Account/Login`, không đọc mật khẩu từ `appsettings` mỗi lần login.

**Quan trọng:** Đổi `Email` và `Password` trước khi deploy công khai. Đặt `Enabled: false` sau khi đã có admin thật.

## Phân quyền người dùng

| Role | Quét URL | Lịch sử / PDF | Whitelist / Blacklist | Phân quyền | Swagger UI |
|---|---|---|---|---|---|
| Guest | Có | Không | Không | Không | Không |
| User | Có | Có | Không | Không | Không |
| Manager | Có | Có | Có | Không | Không |
| Admin | Có | Có | Có | Có | Có |

- **Đăng ký** (`/Account/Register`) luôn tạo tài khoản **User**.
- Chỉ có **một Admin**; Admin không thể tự hạ quyền hoặc tạo thêm Admin qua trang Phân quyền.

## REST API

Hệ thống có hai nhóm endpoint (dùng chung cookie đăng nhập với MVC):

### API v1 (chuẩn hóa, có `ApiResponse<T>`)

| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| POST | `/api/v1/scans` | Khách (AllowAnonymous) | Quét URL |
| GET | `/api/v1/scans` | User+ | Lịch sử (phân trang, lọc) |
| GET | `/api/v1/scans/stats` | User+ | Thống kê |
| GET | `/api/v1/scans/{id}` | User+ | Chi tiết |
| DELETE | `/api/v1/scans/{id}` | User+ | Xóa |
| GET | `/api/v1/health` | Công khai | Health check |

### API flat (JSON trực tiếp)

| Method | Endpoint | Quyền |
|---|---|---|
| POST | `/api/scans` | Khách |
| GET/DELETE | `/api/scans`, `/api/scans/{id}` | User+ |
| CRUD | `/api/trusted-brands` | Manager+ |
| CRUD | `/api/blacklisted-domains` | Manager+ |

**Swagger UI:** `/swagger` (và `/api/docs` ở Development) — chỉ **Admin** đã đăng nhập.

## Cách hệ thống quét URL

1. Chuẩn hóa URL (`google.com` → `https://google.com`).
2. Gửi HTTP request, đo thời gian phản hồi.
3. Ghi nhận status code và phân loại trạng thái.
4. Đối chiếu Whitelist / Blacklist.
5. Áp dụng rule phân tích rủi ro.
6. Tra cứu IP và vị trí địa lý.
7. Lưu kết quả vào SQL Server.
8. Hiển thị kết quả trên giao diện (inline hoặc modal).

## Cách tính điểm rủi ro

NetURLScanner sử dụng **rule-based scoring**. Mỗi dấu hiệu bất thường được cộng điểm (tối đa 100).

| Nhóm rule | Ví dụ | Điểm cộng |
|---|---|---:|
| Bảo mật kết nối | Không HTTPS | +15 |
| Trạng thái | Offline / lỗi kết nối | +35 |
| Chuyển hướng | 301, 302 | +10 |
| Hiệu năng | > 3s / > 7s | +10 / +15 |
| Cấu trúc URL | Quá dài, query dài, nhiều subdomain | +10–15 |
| Giả mạo thương hiệu | Domain/path giả mạo brand | +25–40 |
| Lookalike | `go0gle.com`, `paypa1.com` | +25–30 |
| TLD rủi ro | `.xyz`, `.top`, `.click`... | +5–12 |
| Cá cược / cờ bạc | `bet`, `casino`, `f8bet`... | +60 |

### Phân loại mức rủi ro

| Điểm | Mức | Ý nghĩa |
|---:|---|---|
| 0–25 | Safe | Ít dấu hiệu bất thường |
| 26–55 | Warning | Cần chú ý |
| 56–100 | Suspicious | Nhiều dấu hiệu rủi ro |

## Công nghệ sử dụng

- C# / ASP.NET Core MVC (.NET 8.0)
- Entity Framework Core 8.0 (Code First)
- SQL Server / LocalDB
- Cookie Authentication + `PasswordHasher`
- Bootstrap 5.3, Bootstrap Icons
- Leaflet.js, Chart.js
- iText7 (xuất PDF)
- Swashbuckle (Swagger / OpenAPI)
- ip-api.com (geolocation)

## Kiến trúc tổng quát

```
ASP.NET Core MVC
        │
        ├── Controllers (MVC + API)
        ├── UrlScannerService (rule-based scoring)
        ├── Entity Framework Core
        └── SQL Server
```

## Cấu trúc project

```
NetUrlScanner/
├── Controllers/
│   ├── AccountController.cs      # Đăng nhập / đăng ký
│   ├── UserController.cs           # Phân quyền (Admin)
│   ├── UrlScannerController.cs     # Quét URL, lịch sử, PDF
│   ├── TrustedBrandsController.cs  # Whitelist (MVC)
│   ├── BlacklistedDomainsController.cs
│   ├── HomeController.cs
│   └── Api/
│       ├── ScansApiController.cs
│       ├── TrustedBrandsApiController.cs
│       ├── BlacklistedDomainsApiController.cs
│       └── V1/
│           ├── ScansController.cs
│           └── HealthController.cs
├── Data/
│   └── ApplicationDbContext.cs
├── Models/
│   ├── User.cs
│   ├── UrlScan.cs
│   ├── TrustedBrand.cs
│   ├── BlacklistedDomain.cs
│   └── Api/                        # DTO, ApiResponse
├── Options/
│   └── AdminSeedOptions.cs
├── Services/
│   ├── UrlScannerService.cs
│   ├── AdminSeedService.cs
│   └── TrustedBrandDefaults.cs
├── Views/
│   ├── Account/
│   ├── User/
│   ├── UrlScanner/
│   ├── TrustedBrands/
│   ├── BlacklistedDomains/
│   ├── Home/
│   └── Shared/
├── wwwroot/
├── Migrations/
├── Program.cs
├── appsettings.json
└── README.md
```

## URL kiểm thử

**Ít rủi ro:** `https://google.com`, `https://vietcombank.com.vn`, `https://momo.vn`

**Đáng ngờ:** `https://go0gle.com`, `https://vietcombank-login.xyz`, `https://momo-verify.top`

**Cá cược / cờ bạc:** `https://f8bet.vn`, `https://casino-online.xyz`

## Các tính năng nâng cao

### Đăng nhập, đăng ký và phân quyền
- Cookie Authentication, mật khẩu băm bằng `PasswordHasher`.
- Ba cấp: Admin, Manager, User; Guest chỉ quét URL cơ bản.

### Toast + AJAX
- Xóa lịch sử không reload trang; thông báo Bootstrap Toast.

### Modal chi tiết
- Partial View `_ScanDetails.cshtml` tải qua AJAX vào Bootstrap Modal.

### Xuất PDF
- Nút trong modal Lịch sử; font Arial + `IDENTITY_H` cho tiếng Việt (Windows).

## Thành viên thực hiện

- Hồ Đắc Hiệp
- Trần Gia Bảo
- Phan Gia Bảo

## Ghi chú

Kết quả đánh giá dựa trên rule đã định nghĩa. Hệ thống không khẳng định tuyệt đối URL an toàn hay độc hại — chỉ cung cấp điểm rủi ro để tham khảo trước khi truy cập.
