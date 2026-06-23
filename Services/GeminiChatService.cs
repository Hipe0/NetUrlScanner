using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NetURLScanner.Options;

namespace NetURLScanner.Services;

/// <summary>Gọi Google Gemini API cho chatbox — mặc định gemini-3.1-flash-lite, retry 503.</summary>
public class GeminiChatService
{
    private const int MaxAttempts = 3;

    private const string SystemInstruction =
        "Bạn là trợ lý NetURLScanner — hệ thống quét URL, chấm điểm rủi ro, OCR, tra cứu ngân hàng lừa đảo. " +
        "Trả lời ngắn gọn bằng tiếng Việt, Markdown nhẹ. " +
        "Hướng dẫn: Quét URL (menu Quét URL), Lịch sử/Dashboard (cần đăng nhập User), Whitelist/Blacklist (Manager), " +
        "OCR & tra cứu NH (Premium), báo cáo lừa đảo (menu Trợ giúp). " +
        "Không khẳng định URL chắc chắn an toàn/độc hại — chỉ điểm rủi ro tham khảo. " +
        "Từ chối lịch sự câu hỏi không liên quan NetURLScanner.";

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;

    public GeminiChatService(HttpClient httpClient, IOptions<GeminiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<string> GenerateResponseAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return "Lỗi: chưa cấu hình Gemini API key (Gemini:ApiKey trong appsettings.Development.json).";

        var model = ResolveModel();
        var requestUrl =
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(_options.ApiKey)}";
        var requestBody = BuildRequestBody(userMessage, model);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var response = await _httpClient.PostAsJsonAsync(requestUrl, requestBody, cancellationToken);
                var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

                if (response.StatusCode == HttpStatusCode.ServiceUnavailable && attempt < MaxAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    return FormatHttpError(response.StatusCode, json);

                return ExtractReplyText(json) ?? "Không thể lấy câu trả lời từ AI.";
            }
            catch (TaskCanceledException) when (attempt < MaxAttempts && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return "Hết thời gian chờ phản hồi từ Gemini. Thử lại sau vài giây.";
            }
            catch (Exception ex)
            {
                return $"Lỗi kết nối tới AI: {ex.Message}";
            }
        }

        return "Gemini tạm thời không phản hồi. Vui lòng thử lại sau.";
    }

    private string ResolveModel() =>
        string.IsNullOrWhiteSpace(_options.Model) ? GeminiOptions.DefaultModel : _options.Model.Trim();

    private object BuildRequestBody(string userMessage, string model)
    {
        var generationConfig = new Dictionary<string, object>
        {
            ["maxOutputTokens"] = _options.MaxOutputTokens,
            ["temperature"] = 0.6
        };

        if (SupportsThinking(model) && !string.IsNullOrWhiteSpace(_options.ThinkingLevel))
        {
            generationConfig["thinkingConfig"] = new
            {
                thinkingLevel = _options.ThinkingLevel.ToUpperInvariant()
            };
        }

        return new Dictionary<string, object>
        {
            ["system_instruction"] = new { parts = new[] { new { text = SystemInstruction } } },
            ["contents"] = new[] { new { parts = new[] { new { text = userMessage } } } },
            ["generationConfig"] = generationConfig
        };
    }

    private static string? ExtractReplyText(JsonElement json)
    {
        if (!json.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return "Không có nội dung trả về.";

        foreach (var part in candidates[0].GetProperty("content").GetProperty("parts").EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textEl))
            {
                var text = textEl.GetString();
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
        }

        return null;
    }

    private static string FormatHttpError(HttpStatusCode status, JsonElement json)
    {
        if ((int)status == 503)
            return "Gemini đang quá tải (503). Đợi vài giây rồi thử lại.";

        var err = json.TryGetProperty("error", out var errObj)
            ? errObj.GetProperty("message").GetString()
            : status.ToString();
        return $"Lỗi AI ({(int)status}): {err}";
    }

    /// <summary>thinkingConfig chỉ dùng với gemini-3.5-flash (không phải *-lite).</summary>
    private static bool SupportsThinking(string model) =>
        model.Contains("3.5-flash", StringComparison.OrdinalIgnoreCase)
        && !model.Contains("lite", StringComparison.OrdinalIgnoreCase);
}
