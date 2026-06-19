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
                    <p><strong>NetURLScanner</strong> là hệ thống web giúp kiểm tra URL trước khi truy cập.</p>
                    <ul>
                        <li>Phân tích HTTP status, HTTPS, thời gian phản hồi</li>
                        <li>Chấm điểm rủi ro theo rule-based scoring</li>
                        <li>Đối chiếu Whitelist / Blacklist và báo cáo lừa đảo</li>
                        <li>Lưu lịch sử, xuất PDF, quét hàng loạt</li>
                    </ul>
                    <p>Đăng ký tài khoản để lưu lịch sử quét và sử dụng dashboard cá nhân.</p>
                    """,
                UpdatedAt = DateTime.Now
            });
        }

        if (!await _context.FaqItems.AnyAsync(cancellationToken))
        {
            _context.FaqItems.AddRange(
                new FaqItem { Question = "Tôi có cần đăng nhập để quét URL không?", Answer = "Không. Bạn có thể quét URL cơ bản khi chưa đăng nhập. Đăng nhập để lưu lịch sử, gắn nhãn và xuất báo cáo.", SortOrder = 1 },
                new FaqItem { Question = "Điểm rủi ro được tính như thế nào?", Answer = "Hệ thống cộng điểm theo các rule: HTTPS, trạng thái HTTP, từ khóa đáng ngờ, giả mạo thương hiệu, TLD rủi ro...", SortOrder = 2 },
                new FaqItem { Question = "Làm sao báo cáo website lừa đảo?", Answer = "Dùng menu <strong>Báo cáo lừa đảo</strong> hoặc <strong>Góp ý</strong> để gửi thông tin. Admin/Manager sẽ xem xét và xử lý.", SortOrder = 3 },
                new FaqItem { Question = "Tôi có thể quét nhiều URL cùng lúc không?", Answer = "Có. Vào <strong>Quét hàng loạt</strong> để dán danh sách URL hoặc import file CSV.", SortOrder = 4 }
            );
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
