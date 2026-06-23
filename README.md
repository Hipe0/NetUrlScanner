# NetURLScanner

NetURLScanner là đồ án lập trình web được xây dựng bằng **ASP.NET Core MVC**, **Entity Framework Core 8.0** và **SQL Server**. Hệ thống cho phép người dùng nhập URL để kiểm tra trạng thái hoạt động, đo thời gian phản hồi, tra cứu IP/vị trí và đánh giá mức độ rủi ro dựa trên rule phân tích bảo mật.

Ngoài quét URL cơ bản, hệ thống hỗ trợ quét hàng loạt (CSV/Excel/PDF), quét mã QR, OCR ảnh (Premium), tra cứu blacklist ngân hàng (Premium), báo cáo lừa đảo, chatbox AI và xuất báo cáo đa định dạng.

## Chức năng chính

### Quét & phân tích URL
- Quét URL đơn lẻ tại `/Scan` (không cần đăng nhập).
- **Quét hàng loạt** — dán danh sách hoặc import **CSV, Excel (.xlsx), PDF** (tối đa 30 URL/lần).
- **Quét mã QR** (`/QrScan`) — upload ảnh hoặc camera; nút **Đọc QR & quét URL**; decode server (ZXing) + hiển thị kết quả trên cùng trang (giống OCR).
- Ghi nhận HTTP status (200, 301, 302, 403, 404, 500...), đo thời gian phản hồi, kiểm tra HTTPS.
- Phân loại trạng thái: Online, Redirect, Client Error, Server Error, Offline.
- Đánh giá rủi ro **rule-based scoring**; phát hiện lookalike, punycode, Unicode bất thường, TLD rủi ro.
- Đối chiếu **Whitelist** / **Blacklist**; tra cứu IP & vị trí (**ip-api.com** + **Leaflet.js**).
- Kết quả inline trên `/Scan` (gauge rủi ro + báo cáo nhanh); thống kê cập nhật live sau mỗi lần quét.

### Phân loại nội dung & kiểm tra chéo
- **Crawl HTML** — phân loại tự động (Tin tức, TMĐT, Game, Giáo dục, Công nghệ...) với trọng số title/meta/URL/schema.org.
- **Google Safe Browsing** — kiểm tra chéo malware/phishing (cả gói miễn phí).

### Lịch sử, báo cáo & cộng đồng
- Lịch sử quét tại `/History`: filter, phân trang, biểu đồ **Chart.js**, modal chi tiết (dark mode).
- Xuất **CSV / Excel / PDF**; xuất PDF từng bản ghi trong modal.
- Gắn **nhãn & ghi chú** cá nhân cho từng lượt quét.
- **Upvote/Downvote domain** — đánh giá độ tin cậy từng domain.
- **Báo cáo lừa đảo** — URL/IP hoặc tài khoản ngân hàng, kèm upload ảnh bằng chứng.

### Ngân hàng & chống lừa đảo tài chính
- **Tra cứu ngân hàng** (Premium) tại `/BankAccountChecker` — đối chiếu STK với:
  - Báo cáo lừa đảo đã duyệt (`ScamReports`, loại BankAccount)
  - Bảng **Blacklist Ngân hàng** (`BlacklistedBankAccounts`)
- Tra tên chủ TK qua **VietQR API** (nếu ngân hàng hỗ trợ).
- **Quản lý Blacklist Ngân hàng** (Manager/Admin) tại `/Manager/BlacklistBank` — thêm/sửa/xóa STK lừa đảo, bật/tắt.

### Premium
- **OCR** (Tesseract) — đọc ảnh SMS/email, trích URL và quét tự động.
- **Tra cứu ngân hàng** — đối chiếu STK với báo cáo cộng đồng đã duyệt và blacklist ngân hàng.
- Trang nâng cấp `/Premium` với thanh toán demo (VietQR); kích hoạt qua `CheckoutAjax` (fetch + antiforgery).
- Sau nâng cấp: `User.IsPremium = true`; navbar (`_NavUserInfo`) và `/Profile` hiển thị trạng thái VIP.

