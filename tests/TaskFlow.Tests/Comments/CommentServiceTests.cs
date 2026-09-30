using NSubstitute;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Features.Comments.DTOs;
using TaskFlow.Application.Features.Comments.Services;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Tests.Comments;

public class CommentServiceTests
{
    private readonly ICommentRepository _comments = Substitute.For<ICommentRepository>();
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IFileStorageService _storage = Substitute.For<IFileStorageService>();

    private CommentService Sut() => new(_comments, _tasks, _users, _storage);

    private static readonly Guid UserId = Guid.NewGuid();

    private static TaskItem MakeTask() => new()
    {
        Id = Guid.NewGuid(),
        Title = "T",
        UserId = UserId,
    };

    private static Comment MakeComment(TaskItem task, User author, string content = "hi") => new()
    {
        Id = Guid.NewGuid(),
        Task = task,
        TaskId = task.Id,
        Author = author,
        AuthorId = author.Id,
        Content = content,
    };

    [Fact]
    public async Task GetByTask_TaskKhongTonTai_NemKeyNotFound()
    {
        _tasks.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((TaskItem?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().GetByTaskIdAsync(Guid.NewGuid(), UserId));
    }

    [Fact]
    public async Task GetByTask_HaiCommentCungAuthor_SignedUrlChiTao1Lan()
    {
        var task = MakeTask();
        var author = new User { Id = Guid.NewGuid(), FullName = "A", AvatarPath = "avatars/a.png" };
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);
        _comments.GetByTaskIdAsync(task.Id).Returns(
            [MakeComment(task, author, "c1"), MakeComment(task, author, "c2")]);
        _storage.CreateSignedUrlAsync("avatars/a.png").Returns("https://signed/a");

        var res = await Sut().GetByTaskIdAsync(task.Id, UserId);

        Assert.Equal(2, res.Count);
        Assert.All(res, c => Assert.Equal("https://signed/a", c.AuthorAvatarUrl));
        await _storage.Received(1).CreateSignedUrlAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task GetByTask_AuthorKhongCoAvatar_UrlNullKhongGoiStorage()
    {
        var task = MakeTask();
        var author = new User { Id = Guid.NewGuid(), FullName = "A" };
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);
        _comments.GetByTaskIdAsync(task.Id).Returns([MakeComment(task, author)]);

        var res = await Sut().GetByTaskIdAsync(task.Id, UserId);

        Assert.Null(res[0].AuthorAvatarUrl);
        await _storage.DidNotReceive().CreateSignedUrlAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task GetById_CommentCuaUserKhac_NemKeyNotFoundKhongLeak()
    {
        _comments.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((Comment?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().GetByIdAsync(Guid.NewGuid(), UserId));
    }

    [Fact]
    public async Task GetById_CommentThuocTaskCuaUserKhac_NemKeyNotFound()
    {
        var task = MakeTask();
        var author = new User { Id = UserId, FullName = "A" };
        var comment = MakeComment(task, author);
        _comments.GetByIdAsync(comment.Id, UserId).Returns(comment);
        _tasks.GetByIdAsync(comment.TaskId, UserId).Returns((TaskItem?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().GetByIdAsync(comment.Id, UserId));
    }

    [Fact]
    public async Task Create_AuthorKhongTonTai_NemKeyNotFound()
    {
        var task = MakeTask();
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);
        _users.GetByIdAsync(UserId).Returns((User?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => Sut().CreateAsync(UserId, new CreateCommentRequest { TaskId = task.Id, Content = "x" }));

        Assert.Equal("User not found", ex.Message);
    }

    [Fact]
    public async Task Create_HopLe_TaoCommentVoiAuthor()
    {
        var task = MakeTask();
        var author = new User { Id = UserId, FullName = "Me", AvatarPath = "p.png" };
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);
        _users.GetByIdAsync(UserId).Returns(author);
        _storage.CreateSignedUrlAsync("p.png").Returns("https://signed/me");

        var res = await Sut().CreateAsync(UserId, new CreateCommentRequest { TaskId = task.Id, Content = "  first  " });

        Assert.Equal("first", res.Content);
        Assert.Equal(UserId, res.AuthorId);
        Assert.Equal("Me", res.AuthorName);
        Assert.Equal("https://signed/me", res.AuthorAvatarUrl);
        await _comments.Received(1).AddAsync(Arg.Any<Comment>());
        await _comments.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Create_ContentRong_NemArgument()
    {
        var task = MakeTask();
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);
        _users.GetByIdAsync(UserId).Returns(new User { Id = UserId });

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().CreateAsync(UserId, new CreateCommentRequest { TaskId = task.Id, Content = "   " }));

        Assert.Equal("Comment content is required.", ex.Message);
    }

    [Fact]
    public async Task Create_ContentQua2000_NemArgument()
    {
        var task = MakeTask();
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);
        _users.GetByIdAsync(UserId).Returns(new User { Id = UserId });

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().CreateAsync(UserId, new CreateCommentRequest { TaskId = task.Id, Content = new string('a', 2001) }));

        Assert.Equal("Comment content must not exceed 2000 characters.", ex.Message);
    }

    [Fact]
    public async Task Update_HopLe_CapNhatContent()
    {
        var task = MakeTask();
        var author = new User { Id = UserId, FullName = "Me" };
        var comment = MakeComment(task, author, "old");
        _comments.GetByIdAsync(comment.Id, UserId).Returns(comment);
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        var res = await Sut().UpdateAsync(comment.Id, UserId, new UpdateCommentRequest { Content = " edited " });

        Assert.Equal("edited", res.Content);
        _comments.Received(1).Update(comment);
        await _comments.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Delete_HopLe_XoaComment()
    {
        var task = MakeTask();
        var comment = MakeComment(task, new User { Id = UserId });
        _comments.GetByIdAsync(comment.Id, UserId).Returns(comment);
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        await Sut().DeleteAsync(comment.Id, UserId);

        _comments.Received(1).Delete(comment);
        await _comments.Received(1).SaveChangesAsync();
    }
}
