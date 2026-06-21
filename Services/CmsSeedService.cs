using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models;

namespace NetURLScanner.Services;

public class CmsSeedService
{
    private readonly ApplicationDbContext _context;

    public CmsSeedService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!await _context.SiteContents.AnyAsync(cancellationToken))
        {
            _context.SiteContents.Add(new SiteContent
            {
                ContentKey = "introduction",
                Title = "Giới thiệu NetURLScanner",
                Body = """
                    <p><strong>NetURLScanner</strong> là hệ thống web giúp kiểm tra URL, phát hiện dấu hiệu lừa đảo và hỗ trợ cộng đồng báo cáo website / tài khoản ngân hàng đáng ngờ.</p>
                    <ul>
                        <li>Quét URL đơn lẻ, <strong>quét hàng loạt</strong> (CSV/Excel/PDF), <strong>quét mã QR</strong></li>
                        <li>Phân tích HTTPS, HTTP status, thời gian phản hồi, IP & vị trí máy chủ</li>
                        <li>Chấm điểm rủi ro, đối chiếu Whitelist/Blacklist URL, Google Safe Browsing</li>
                        <li>Phân loại nội dung trang web tự động (crawl HTML)</li>
                        <li>Lịch sử quét, xuất báo cáo <strong>CSV / Excel / PDF</strong></li>
                        <li><strong>Premium:</strong> OCR đọc ảnh SMS/email, tra cứu blacklist ngân hàng</li>
                        <li><strong>Kiểm duyệt:</strong> Blacklist Ngân hàng (<code>/Manager/BlacklistBank</code>), duyệt báo cáo lừa đảo</li>
                        <li>Báo cáo lừa đảo (upload ảnh bằng chứng), chatbox AI hỗ trợ</li>
                    </ul>
                    <p>Đăng ký tài khoản để lưu lịch sử, quét hàng loạt và sử dụng dashboard. Có <strong>dữ liệu mẫu</strong> tự seed khi demo (<code>SampleDataSeed</code>).</p>
                    """,
                UpdatedAt = DateTime.Now
            });
        }

        if (!await _context.FaqItems.AnyAsync(cancellationToken))
        {
            _context.FaqItems.AddRange(
                new FaqItem { Question = "Tôi có cần đăng nhập để quét URL không?", Answer = "Không. Bạn có thể quét URL cơ bản khi chưa đăng nhập. Đăng nhập để lưu lịch sử, quét hàng loạt, quét QR, gắn nhãn và xuất báo cáo.", SortOrder = 1 },
                new FaqItem { Question = "Điểm rủi ro được tính như thế nào?", Answer = "Hệ thống cộng điểm theo các rule: HTTPS, trạng thái HTTP, từ khóa đáng ngờ, giả mạo thương hiệu, TLD rủi ro, blacklist... Kết quả chỉ mang tính tham khảo.", SortOrder = 2 },
                new FaqItem { Question = "Premium có gì khác gói miễn phí?", Answer = "Gói miễn phí gồm quét URL, hàng loạt, QR, lịch sử và báo cáo lừa đảo. <strong>Premium</strong> thêm <strong>OCR</strong> đọc ảnh và <strong>tra cứu tài khoản ngân hàng</strong> nghi ngờ.", SortOrder = 3 },
                new FaqItem { Question = "Làm sao báo cáo website hoặc STK lừa đảo?", Answer = "Vào menu <strong>Trợ giúp → Báo cáo lừa đảo</strong> (cần đăng nhập). Bạn có thể báo cáo URL/IP hoặc tài khoản ngân hàng, kèm mô tả và ảnh bằng chứng. Admin/Manager sẽ duyệt.", SortOrder = 4 },
                new FaqItem { Question = "Tôi có thể quét nhiều URL cùng lúc không?", Answer = "Có. Vào <strong>Tài khoản → Quét hàng loạt</strong> để dán danh sách URL hoặc import file <strong>CSV, Excel (.xlsx) hoặc PDF</strong> (tối đa 30 URL/lần).", SortOrder = 5 },
                new FaqItem { Question = "Google Safe Browsing là gì?", Answer = "Đây là bước <strong>kiểm tra chéo</strong> với cơ sở dữ liệu malware/phishing của Google. Có sẵn ở cả gói miễn phí — không thay thế điểm rủi ro nội bộ của hệ thống.", SortOrder = 6 },
                new FaqItem { Question = "Blacklist Ngân hàng khác báo cáo lừa đảo thế nào?", Answer = "Báo cáo lừa đảo do người dùng gửi, Admin duyệt. <strong>Blacklist Ngân hàng</strong> do Manager/Admin thêm trực tiếp tại <code>/Manager/BlacklistBank</code>. Tra cứu Premium kiểm tra cả hai nguồn.", SortOrder = 7 },
                new FaqItem { Question = "Dữ liệu mẫu (SampleDataSeed) là gì?", Answer = "Khi bật trong appsettings, app tự tạo user demo (<code>user1@gmail.com</code> / <code>123456</code>), lịch sử quét, blacklist... để tiện demo. Tắt khi dùng dữ liệu thật.", SortOrder = 8 }
            );
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