### Tài khoản & quản trị
- Đăng ký / đăng nhập (email + **Google OAuth**), phân quyền Admin / Manager / User.
- **Hồ sơ cá nhân** (`/Profile`) — badge **VIP Premium**, card gói dịch vụ, thống kê quét; navbar hiển thị **VIP** cạnh lời chào khi `IsPremium = true`.
- **Kiểm duyệt** (Manager/Admin): Whitelist URL (`/Manager/Whitelist`), Blacklist URL (`/Manager/Blacklist`), Blacklist Ngân hàng (`/Manager/BlacklistBank`), duyệt báo cáo lừa đảo.
- Dashboard, hồ sơ cá nhân, quản lý người dùng (Admin), CMS (Giới thiệu, FAQ), form góp ý.
- **Chatbox AI** (Google Gemini) hỗ trợ người dùng.
- REST API + **Swagger** (chỉ Admin).
- Giao diện **Light/Dark Mode**.
- **Dữ liệu mẫu** (`SampleDataSeed`) — tự tạo user, URL scan, blacklist, scam report demo khi chạy app (bật/tắt trong appsettings).

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
dotnet run --launch-profile http
```

App tự **migrate** database và seed khi khởi động (`Program.cs`). Có thể dùng `dotnet ef database update` thay thế nếu cần migrate thủ công.

### Cấu hình kết nối database

Chỉnh `ConnectionStrings:DefaultConnection` trong `appsettings.json` nếu cần:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=NetURLScannerDb;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

### Khôi phục database từ `database.bak`

Nếu có file **`database.bak`** (cùng cấp `appsettings.json`), restore vào SQL Server / LocalDB để dùng sẵn dữ liệu thay vì tạo DB trống:

1. Đặt `database.bak` ở thư mục gốc project (cạnh `appsettings.json`).
2. Mở **SQL Server Management Studio** (SSMS):
   - Chuột phải **Databases** → **Restore Database…**
   - **Device** → chọn file `database.bak`
   - Đặt tên database: **`NetURLScannerDb`** (khớp connection string bên dưới)
3. Kiểm tra `ConnectionStrings:DefaultConnection` trong `appsettings.json` trỏ đúng instance SQL (mặc định `(localdb)\MSSQLLocalDB`).
4. Chạy `dotnet run` — app kết nối DB đã restore.

Nếu **không** có file `.bak`, dùng `dotnet ef database update` như phần [Các bước](#các-bước) ở trên; app sẽ tự migrate và seed dữ liệu mẫu khi khởi động.

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

### Đăng nhập bằng Google (OAuth)

Khác với API key Safe Browsing — cần **OAuth Client ID** và **Client Secret**.

1. [Google Cloud Console](https://console.cloud.google.com/) → **OAuth consent screen** → cấu hình app (External), thêm scope `email`, `profile`, `openid`.
2. **Credentials** → **Create Credentials** → **OAuth client ID** → **Web application**.
3. **Authorized redirect URIs** — thêm **tất cả** URI khớp cách bạn chạy app (Google chỉ chấp nhận URI đã khai báo, kể cả khác port):

| Cách chạy | Redirect URI |
|-----------|----------------|
| `dotnet run` / profile **http** | `http://localhost:5213/signin-google` |
| Profile **https** (HTTPS) | `https://localhost:7108/signin-google` |
| Profile **https** (HTTP fallback) | `http://localhost:5213/signin-google` *(trùng dòng trên)* |
| **IIS Express** (HTTP) | `http://localhost:35576/signin-google` |
| **IIS Express** (HTTPS) | `https://localhost:44372/signin-google` |

Trong Google Cloud → OAuth client → **Authorized redirect URIs** → **Add URI** từng dòng. Có thể thêm hết 4 URI (5213, 7108, 35576, 44372) để không phải sửa lại khi đổi giữa `dotnet run` và Visual Studio.

> Nếu Visual Studio gán port khác (xem thanh địa chỉ trình duyệt hoặc `launchSettings.json`), thêm `{origin}/signin-google` tương ứng.

4. Copy Client ID + Secret vào `appsettings.json` (hoặc `appsettings.Development.json`):

```json
"GoogleAuth": {
  "Enabled": true,
  "ClientId": "xxx.apps.googleusercontent.com",
  "ClientSecret": "GOCSPX-..."
}
```

Khi app ở chế độ **Testing**, thêm Gmail test user trong OAuth consent screen. User Google mới đăng ký tự động với role **User**; email trùng tài khoản cũ sẽ được liên kết Google.

### Google Safe Browsing API (tùy chọn)

