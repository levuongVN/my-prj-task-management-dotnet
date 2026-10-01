using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using NSubstitute;
using TaskFlow.Application.Common;
using TaskFlow.Application.Features.AI.DTOs;
using TaskFlow.Application.Features.AI.Services;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Tests.AI;

public class AiServiceTests
{
    private readonly IChatClient _chat = Substitute.For<IChatClient>();
    private readonly IAiChatSessionRepository _sessions = Substitute.For<IAiChatSessionRepository>();
    private readonly IAiChatMessageRepository _messages = Substitute.For<IAiChatMessageRepository>();
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly IMeetingRepository _meetings = Substitute.For<IMeetingRepository>();
    private readonly ISubtaskRepository _subtasks = Substitute.For<ISubtaskRepository>();

    private static readonly Guid UserId = Guid.NewGuid();

    // Mặc định DailyUserLimit = 0 (tắt quota) để test không bị chặn
    private AiService Sut(AiOptions? options = null) => new(
        _chat,
        _sessions,
        _messages,
        _tasks,
        _projects,
        _meetings,
        _subtasks,
        Options.Create(options ?? new AiOptions { DailyUserLimit = 0 })
    );

    // Giả lập luồng streaming của IChatClient. Mỗi ChatResponseUpdate = 1 mảnh
    // text AI "sinh ra" (ChatResponseUpdate là type streaming của Microsoft.Extensions.AI
    // ở bản 10.x; bản 9.x cũ tên là StreamingChatCompletionUpdate).
    private static async IAsyncEnumerable<ChatResponseUpdate> Stream(params string[] chunks)
    {
        // await rỗng để tránh cảnh báo CS1998 (async iterator không có await)
        await Task.CompletedTask;

        foreach (var chunk in chunks)
            yield return new ChatResponseUpdate(ChatRole.Assistant, chunk);
    }

    private void SeedEmptyContext()
    {
        _tasks.GetAllByUserIdAsync(UserId).Returns([]);
        _projects.GetAllByUserAsync(UserId).Returns([]);
        _meetings.GetAllByUserIdAsync(UserId).Returns([]);
        _messages.GetLastBySessionAsync(Arg.Any<Guid>(), Arg.Any<int>()).Returns([]);
        _sessions.AddAsync(Arg.Any<AiChatSession>()).Returns(ci => ci.Arg<AiChatSession>());
        _messages.AddAsync(Arg.Any<AiChatMessage>()).Returns(ci => ci.Arg<AiChatMessage>());
    }

    [Fact]
    public async Task StreamChat_SessionMoi_LuuUserMessageVaAssistantMessage()
    {
        SeedEmptyContext();

        _chat.GetStreamingResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Stream("Xin", " chào"));

        var events = new List<AiStreamEvent>();

        await foreach (var evt in Sut().StreamChatAsync(new AiChatRequest { Message = "hello" }, UserId))
            events.Add(evt);

        Assert.Equal(AiStreamEventType.Session, events[0].Type);
        Assert.NotNull(events[0].SessionId);
        Assert.Equal(2, events.Count(e => e.Type == AiStreamEventType.Chunk));
        Assert.Equal(AiStreamEventType.Done, events[^1].Type);

        await _messages.Received(1).AddAsync(Arg.Is<AiChatMessage>(m =>
            m.Role == AiChatRole.User && m.Content == "hello"));

        // Text từ nhiều update phải được cộng dồn thành 1 message duy nhất của AI
        await _messages.Received(1).AddAsync(Arg.Is<AiChatMessage>(m =>
            m.Role == AiChatRole.Assistant && m.Content == "Xin chào"));

        await _sessions.Received(1).TouchAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task StreamChat_SessionCuaUserKhac_ThrowNotFound()
    {
        var other = new AiChatSession { UserId = Guid.NewGuid() };
        _sessions.GetByIdAsync(other.Id).Returns(other);

        await Assert.ThrowsAsync<NotFoundException>(async () =>
        {
            await foreach (var _ in Sut().StreamChatAsync(
                new AiChatRequest { SessionId = other.Id, Message = "hi" }, UserId))
            {
            }
        });

        await _messages.DidNotReceive().AddAsync(Arg.Any<AiChatMessage>());
    }

    [Fact]
    public async Task StreamChat_VuotQuotaNgay_ThrowBadRequest()
    {
        _messages.CountTodayByUserAsync(UserId).Returns(80);

        await Assert.ThrowsAsync<BadRequestException>(async () =>
        {
            await foreach (var _ in Sut(new AiOptions { DailyUserLimit = 80 }).StreamChatAsync(
                new AiChatRequest { Message = "hi" }, UserId))
            {
            }
        });
    }

