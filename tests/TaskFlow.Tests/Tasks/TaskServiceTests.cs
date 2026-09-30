using NSubstitute;
using TaskFlow.Application.Common;
using TaskFlow.Application.Features.Tasks.DTOs;
using TaskFlow.Application.Features.Tasks.Services;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using Priority = TaskFlow.Domain.Enums.TaskPriority;
using Recurrence = TaskFlow.Domain.Enums.RecurrenceType;
using Status = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Tests.Tasks;

public class TaskServiceTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly ILabelRepository _labels = Substitute.For<ILabelRepository>();

    private TaskService Sut() => new(_tasks, _projects, _labels);

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ProjectId = Guid.NewGuid();

    private static TaskItem MakeTask(
        Status status = Status.Todo,
        Recurrence recurrence = Recurrence.None,
        DateTime? deadline = null,
        Guid? projectId = null)
    {
        var t = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Task A",
            Description = "desc",
            Status = status,
            Priority = Priority.High,
            Deadline = deadline,
            RecurrenceType = recurrence,
            UserId = UserId,
            ProjectId = projectId,
        };
        t.Labels.Add(new Label { Name = "urgent", Color = "#ff0000", UserId = UserId });
        return t;
    }

    // ---------------- GetByIdAsync / GetProjectTasksAsync ----------------

    [Fact]
    public async Task GetById_KhongTimThay_NemKeyNotFound()
    {
        _tasks.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((TaskItem?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => Sut().GetByIdAsync(Guid.NewGuid(), UserId));
    }

    [Fact]
    public async Task GetById_TimThay_MapDungFieldVaProgress()
    {
        var task = MakeTask(status: Status.InProgress, deadline: new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), projectId: ProjectId);
        task.Subtasks.Add(new SubtaskItem { Title = "s1", IsCompleted = true, Position = 1, TaskId = task.Id });
        task.Subtasks.Add(new SubtaskItem { Title = "s2", IsCompleted = false, Position = 2, TaskId = task.Id });
        task.Subtasks.Add(new SubtaskItem { Title = "deleted", IsCompleted = true, Position = 3, IsDeleted = true, TaskId = task.Id });
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        var res = await Sut().GetByIdAsync(task.Id, UserId);

        Assert.Equal(task.Id, res.Id);
        Assert.Equal((int)Status.InProgress, res.Status);
        Assert.Equal((int)Priority.High, res.Priority);
        Assert.Equal(ProjectId, res.ProjectId);
        Assert.Equal(2, res.TotalSubtasks);
        Assert.Equal(1, res.CompletedSubtasks);
        Assert.Equal(50, res.ProgressPercent);
        Assert.Single(res.Labels);
        Assert.Equal("urgent", res.Labels[0].Name);
        Assert.Equal(2, res.Subtasks.Count);
        Assert.Equal(["s1", "s2"], res.Subtasks.Select(s => s.Title));
    }

    [Fact]
    public async Task GetProjectTasks_MapToanBoTasksCuaProject()
    {
        var t1 = MakeTask(projectId: ProjectId);
        var t2 = MakeTask(projectId: ProjectId);
        _tasks.GetByProjectIdAsync(ProjectId, UserId).Returns([t1, t2]);

        var res = await Sut().GetProjectTasksAsync(ProjectId, UserId);

        Assert.Equal(2, res.Count);
    }

    [Fact]
    public async Task Map_KhongCoSubtask_ProgressBang0()
    {
        var task = MakeTask();
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        var res = await Sut().GetByIdAsync(task.Id, UserId);

        Assert.Equal(0, res.TotalSubtasks);
        Assert.Equal(0, res.ProgressPercent);
    }

    // ---------------- CreateAsync ----------------

    [Fact]
    public async Task Create_ThuocProjectTonTai_TaoTaskDung()
    {
        _projects.GetByIdAsync(ProjectId, UserId).Returns(new Project { Name = "P", UserId = UserId });

        var res = await Sut().CreateAsync(new CreateOrUpdateTaskRequest
        {
            Title = "New",
            Description = "d",
            ProjectId = ProjectId,
            Status = Status.InProgress,
            Priority = Priority.Low,
            Deadline = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc),
        }, UserId);

        Assert.Equal("New", res.Title);
        Assert.Equal((int)Status.InProgress, res.Status);
        Assert.Equal((int)Priority.Low, res.Priority);
        Assert.Equal(ProjectId, res.ProjectId);
        Assert.Equal(UserId, res.UserId);
        Assert.Equal((int)Recurrence.None, res.RecurrenceType);
        await _tasks.Received(1).AddAsync(Arg.Any<TaskItem>());
        await _tasks.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Create_ProjectKhongTonTai_NemNotFound()
    {
        _projects.GetByIdAsync(ProjectId, UserId).Returns((Project?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => Sut().CreateAsync(new CreateOrUpdateTaskRequest { Title = "x", ProjectId = ProjectId }, UserId));

        Assert.Equal("Project not found", ex.Message);
        await _tasks.DidNotReceive().AddAsync(Arg.Any<TaskItem>());
    }

    [Fact]
    public async Task Create_KhongCoProject_KhongHoiProjectRepository()
    {
        await Sut().CreateAsync(new CreateOrUpdateTaskRequest { Title = "solo" }, UserId);

        await _projects.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task Create_HeThongLabelIds_GanDungLabels()
    {
        var l1 = new Label { Id = Guid.NewGuid(), Name = "a", UserId = UserId };
        var l2 = new Label { Id = Guid.NewGuid(), Name = "b", UserId = UserId };
        var ids = new List<Guid> { l1.Id, l2.Id };
        _labels.GetByIdsAsync(UserId, Arg.Any<List<Guid>>()).Returns([l1, l2]);

        await Sut().CreateAsync(new CreateOrUpdateTaskRequest { Title = "t", LabelIds = ids }, UserId);

        await _labels.Received(1).GetByIdsAsync(UserId, Arg.Is<List<Guid>>(l => l.Count == 2));
        await _tasks.Received(1).AddAsync(
            Arg.Is<TaskItem>(t => t.Labels.Count == 2));
    }

    [Fact]
    public async Task Create_LabelIdsCoIdLa_NemArgument()
    {
        _labels.GetByIdsAsync(UserId, Arg.Any<List<Guid>>()).Returns([new Label { UserId = UserId }]);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().CreateAsync(new CreateOrUpdateTaskRequest { Title = "t", LabelIds = [Guid.NewGuid(), Guid.NewGuid()] }, UserId));

        Assert.Equal("One or more labels not found", ex.Message);
    }

    [Fact]
    public async Task Create_LabelIdsRong_LabelsRongVaKhongHoiRepository()
    {
        await Sut().CreateAsync(new CreateOrUpdateTaskRequest { Title = "t", LabelIds = [] }, UserId);

        await _labels.DidNotReceive().GetByIdsAsync(Arg.Any<Guid>(), Arg.Any<List<Guid>>());
        await _tasks.Received(1).AddAsync(Arg.Is<TaskItem>(t => t.Labels.Count == 0));
    }

    [Fact]
    public async Task Create_LabelIdsTrungNhau_DistinctTruocKhiHoi()
    {
        var l1 = new Label { Name = "a", UserId = UserId };
        _labels.GetByIdsAsync(UserId, Arg.Any<List<Guid>>()).Returns([l1]);

        await Sut().CreateAsync(new CreateOrUpdateTaskRequest { Title = "t", LabelIds = [l1.Id, l1.Id] }, UserId);

        await _labels.Received(1).GetByIdsAsync(UserId, Arg.Is<List<Guid>>(l => l.Count == 1));
    }

    [Fact]
    public async Task Create_RecurrenceKhongGui_MacDinhNone()
    {
        await Sut().CreateAsync(new CreateOrUpdateTaskRequest { Title = "t", RecurrenceType = Recurrence.Weekly }, UserId);

        await _tasks.Received(1).AddAsync(Arg.Is<TaskItem>(t => t.RecurrenceType == Recurrence.Weekly));
    }

    // ---------------- UpdateAsync ----------------

    [Fact]
    public async Task Update_TaskKhongTonTai_NemNotFound()
    {
        _tasks.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((TaskItem?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => Sut().UpdateAsync(Guid.NewGuid(), UserId, new CreateOrUpdateTaskRequest { Title = "x" }));
    }

    [Fact]
    public async Task Update_HopLe_OverrideFieldVaSetUpdatedAt()
    {
        var task = MakeTask();
        var before = task.UpdatedAt;
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        var res = await Sut().UpdateAsync(task.Id, UserId, new CreateOrUpdateTaskRequest
        {
            Title = "Renamed",
            Description = "new-desc",
            Status = Status.InPreview,
            Priority = Priority.Low,
            Deadline = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ProjectId = ProjectId,
        });

        Assert.Equal("Renamed", res.Title);
        Assert.Equal((int)Status.InPreview, res.Status);
        Assert.Equal((int)Priority.Low, res.Priority);
        Assert.Equal(ProjectId, res.ProjectId);
        Assert.True(task.UpdatedAt >= before);
        _tasks.Received(1).Update(task);
        await _tasks.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Update_RecurrenceTypeNull_GiuNguyenQuyTacLap()
    {
        var task = MakeTask(recurrence: Recurrence.Monthly);
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        await Sut().UpdateAsync(task.Id, UserId, new CreateOrUpdateTaskRequest { Title = "t" });

        Assert.Equal(Recurrence.Monthly, task.RecurrenceType);
    }

    [Fact]
    public async Task Update_LabelIdsNull_GiuNguyenLabelsCu()
    {
        var task = MakeTask();
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        await Sut().UpdateAsync(task.Id, UserId, new CreateOrUpdateTaskRequest { Title = "t" });

        Assert.Single(task.Labels);
        await _labels.DidNotReceive().GetByIdsAsync(Arg.Any<Guid>(), Arg.Any<List<Guid>>());
    }

    [Fact]
    public async Task Update_LabelIdsRong_GohetLabels()
    {
        var task = MakeTask();
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        await Sut().UpdateAsync(task.Id, UserId, new CreateOrUpdateTaskRequest { Title = "t", LabelIds = [] });

        Assert.Empty(task.Labels);
    }

    // ---------------- Recurring: Done -> sinh task kế ----------------

    private async Task<(TaskItem Completed, TaskItem? Clone)> CompleteWithRecurrence(
        Recurrence recurrence,
        Status oldStatus = Status.Todo,
        DateTime? deadline = null)
    {
        var task = MakeTask(status: oldStatus, recurrence: recurrence, deadline: deadline, projectId: ProjectId);
        task.Subtasks.Add(new SubtaskItem { Title = "step-1", IsCompleted = true, Position = 1 });
        task.Subtasks.Add(new SubtaskItem { Title = "step-2", IsCompleted = true, Position = 2 });
        task.Subtasks.Add(new SubtaskItem { Title = "deleted", Position = 3, IsDeleted = true });
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);
        _tasks.GetMaxPositionAsync(UserId, ProjectId).Returns(10);
        TaskItem? clone = null;
        await _tasks.AddAsync(Arg.Do<TaskItem>(t => { if (!ReferenceEquals(t, task)) clone = t; }));

        await Sut().UpdateAsync(task.Id, UserId, new CreateOrUpdateTaskRequest
        {
            Title = task.Title,
            Status = Status.Done,
            ProjectId = ProjectId,
            Deadline = deadline,
            RecurrenceType = recurrence,
        });

        return (task, clone);
    }

    [Fact]
    public async Task Update_WeeklyDone_SinhTaskKeCloneStatusTodoVaDeadlineTuanSau()
    {
        var deadline = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);
        var (completed, clone) = await CompleteWithRecurrence(Recurrence.Weekly, deadline: deadline);

        Assert.NotNull(clone);
        Assert.NotEqual(completed.Id, clone!.Id);
        Assert.Equal(Status.Todo, clone.Status);
        Assert.Equal(completed.Title, clone.Title);
        Assert.Equal(completed.UserId, clone.UserId);
        Assert.Equal(ProjectId, clone.ProjectId);
        Assert.Equal(Recurrence.Weekly, clone.RecurrenceType);
        Assert.Equal(deadline.AddDays(7), clone.Deadline);
        Assert.Equal(11, clone.Position);
        Assert.Equal(completed.Labels.Count, clone.Labels.Count);
        Assert.Equal(2, clone.Subtasks.Count);
        Assert.All(clone.Subtasks, s => Assert.False(s.IsCompleted));
    }

    [Fact]
    public async Task Update_DailyDone_DeadlineCong1Ngay()
    {
        var deadline = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);
        var (_, clone) = await CompleteWithRecurrence(Recurrence.Daily, deadline: deadline);

        Assert.Equal(deadline.AddDays(1), clone!.Deadline);
    }

    [Fact]
    public async Task Update_MonthlyDone_31Jan_ClamVe28Feb()
    {
        var deadline = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        var (_, clone) = await CompleteWithRecurrence(Recurrence.Monthly, deadline: deadline);

        Assert.Equal(new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc), clone!.Deadline);
    }

    [Fact]
    public async Task Update_RecurringKhongDeadline_DeadlineMoiTuBayGio()
    {
        var (_, clone) = await CompleteWithRecurrence(Recurrence.Daily, deadline: null);

        Assert.NotNull(clone!.Deadline);
        Assert.True(clone.Deadline > DateTime.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public async Task Update_DaDoneTuTruoc_DoneLanNua_KhongSinhClone()
    {
        var (_, clone) = await CompleteWithRecurrence(Recurrence.Weekly, oldStatus: Status.Done);

        Assert.Null(clone);
        await _tasks.DidNotReceive().AddAsync(Arg.Any<TaskItem>());
    }

    [Fact]
    public async Task Update_DoneNhungKhongRecurring_KhongSinhClone()
    {
        var (_, clone) = await CompleteWithRecurrence(Recurrence.None);

        Assert.Null(clone);
    }

    [Fact]
    public async Task Update_TodoSangInProgress_KhongSinhClone()
    {
        var task = MakeTask(recurrence: Recurrence.Daily);
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);
        TaskItem? clone = null;
        await _tasks.AddAsync(Arg.Do<TaskItem>(t => { if (!ReferenceEquals(t, task)) clone = t; }));

        await Sut().UpdateAsync(task.Id, UserId, new CreateOrUpdateTaskRequest { Title = "t", Status = Status.InProgress });

        Assert.Null(clone);
    }

    // ---------------- DeleteAsync ----------------

    [Fact]
    public async Task Delete_KhongTimThay_NemNotFound()
    {
        _tasks.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((TaskItem?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => Sut().DeleteAsync(Guid.NewGuid(), UserId));
    }

    [Fact]
    public async Task Delete_TimThay_XoaVaLuu()
    {
        var task = MakeTask();
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        await Sut().DeleteAsync(task.Id, UserId);

        _tasks.Received(1).Delete(task);
        await _tasks.Received(1).SaveChangesAsync();
    }

    // ---------------- GetAllByUser / Paged ----------------

    [Fact]
    public async Task GetAllByUser_MapToanBo()
    {
        _tasks.GetAllByUserIdAsync(UserId).Returns([MakeTask(), MakeTask()]);

        var res = await Sut().GetAllByUserAsync(UserId);

        Assert.Equal(2, res.Count);
    }

    [Fact]
    public async Task GetPagedByUser_PagePageSizeLe_ClampVe1Va100()
    {
        _tasks.GetPagedByUserIdAsync(UserId, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Guid?>())
            .Returns((new List<TaskItem>(), 0));

        var res = await Sut().GetPagedByUserAsync(UserId, page: 0, pageSize: 5000);

        Assert.Equal(1, res.Page);
        Assert.Equal(100, res.PageSize);
        await _tasks.Received(1).GetPagedByUserIdAsync(UserId, 1, 100, null);
    }

    [Fact]
    public async Task GetPagedByUser_HopLe_TraDungMeta()
    {
        _tasks.GetPagedByUserIdAsync(UserId, 2, 20, null).Returns(([MakeTask()], 45));

        var res = await Sut().GetPagedByUserAsync(UserId, 2, 20);

        Assert.Single(res.Items);
        Assert.Equal(45, res.TotalCount);
        Assert.Equal(3, res.TotalPages);
        Assert.True(res.HasNext);
        Assert.True(res.HasPrevious);
    }

    [Fact]
    public async Task GetPagedByUser_CoFilterLabel_PassedXuongRepository()
    {
        var labelId = Guid.NewGuid();
        _tasks.GetPagedByUserIdAsync(UserId, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Guid?>())
            .Returns((new List<TaskItem>(), 0));

        await Sut().GetPagedByUserAsync(UserId, 1, 20, labelId);

        await _tasks.Received(1).GetPagedByUserIdAsync(UserId, 1, 20, labelId);
    }

    [Fact]
    public async Task GetPagedProjectTasks_ClampVaMapDung()
    {
        _tasks.GetPagedByProjectIdAsync(ProjectId, UserId, Arg.Any<int>(), Arg.Any<int>())
            .Returns(([MakeTask(projectId: ProjectId)], 1));

        var res = await Sut().GetPagedProjectTasksAsync(ProjectId, UserId, -5, 0);

        Assert.Equal(1, res.Page);
        Assert.Equal(1, res.PageSize);
        Assert.Single(res.Items);
    }
}
