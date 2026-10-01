using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.Core;
using TaskFlow.Infrastructure.AI;

namespace TaskFlow.Tests.AI;

public class ResilientChatClientTests
{
    private static async IAsyncEnumerable<ChatResponseUpdate> Stream(params string[] chunks)
    {
        await Task.CompletedTask;

        foreach (var chunk in chunks)
            yield return new ChatResponseUpdate(ChatRole.Assistant, chunk);
    }

    // Async iterator ném lỗi TRƯỚC khi nhả chunk nào (giống lỗi 503 lúc mở kết nối)
    private static async IAsyncEnumerable<ChatResponseUpdate> ThrowingStream(Exception exception)
    {
        await Task.CompletedTask;

        if (exception is null)
            yield break;

        throw exception;
    }

    // Async iterator nhả 1 chunk rồi mới lỗi (lỗi giữa stream)
    private static async IAsyncEnumerable<ChatResponseUpdate> YieldThenThrow()
    {
        await Task.CompletedTask;

        yield return new ChatResponseUpdate(ChatRole.Assistant, "partial");

        throw new ServerError("mid-stream failure");
    }

    private static IChatClient ResponseClient(ChatResponse response) =>
        ResponseClient(_ => Task.FromResult(response));

    private static IChatClient ResponseClient(Func<CallInfo, Task<ChatResponse>> behavior)
    {
        var client = Substitute.For<IChatClient>();
        client.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(behavior);
        return client;
    }

    private static IChatClient StreamingClient(Func<CallInfo, IAsyncEnumerable<ChatResponseUpdate>> behavior)
    {
        var client = Substitute.For<IChatClient>();
        client.GetStreamingResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(behavior);
        return client;
    }

    private static ResilientChatClient Sut(params (string Model, IChatClient Client)[] models) =>
        new(models, NullLogger<ResilientChatClient>.Instance);

    [Fact]
    public async Task GetResponseAsync_ModelChinhLoi503_ChuyenSangModelDuPhong()
    {
        var primary = ResponseClient(_ => Task.FromException<ChatResponse>(new ServerError("high demand")));
        var fallback = ResponseClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        var res = await Sut(("primary", primary), ("fallback", fallback))
            .GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.Equal("ok", res.Text);
    }

    [Fact]
    public async Task GetResponseAsync_TatCaModelLoi_NemLoiCuoiCung()
    {
        var first = ResponseClient(_ => Task.FromException<ChatResponse>(new ServerError("503")));
        var second = ResponseClient(_ => Task.FromException<ChatResponse>(new ServerError("503 again")));

        var ex = await Assert.ThrowsAsync<ServerError>(() =>
            Sut(("first", first), ("second", second))
                .GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));

        Assert.Equal("503 again", ex.Message);
    }

    [Fact]
    public async Task GetResponseAsync_LoiKhongTamThoi_KhongFallback()
    {
        var primary = ResponseClient(_ =>
            Task.FromException<ChatResponse>(new InvalidOperationException("bad request")));
        var fallback = ResponseClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Sut(("primary", primary), ("fallback", fallback))
                .GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));

        await fallback.DidNotReceive().GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStreamingResponseAsync_LoiTruocKhiYield_ChuyenSangModelDuPhong()
    {
        var primary = StreamingClient(_ => ThrowingStream(new ServerError("high demand")));
        var fallback = StreamingClient(_ => Stream("Xin", " chào"));

        var chunks = new List<string>();

        await foreach (var update in Sut(("primary", primary), ("fallback", fallback))
            .GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")]))
        {
            chunks.Add(update.Text ?? string.Empty);
        }

        Assert.Equal("Xin chào", string.Concat(chunks));
    }

    [Fact]
    public async Task GetStreamingResponseAsync_LoiSauKhiYield_KhongFallbackDeTranhTrungLap()
    {
        var primary = StreamingClient(_ => YieldThenThrow());
        var fallback = StreamingClient(_ => Stream("should not appear"));

        var received = new List<string>();

        await Assert.ThrowsAsync<ServerError>(async () =>
        {
            await foreach (var update in Sut(("primary", primary), ("fallback", fallback))
                .GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")]))
            {
                received.Add(update.Text ?? string.Empty);
            }
        });

        Assert.Equal(new[] { "partial" }, received);

        // Không await: GetStreamingResponseAsync trả IAsyncEnumerable (không phải Task)
        fallback.DidNotReceive().GetStreamingResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    // Type name phải khớp những gì ResilientChatClient.IsTransient dò ("ServerError"/"ClientError")
    private sealed class ServerError(string message) : Exception(message);
}
