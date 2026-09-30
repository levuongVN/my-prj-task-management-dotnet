using NSubstitute;
using TaskFlow.Application.Common;
using TaskFlow.Application.Features.Meetings.DTOs;
using TaskFlow.Application.Features.Meetings.Services;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Tests.Meetings;

public class MeetingServiceTests
{
    private readonly IMeetingRepository _meetings = Substitute.For<IMeetingRepository>();
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();

    private MeetingService Sut() => new(_meetings, _projects);

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ProjectId = Guid.NewGuid();

    private static Meeting MakeMeeting() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Daily",
        StartAt = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc),
        UserId = UserId,
    };

    [Fact]
    public async Task GetAll_MapVaProjectName()
    {
        var meeting = MakeMeeting();
        meeting.Project = new Project { Name = "Web", UserId = UserId };
        _meetings.GetAllByUserIdAsync(UserId).Returns([meeting]);

        var res = await Sut().GetAllAsync(UserId);

        Assert.Single(res);
        Assert.Equal("Web", res[0].ProjectName);
        Assert.Equal(meeting.Id, res[0].Id);
    }

    [Fact]
    public async Task GetPaged_PageLe_ClampVaTraMeta()
    {
        _meetings.GetPagedByUserIdAsync(UserId, Arg.Any<int>(), Arg.Any<int>()).Returns((new List<Meeting>(), 21));

        var res = await Sut().GetPagedAsync(UserId, 0, 5000);

        Assert.Equal(1, res.Page);
        Assert.Equal(100, res.PageSize);
        Assert.Equal(21, res.TotalCount);
        await _meetings.Received(1).GetPagedByUserIdAsync(UserId, 1, 100);
    }

    [Fact]
    public async Task GetById_KhongTonTai_NemNotFound()
    {
        _meetings.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((Meeting?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => Sut().GetByIdAsync(Guid.NewGuid(), UserId));

        Assert.Equal("Meeting not found", ex.Message);
    }

    [Fact]
    public async Task GetById_HopLe_MapDung()
    {
        var meeting = MakeMeeting();
        _meetings.GetByIdAsync(meeting.Id, UserId).Returns(meeting);

        var res = await Sut().GetByIdAsync(meeting.Id, UserId);

        Assert.Equal("Daily", res.Title);
        Assert.Equal(meeting.StartAt, res.StartAt);
    }

    [Fact]
    public async Task Create_ProjectKhongTonTai_NemNotFound()
    {
        _projects.GetByIdAsync(ProjectId, UserId).Returns((Project?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => Sut().CreateAsync(new CreateOrUpdateMeetingRequest { Title = "x", ProjectId = ProjectId }, UserId));
    }

    [Fact]
    public async Task Create_HopLe_AddVaReloadLayProjectName()
    {
        var project = new Project { Name = "Web", UserId = UserId };
        _projects.GetByIdAsync(ProjectId, UserId).Returns(project);
        Meeting created = null!;
        _meetings.GetByIdAsync(Arg.Any<Guid>(), UserId).Returns(ci =>
        {
            created.Project = project;
            return created;
        });
        await _meetings.AddAsync(Arg.Do<Meeting>(m => created = m));

        var res = await Sut().CreateAsync(
            new CreateOrUpdateMeetingRequest { Title = "Sync", StartAt = new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc), ProjectId = ProjectId }, UserId);

        Assert.Equal("Sync", res.Title);
        Assert.Equal("Web", res.ProjectName);
        Assert.Equal(UserId, res.UserId);
        await _meetings.Received(1).AddAsync(Arg.Any<Meeting>());
        await _meetings.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Create_KhongProject_KhongGoiProjectRepository()
    {
        Meeting created = null!;
        _meetings.GetByIdAsync(Arg.Any<Guid>(), UserId).Returns(ci => created);
        await _meetings.AddAsync(Arg.Do<Meeting>(m => created = m));

        await Sut().CreateAsync(new CreateOrUpdateMeetingRequest { Title = "Solo" }, UserId);

        await _projects.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task Update_MeetingKhongTonTai_NemNotFound()
    {
        _meetings.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((Meeting?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => Sut().UpdateAsync(Guid.NewGuid(), new CreateOrUpdateMeetingRequest { Title = "x" }, UserId));
    }

    [Fact]
    public async Task Update_ProjectMoiKhongTonTai_NemNotFound()
    {
        var meeting = MakeMeeting();
        _meetings.GetByIdAsync(meeting.Id, UserId).Returns(meeting);
        _projects.GetByIdAsync(ProjectId, UserId).Returns((Project?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => Sut().UpdateAsync(meeting.Id, new CreateOrUpdateMeetingRequest { Title = "x", ProjectId = ProjectId }, UserId));
    }

    [Fact]
    public async Task Update_HopLe_CapNhatField()
    {
        var meeting = MakeMeeting();
        _meetings.GetByIdAsync(meeting.Id, UserId).Returns(meeting);
        var start = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

        var res = await Sut().UpdateAsync(meeting.Id, new CreateOrUpdateMeetingRequest { Title = "Moved", StartAt = start }, UserId);

        Assert.Equal("Moved", res.Title);
        Assert.Equal(start, res.StartAt);
        Assert.Null(res.ProjectId);
        await _meetings.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Delete_KhongTonTai_NemNotFound()
    {
        _meetings.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((Meeting?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => Sut().DeleteAsync(Guid.NewGuid(), UserId));
    }

    [Fact]
    public async Task Delete_HopLe_XoaVaLuu()
    {
        var meeting = MakeMeeting();
        _meetings.GetByIdAsync(meeting.Id, UserId).Returns(meeting);

        await Sut().DeleteAsync(meeting.Id, UserId);

        _meetings.Received(1).Delete(meeting);
        await _meetings.Received(1).SaveChangesAsync();
    }
}
