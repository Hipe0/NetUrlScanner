using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NetURLScanner.Options;

namespace NetURLScanner.Services;

public class GeminiChatService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;

    private const string SystemInstruction =
        "Bạn là trợ lý NetURLScanner — hệ thống quét URL, chấm điểm rủi ro, OCR, tra cứu ngân hàng lừa đảo. " +
        "Trả lời ngắn gọn bằng tiếng Việt, Markdown nhẹ. " +
        "Hướng dẫn: Quét URL (menu Quét URL), Lịch sử/Dashboard (cần đăng nhập User), Whitelist/Blacklist (Manager), " +
        "OCR & tra cứu NH (Premium), báo cáo lừa đảo (menu Trợ giúp). " +
        "Không khẳng định URL chắc chắn an toàn/độc hại — chỉ điểm rủi ro tham khảo. " +
        "Từ chối lịch sự câu hỏi không liên quan NetURLScanner.";

    public GeminiChatService(HttpClient httpClient, IOptions<GeminiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<string> GenerateResponseAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return "Lỗi: chưa cấu hình Gemini API key (Gemini:ApiKey trong appsettings.Development.json).";

        var requestBody = new Dictionary<string, object>
        {
            ["system_instruction"] = new { parts = new[] { new { text = SystemInstruction } } },
            ["contents"] = new[] { new { parts = new[] { new { text = userMessage } } } },
            ["generationConfig"] = new
            {
                maxOutputTokens = _options.MaxOutputTokens,
                temperature = 0.6
            }
        };

        if (!string.IsNullOrWhiteSpace(_options.ThinkingLevel))
        {
            requestBody["thinkingConfig"] = new
            {
                thinkingLevel = _options.ThinkingLevel.ToUpperInvariant()
            };
        }

        var model = string.IsNullOrWhiteSpace(_options.Model) ? "gemini-3.1-flash-lite" : _options.Model;
        var requestUrl =
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(_options.ApiKey)}";

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(requestUrl, requestBody, cancellationToken);
            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var err = jsonResponse.TryGetProperty("error", out var errObj)
                    ? errObj.GetProperty("message").GetString()
                    : response.ReasonPhrase;
                return $"Lỗi AI ({(int)response.StatusCode}): {err}";
            }

            if (!jsonResponse.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                return "Không có nội dung trả về.";

            var parts = candidates[0].GetProperty("content").GetProperty("parts");
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textEl))
                {
                    var text = textEl.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
            }

            return "Không thể lấy câu trả lời từ AI.";
        }
        catch (TaskCanceledException)
        {
            return "Lỗi: hết thời gian chờ phản hồi từ AI. Thử lại sau.";
        }
        catch (Exception ex)
        {
            return $"Lỗi kết nối tới AI: {ex.Message}";
        }
    }
}
