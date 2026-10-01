using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace TaskFlow.Infrastructure.AI;

// =============================================================================
// ResilientChatClient - bọc nhiều IChatClient (mỗi model Gemini 1 client) và tự
// động chuyển sang model kế tiếp khi model hiện tại gặp lỗi TẠM THỜI.
//
// Vì sao cần? Gemini free tier hay trả 503 "This model is currently experiencing
// high demand" (hoặc 429 hết quota) theo từng đợt. Nếu chỉ dùng 1 model thì cả
// tính năng chết. Wrapper này thử lần lượt: primary -> fallback1 -> fallback2...
//
// Vẫn là IChatClient nên Application KHÔNG cần biết gì về cơ chế này.
// =============================================================================
public class ResilientChatClient : IChatClient
{
    private readonly IReadOnlyList<(string Model, IChatClient Client)> _models;
    private readonly ILogger<ResilientChatClient> _logger;

    public ResilientChatClient(
        IReadOnlyList<(string Model, IChatClient Client)> models,
        ILogger<ResilientChatClient> logger)
    {
        if (models.Count == 0)
            throw new ArgumentException("At least one AI model is required.", nameof(models));

        _models = models;
        _logger = logger;
    }

    // Bản không streaming (dùng cho parse-task / breakdown): thử từng model cho tới
    // khi có model trả về thành công. Ở đây không có ràng buộc "đã yield" nên fallback
    // đơn giản hơn bản streaming.
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var list = messages as IList<ChatMessage> ?? messages.ToList();
        var lastIndex = _models.Count - 1;
        Exception? lastError = null;

        for (var i = 0; i <= lastIndex; i++)
        {
            var (model, client) = _models[i];

            try
            {
                return await client.GetResponseAsync(list, options, cancellationToken);
            }
            catch (Exception ex) when (i < lastIndex && IsTransient(ex, cancellationToken))
            {
                _logger.LogWarning(ex, "AI model {Model} failed transiently; trying next model", model);
                lastError = ex;
            }
        }

        // Tới đây nghĩa là model cuối cũng lỗi (hoặc lỗi ở model cuối không bị catch).
        throw lastError ?? new InvalidOperationException("No AI model is configured.");
    }

    // Bản streaming: phức tạp hơn vì phải đảm bảo KHÔNG fallback sau khi đã nhả
    // chunk cho client (fallback lúc đó sẽ gây trùng lặp nội dung). Quy tắc:
    //   - Lỗi tạm thời + CHƯA yield chunk nào -> thử model kế tiếp.
    //   - Lỗi SAU khi đã yield -> để lỗi nổi lên (không thể "rút lại" chunk đã gửi).
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var list = messages as IList<ChatMessage> ?? messages.ToList();
        var lastIndex = _models.Count - 1;
        Exception? lastError = null;

        for (var i = 0; i <= lastIndex; i++)
        {
            var (model, client) = _models[i];
            var canFallback = i < lastIndex;
            var yieldedAny = false;

            await using var enumerator = client
                .GetStreamingResponseAsync(list, options, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);

            while (true)
            {
                bool hasNext;

                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                }
                catch (Exception ex) when (canFallback && !yieldedAny && IsTransient(ex, cancellationToken))
                {
                    _logger.LogWarning(ex, "AI model {Model} failed transiently; trying next model", model);
                    lastError = ex;
                    break; // thoát while -> finally dispose enumerator -> sang model kế
                }

                if (!hasNext)
                    yield break; // model này trả xong -> kết thúc luôn, không thử model khác

                yieldedAny = true;
                yield return enumerator.Current;
            }
        }

        // Tất cả model đều lỗi tạm thời trước khi nhả được chunk nào -> ném lỗi gốc.
        // Controller sẽ bắt và gửi event SSE "error" cho frontend.
        if (lastError is not null)
            throw lastError;
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (serviceType == typeof(IChatClient))
            return this;

        return _models[0].Client.GetService(serviceType, serviceKey);
    }

    public void Dispose()
    {
        foreach (var (_, client) in _models)
            client.Dispose();

        GC.SuppressFinalize(this);
    }

    // Nhận diện lỗi TẠM THỜI (đáng thử model khác). Dùng TÊN TYPE thay vì reference
    // thẳng Google.GenAI.* để: (1) không phụ thuộc SDK của provider, (2) dễ unit test.
    private static bool IsTransient(Exception ex, CancellationToken cancellationToken)
    {
        // Timeout mạng cũng là tạm thời, NHƯNG OperationCanceledException do client
        // chủ động huỷ (đóng tab) thì không phải -> phân biệt bằng cancellationToken.
        if (ex is OperationCanceledException)
            return !cancellationToken.IsCancellationRequested;

        var typeName = ex.GetType().Name;

        // Google.GenAI.ServerError = lỗi 5xx (503 high demand, 500...)
        if (typeName == "ServerError")
            return true;

        // Google.GenAI.ClientError = lỗi 4xx, chỉ 429 / hết quota mới đáng thử lại
        if (typeName == "ClientError")
        {
            var message = ex.Message;
            return message.Contains("429", StringComparison.Ordinal)
                || message.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
                || message.Contains("quota", StringComparison.OrdinalIgnoreCase);
        }

        return ex is HttpRequestException;
    }
}