Tạo API key tại [Google Cloud Console](https://console.cloud.google.com/) → bật **Safe Browsing API** → cấu hình:

```json
"GoogleSafeBrowsing": {
  "Enabled": true,
  "ApiKey": "YOUR_API_KEY_HERE"
}
```

Nếu `Enabled: false` hoặc thiếu key, hệ thống vẫn quét bình thường — chỉ hiển thị "Chưa cấu hình API".

### Quét mã QR

Tại **Quét mã QR** (`/QrScan`) — cần đăng nhập. Upload ảnh QR hoặc dùng camera (thư viện `html5-qrcode` trên trình duyệt), bấm nút quét; server dùng **ZXing** (`QrScanService`) decode ảnh, trích URL và gọi `UrlScannerService` — kết quả hiển thị ngay trên trang (không redirect sang `/Scan`). Ảnh upload lưu tại `wwwroot/uploads/qr-images/`.

### Quét ảnh OCR (Tesseract)

Tải ảnh chụp SMS, email hoặc tin nhắn lừa đảo tại **Quét ảnh OCR** (`/OcrScan`). Hệ thống dùng **Tesseract** đọc chữ, trích URL bằng regex và quét từng link (tối đa 10). Ảnh upload lưu riêng trong `wwwroot/uploads/ocr-images/`.

**Dữ liệu ngôn ngữ:** thư mục `tessdata/` ở gốc project (đã có trong repo) gồm `eng.traineddata` và `vie.traineddata`. Khi clone chỉ cần `dotnet run` — không tải thêm.

Cấu hình `appsettings.json`:

```json
"Ocr": {
  "UploadFolder": "ocr-images",
  "TessDataFolder": "tessdata",
  "Languages": "eng+vie",
  "MaxUploadBytes": 5242880,
  "MaxUrlsToScan": 10
}
```

**Lưu ý `dotnet watch`:** Khi app ghi file mới vào `wwwroot/uploads/`, Hot Reload đôi khi crash. Project đã loại `wwwroot/uploads/**` khỏi watch; nếu vẫn lỗi, dùng `dotnet run` thay vì `dotnet watch`.

### Google Gemini (Chatbox AI, tùy chọn)

```json
"Gemini": {
  "ApiKey": "YOUR_API_KEY",
  "Model": "gemini-3.1-flash-lite",
  "ThinkingLevel": "",
  "MaxOutputTokens": 512
}
```

Mặc định **`gemini-3.1-flash-lite`** — model lite ổn định trên API (nhanh, ít 503). Có thể đổi sang `gemini-3.5-flash` nếu cần suy luận mạnh hơn (kèm `ThinkingLevel`, ví dụ `minimal`). Service tự retry khi Google trả 503; timeout HTTP 60 giây.

Nếu thiếu API key, chatbox vẫn hiển thị nhưng không trả lời được.

### Dữ liệu mẫu (SampleDataSeed)

Hữu ích khi demo hoặc phát triển — tự tạo dữ liệu nếu DB còn trống:

```json
"SampleDataSeed": {
  "Enabled": true
}
```

Khi bật, lần chạy app đầu tiên sẽ seed (nếu chưa có):
- 5 user thường (`user1@gmail.com` … `user5@gmail.com`, mật khẩu `123456`)
- 5 user Premium (`premium1@gmail.com` …, mật khẩu `123456`)
- Blacklist domain, Whitelist brand, **Blacklist tài khoản ngân hàng**, báo cáo lừa đảo, lịch sử quét URL mẫu

Đặt `Enabled: false` khi dùng dữ liệu thật hoặc deploy production.

## Phân quyền người dùng

| Role | Quét URL | Hàng loạt / QR | Lịch sử & xuất | OCR / Tra NH | Whitelist / Blacklist URL / NH | Swagger |
|---|---|---|---|---|---|---|
| Guest | Có | Không | Không | Không | Không | Không |
| User | Có | Có | Có | Premium | Không | Không |
| Manager | Có | Có | Có | Premium | Có | Không |
| Admin | Có | Có | Có | Có | Có | Có |

- **Đăng ký** (`/Account/Register`) luôn tạo tài khoản **User**.
- **OCR** và **Tra cứu ngân hàng** yêu cầu **Premium** (Admin/Manager được miễn).
- Chỉ có **một Admin**; Admin không thể tự hạ quyền hoặc tạo thêm Admin qua trang Phân quyền.
- **Manager** do Admin tạo tại `/User` (không có trong SampleDataSeed).

### URL rút gọn

Các đường dẫn sau hoạt động không cần `/Index` (cấu hình route trong `Program.cs`): `/Premium`, `/User`, `/BankAccountChecker`, `/ScamReport`.

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
| GET | `/api/v1/banks` | Công khai | Danh sách ngân hàng VietQR |
| GET | `/api/v1/banks/lookup` | Công khai | Tra tên chủ tài khoản |

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
8. **Crawl HTML** → phân loại nội dung (title, meta, schema.org, URL hints).
9. **Google Safe Browsing** — kiểm tra chéo (nếu bật API).
10. Hiển thị kết quả (inline, modal hoặc lưu lịch sử).

## Phân loại nội dung tự động

Khi URL phản hồi HTML, hệ thống dùng **HtmlAgilityPack** kết hợp:

- Trích **title**, **meta description**, **h1**, **Open Graph**, **JSON-LD** (schema.org).
- Chấm điểm từ khóa có **trọng số** (title ×5, h1 ×4, meta ×3, body ×1).
- **URL/domain hints** (vd. `udemy.com` → Giáo dục, `fifa`/`squad-builder` → Game).
- **Xử lý xung đột** để giảm false positive (game không bị gán TMĐT, Udemy không bị gán Công nghệ).
- Ngưỡng tin cậy — nếu không đủ chắc chắn → **Tổng quát**.

Các nhóm: Tin tức, Thương mại điện tử, Ngân hàng/Tài chính, Cờ bạc, Giáo dục/Khóa học, Game, Công nghệ, Giải trí.

## Bình chọn domain (Upvote / Downvote)

User đăng nhập có thể upvote (+1) hoặc downvote (-1) mỗi domain. API: `GET /Domain/Stats?domain=...`, `POST /Domain/Vote`.

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
- Leaflet.js, Chart.js, html5-qrcode (camera QR phía client)
- ZXing.Net + SkiaSharp (decode QR ảnh phía server)
- iText7 (PDF), ClosedXML (Excel)
- Swashbuckle (Swagger / OpenAPI)
- ip-api.com (geolocation)
- Tesseract (OCR), Google Gemini (chatbox)
- HtmlAgilityPack (crawl HTML)

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
│   ├── AccountController.cs
│   ├── ProfileController.cs           # /Profile — hồ sơ, gói VIP
│   ├── UrlScannerController.cs      # /Scan — quét, lịch sử, export
│   ├── BulkScanController.cs        # Quét hàng loạt
│   ├── QrScanController.cs          # Quét mã QR
│   ├── OcrScanController.cs         # OCR (Premium)
│   ├── BankAccountCheckerController.cs
│   ├── PremiumController.cs
│   ├── ScamReportController.cs
│   ├── BlacklistedBankAccountsController.cs  # /Manager/BlacklistBank
│   ├── DomainVoteController.cs
│   ├── DashboardController.cs
│   ├── CmsController.cs
│   └── Api/ ...
├── Services/
│   ├── UrlScannerService.cs
│   ├── UserAuthService.cs             # Cookie login + navbar VIP (đọc DB)
│   ├── GeminiChatService.cs           # Chatbox AI (Gemini API)
│   ├── ContentCategorizationService.cs
│   ├── GoogleSafeBrowsingService.cs
│   ├── OcrService.cs
│   ├── QrScanService.cs               # Decode QR (ZXing), lưu uploads/qr-images/
│   ├── UrlListFileParser.cs         # Parse CSV/Excel/PDF
│   ├── SampleDataSeedService.cs     # Dữ liệu mẫu demo
│   └── ...
├── tessdata/                        # eng + vie traineddata (OCR, trong repo)
├── Views/
│   ├── UrlScanner/
│   ├── Premium/
│   ├── Profile/
│   ├── ScamReport/
│   ├── QrScan/
│   └── Shared/                      # _Layout, _NavUserInfo, _Chatbox
├── wwwroot/
├── Migrations/
├── Program.cs
├── appsettings.json
└── database.bak                     # (tùy chọn, local — restore DB, không push Git)
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
- Thông báo Bootstrap Toast từ `TempData` (`site.js` + `data-flash-*` trên `_Layout.cshtml`).
- Xóa lịch sử không reload trang.

### Modal chi tiết
- Partial View `_ScanDetails.cshtml` tải qua AJAX vào Bootstrap Modal.

### Xuất báo cáo
- Lịch sử: dropdown **CSV / Excel / PDF** (theo bộ lọc hiện tại).
- Modal chi tiết: xuất **PDF** từng bản ghi (iText7, font tiếng Việt trên Windows).

### Báo cáo lừa đảo
- Form báo cáo URL/IP hoặc tài khoản ngân hàng, upload tối đa 5 ảnh bằng chứng.
- Admin/Manager duyệt → URL approved tự động thêm Blacklist domain.

### Blacklist Ngân hàng
- Manager/Admin quản lý STK lừa đảo tại `/Manager/BlacklistBank`.
- Tra cứu Premium đối chiếu cả báo cáo đã duyệt và bảng blacklist này.

## Thành viên thực hiện

- Hồ Đắc Hiệp
- Trần Gia Bảo
- Phan Gia Bảo

## Ghi chú

Kết quả đánh giá dựa trên rule đã định nghĩa. Hệ thống không khẳng định tuyệt đối URL an toàn hay độc hại — chỉ cung cấp điểm rủi ro để tham khảo trước khi truy cập.
