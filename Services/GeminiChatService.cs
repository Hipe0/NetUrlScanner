using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace NetURLScanner.Services
{
    public class GeminiChatService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        private const string SystemInstruction = @"Bạn là một trợ lý ảo hỗ trợ người dùng của hệ thống NetURLScanner. Nhiệm vụ của bạn là hướng dẫn người dùng cách sử dụng trang web, giải thích các tính năng và trả lời các câu hỏi liên quan đến bảo mật URL.

Về NetURLScanner:
Đây là hệ thống quét URL để kiểm tra trạng thái hoạt động, đo thời gian phản hồi và đánh giá mức độ rủi ro dựa trên các rule phân tích bảo mật.

Các chức năng chính của website bao gồm:
1. Quét URL cơ bản: Bất kỳ ai (kể cả khách) cũng có thể nhập URL để kiểm tra. Hệ thống sẽ trả về HTTP status code (200, 301, 404...), thời gian phản hồi, kiểm tra HTTPS và phân loại trạng thái.
2. Đánh giá rủi ro: Hệ thống chấm điểm rủi ro (0-100). Dưới 25 là Safe (An toàn), 26-55 là Warning (Cảnh báo), trên 56 là Suspicious (Đáng ngờ).
3. Phát hiện nâng cao: Phát hiện lookalike domain (vd: go0gle.com), punycode, TLD rủi ro (.xyz, .top), và các trang web liên quan đến cá cược/cờ bạc.
4. Tra cứu thông tin IP: Tìm vị trí địa lý của máy chủ và hiển thị trên bản đồ.
5. Quản lý tài khoản và Phân quyền:
   - Guest: Chỉ có thể quét URL cơ bản.
   - User: Có thể quét URL, xem Lịch sử quét (có lọc, phân trang, biểu đồ) và xuất báo cáo PDF.
   - Manager: Quyền của User cộng thêm quản lý danh sách Whitelist (thương hiệu uy tín) và Blacklist (domain độc hại).
   - Admin: Quyền của Manager cộng thêm quản lý phân quyền User và xem Swagger UI API.

Quy tắc trả lời của bạn:
- Luôn giữ thái độ thân thiện, lịch sự và chuyên nghiệp.
- Trả lời ngắn gọn, đúng trọng tâm vào câu hỏi của người dùng. Trả lời bằng Markdown.
- Nếu người dùng hỏi cách thực hiện một tính năng (ví dụ: xem lịch sử, xuất PDF, thêm blacklist), hãy giải thích rõ họ cần quyền gì (User, Manager, Admin) và hướng dẫn họ vào trang tương ứng trên thanh menu.
- Hệ thống KHÔNG khẳng định tuyệt đối một URL là an toàn hay độc hại, nó chỉ cung cấp điểm rủi ro để tham khảo. Hãy nhắc nhở người dùng điều này nếu họ hỏi ""URL này có lừa đảo chắc chắn không?"".
- Nếu người dùng hỏi các câu không liên quan đến hệ thống NetURLScanner, hãy từ chối lịch sự và nói rằng bạn chỉ hỗ trợ về hệ thống này.";

        public GeminiChatService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
        }

        public async Task<string> GenerateResponseAsync(string userMessage)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                return "Lỗi: API Key chưa được cấu hình.";
            }

            var requestBody = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = SystemInstruction } }
                },
                contents = new[]
                {
                    new { parts = new[] { new { text = userMessage } } }
                }
            };

            var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";

            try
            {
                var response = await _httpClient.PostAsJsonAsync(requestUrl, requestBody);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
                var candidates = jsonResponse.GetProperty("candidates");
                
                if (candidates.GetArrayLength() > 0)
                {
                    var text = candidates[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();
                    
                    return text ?? "Không thể lấy câu trả lời từ AI.";
                }

                return "Không có nội dung trả về.";
            }
            catch (Exception ex)
            {
                return $"Lỗi kết nối tới AI: {ex.Message}";
            }
        }
    }
}
