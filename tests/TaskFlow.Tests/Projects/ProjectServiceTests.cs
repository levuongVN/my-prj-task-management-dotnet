using NSubstitute;
using TaskFlow.Application.Common;
using TaskFlow.Application.DTOs.Projects;
using TaskFlow.Application.Interfaces;
using TaskFlow.Application.Services;
using TaskFlow.Domain.Entities;
using Status = TaskFlow.Domain.Enums.TaskStatus;
using ProjectStatus = TaskFlow.Domain.Enums.ProjectStatus;

namespace TaskFlow.Tests.Projects;

public class ProjectServiceTests
{
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly ILabelRepository _labels = Substitute.For<ILabelRepository>();

    private ProjectService Sut() => new(_projects, _labels);

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ProjectId = Guid.NewGuid();

    private static Project MakeProject(int total = 0, int done = 0) 
    {
        var p = new Project
        {
            Id = ProjectId,
            Name = "Website",
            Description = "d",
            Due = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Status = ProjectStatus.Active,
            UserId = UserId,
        };
        for (var i = 0; i < total; i++)
        {
            p.Tasks.Add(new TaskItem { Status = i < done ? Status.Done : Status.Todo, UserId = UserId });
        }
        return p;
    }

    [Fact]
    public async Task GetAll_MapVaProgressDung()
    {
        _projects.GetAllByUserAsync(UserId).Returns([MakeProject(total: 4, done: 1)]);

        var res = await Sut().GetAllAsync(UserId);

        Assert.Single(res);
        Assert.Equal("Website", res[0].Name);
        Assert.Equal(ProjectStatus.Active, res[0].Status);
        Assert.Equal(25, res[0].Progress);
    }

    [Fact]
    public async Task GetAll_ProjectKhongCoTask_Progress0()
    {
        _projects.GetAllByUserAsync(UserId).Returns([MakeProject()]);

        var res = await Sut().GetAllAsync(UserId);

        Assert.Equal(0, res[0].Progress);
    }

    [Fact]
    public async Task GetAll_ToanBoDone_Progress100()
    {
        _projects.GetAllByUserAsync(UserId).Returns([MakeProject(total: 3, done: 3)]);

        var res = await Sut().GetAllAsync(UserId);

        Assert.Equal(100, res[0].Progress);
    }

    [Fact]
    public async Task GetPaged_PageLe_ClampVe1()
    {
        _projects.GetPagedByUserAsync(UserId, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Guid?>())
            .Returns((new List<Project>(), 0));

        var res = await Sut().GetPagedAsync(UserId, 0, 9999);

        Assert.Equal(1, res.Page);
        Assert.Equal(100, res.PageSize);
        await _projects.Received(1).GetPagedByUserAsync(UserId, 1, 100, null);
    }

    [Fact]
    public async Task GetById_KhongTonTai_TraNullKhongNem()
    {
        _projects.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((Project?)null);

        var res = await Sut().GetByIdAsync(Guid.NewGuid(), UserId);

        Assert.Null(res);
    }

    [Fact]
    public async Task Create_HopLe_TaoProjectVoiUser()
    {
        var res = await Sut().CreateAsync(new CreateOrUpdateProjectRequest
        {
            Name = "New P",
            Due = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc),
            Status = ProjectStatus.Completed,
        }, UserId);

        Assert.Equal("New P", res.Name);
        Assert.Equal(ProjectStatus.Completed, res.Status);
        await _projects.Received(1).AddAsync(Arg.Is<Project>(p => p.UserId == UserId));
        await _projects.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Create_LabelIdsRong_KhongGoiLabelRepository()
    {
        await Sut().CreateAsync(new CreateOrUpdateProjectRequest { Name = "x", LabelIds = [] }, UserId);

        await _labels.DidNotReceive().GetByIdsAsync(Arg.Any<Guid>(), Arg.Any<List<Guid>>());
        await _projects.Received(1).AddAsync(Arg.Is<Project>(p => p.Labels.Count == 0));
    }

    [Fact]
    public async Task Create_LabelIdLa_NemArgument()
    {
        _labels.GetByIdsAsync(UserId, Arg.Any<List<Guid>>()).Returns([new Label { UserId = UserId }]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().CreateAsync(new CreateOrUpdateProjectRequest { Name = "x", LabelIds = [Guid.NewGuid(), Guid.NewGuid()] }, UserId));
    }

    [Fact]
    public async Task Update_KhongTonTai_NemNotFound()
    {
        _projects.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((Project?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => Sut().UpdateAsync(Guid.NewGuid(), new CreateOrUpdateProjectRequest { Name = "x" }, UserId));
    }

    [Fact]
    public async Task Update_HopLe_OverrideFieldVaGiuLabelsKhiNull()
    {
        var project = MakeProject();
        project.Labels.Add(new Label { Name = "keep", UserId = UserId });
        _projects.GetByIdAsync(ProjectId, UserId).Returns(project);

        var res = await Sut().UpdateAsync(ProjectId, new CreateOrUpdateProjectRequest
        {
            Name = "Renamed",
            Status = ProjectStatus.Archived,
        }, UserId);

        Assert.Equal("Renamed", res.Name);
        Assert.Equal(ProjectStatus.Archived, res.Status);
        Assert.Single(project.Labels);
        _projects.Received(1).Update(project);
        await _projects.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Update_LabelIdsRong_GohetLabels()
    {
        var project = MakeProject();
        project.Labels.Add(new Label { Name = "old", UserId = UserId });
        _projects.GetByIdAsync(ProjectId, UserId).Returns(project);

        await Sut().UpdateAsync(ProjectId, new CreateOrUpdateProjectRequest { Name = "x", LabelIds = [] }, UserId);

        Assert.Empty(project.Labels);
    }

    [Fact]
    public async Task Delete_KhongTonTai_NemNotFound()
    {
        _projects.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((Project?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => Sut().DeleteAsync(Guid.NewGuid(), UserId));
    }

    [Fact]
    public async Task Delete_HopLe_SoftDeleteFlag()
    {
        var project = MakeProject();
        _projects.GetByIdAsync(ProjectId, UserId).Returns(project);

        await Sut().DeleteAsync(ProjectId, UserId);

        Assert.True(project.IsDeleted);
        _projects.Received(1).Update(project);
        await _projects.Received(1).SaveChangesAsync();
    }
}