    [Fact]
    public async Task StreamChat_MessageRong_ThrowBadRequest()
    {
        await Assert.ThrowsAsync<BadRequestException>(async () =>
        {
            await foreach (var _ in Sut().StreamChatAsync(new AiChatRequest { Message = "   " }, UserId))
            {
            }
        });
    }

    [Fact]
    public async Task StreamChat_MessageQuaDai_ThrowBadRequest()
    {
        var tooLong = new string('a', 4001);

        await Assert.ThrowsAsync<BadRequestException>(async () =>
        {
            await foreach (var _ in Sut().StreamChatAsync(new AiChatRequest { Message = tooLong }, UserId))
            {
            }
        });
    }

    [Fact]
    public async Task StreamChat_TitleLay60KyTuDau()
    {
        SeedEmptyContext();

        _chat.GetStreamingResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Stream("ok"));

        var longMessage = new string('x', 100);

        await foreach (var _ in Sut().StreamChatAsync(new AiChatRequest { Message = longMessage }, UserId))
        {
        }

        await _sessions.Received(1).AddAsync(Arg.Is<AiChatSession>(s =>
            s.UserId == UserId && s.Title.Length == 60));
    }

    [Fact]
    public async Task ParseTaskDraft_JsonBocFence_ParseVaKhopProject()
    {
        var project = new Project { Id = Guid.NewGuid(), Name = "ABC", UserId = UserId };
        _projects.GetAllByUserAsync(UserId).Returns([project]);

        _chat.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                "```json\n{\"title\":\"Demo\",\"priority\":1,\"projectName\":\"abc\",\"subtasks\":[\"A\",\"B\"]}\n```")));

        var draft = await Sut().ParseTaskDraftAsync("prepare demo for abc project", UserId);

        Assert.Equal("Demo", draft.Title);
        Assert.Equal(1, draft.Priority);

        // Khớp project không phân biệt hoa/thường: "abc" -> "ABC"
        Assert.Equal(project.Id, draft.ProjectId);
        Assert.Equal("ABC", draft.ProjectName);
        Assert.Equal(2, draft.Subtasks.Count);
    }

    [Fact]
    public async Task ParseTaskDraft_AiTraRac_ThrowBadRequest()
    {
        _projects.GetAllByUserAsync(UserId).Returns([]);

        _chat.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "I cannot do that")));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            Sut().ParseTaskDraftAsync("some text here", UserId));
    }

    [Fact]
    public async Task ParseTaskDraft_PriorityNgoaiDai_ClampVe2()
    {
        _projects.GetAllByUserAsync(UserId).Returns([]);

        _chat.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                "{\"title\":\"X\",\"priority\":9}")));

        var draft = await Sut().ParseTaskDraftAsync("some text here", UserId);

        Assert.Equal(2, draft.Priority);
    }

    [Fact]
    public async Task SuggestBreakdown_TaskKhongTonTai_ThrowNotFound()
    {
        _tasks.GetByIdAsync(Arg.Any<Guid>(), UserId).Returns((TaskItem?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Sut().SuggestTaskBreakdownAsync(Guid.NewGuid(), UserId));
    }

    [Fact]
    public async Task SuggestBreakdown_HopLe_TraVeDanhSachGoiY()
    {
        var task = new TaskItem { Id = Guid.NewGuid(), Title = "T", Description = "D", UserId = UserId };
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);
        _subtasks.GetByTaskIdAsync(task.Id).Returns([]);

        _chat.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                "[{\"title\":\"Step 1\",\"reason\":\"why\"},{\"title\":\"Step 2\",\"reason\":\"because\"}]")));

        var res = await Sut().SuggestTaskBreakdownAsync(task.Id, UserId);

        Assert.Equal(2, res.Count);
        Assert.Equal("Step 1", res[0].Title);
        Assert.Equal("why", res[0].Reason);
    }

    [Fact]
    public async Task DeleteSession_CuaUserKhac_ThrowNotFound()
    {
        var other = new AiChatSession { UserId = Guid.NewGuid() };
        _sessions.GetByIdAsync(other.Id).Returns(other);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Sut().DeleteSessionAsync(other.Id, UserId));

        await _sessions.DidNotReceive().DeleteAsync(Arg.Any<AiChatSession>());
    }

    [Fact]
    public async Task GetSessionMessages_SessionCuaUserKhac_ThrowNotFound()
    {
        var other = new AiChatSession { UserId = Guid.NewGuid() };
        _sessions.GetByIdAsync(other.Id).Returns(other);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Sut().GetSessionMessagesAsync(other.Id, UserId, 1, 50));
    }
}
