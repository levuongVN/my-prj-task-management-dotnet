using NSubstitute;
using TaskFlow.Application.Features.Tasks.DTOs;
using TaskFlow.Application.Features.Tasks.Services;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using Status = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Tests.Tasks;

public class SubtaskServiceTests
{
    private readonly ISubtaskRepository _subtasks = Substitute.For<ISubtaskRepository>();
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();

    private SubtaskService Sut() => new(_subtasks, _tasks);

    private static readonly Guid UserId = Guid.NewGuid();

    private static TaskItem MakeTask(Status status = Status.Todo) => new()
    {
        Id = Guid.NewGuid(),
        Title = "T",
        UserId = UserId,
        Status = status,
    };

    private static SubtaskItem MakeSubtask(TaskItem task, bool isCompleted = false, bool isDeleted = false, int position = 0) => new()
    {
        Id = Guid.NewGuid(),
        Task = task,
        TaskId = task.Id,
        Title = "s",
        IsCompleted = isCompleted,
        IsDeleted = isDeleted,
        Position = position,
    };

    // ---------------- GetOwned guards ----------------

    [Fact]
    public async Task GetById_SubtaskKhongTonTai_NemKeyNotFound()
    {
        _subtasks.GetByIdAsync(Arg.Any<Guid>()).Returns((SubtaskItem?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().GetByIdAsync(Guid.NewGuid(), UserId));
    }

    [Fact]
    public async Task GetById_SubtaskDaXoa_NemKeyNotFound()
    {
        var task = MakeTask();
        _subtasks.GetByIdAsync(Arg.Any<Guid>()).Returns(MakeSubtask(task, isDeleted: true));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().GetByIdAsync(Guid.NewGuid(), UserId));
    }

    [Fact]
    public async Task GetById_TaskCuaUserKhac_NemKeyNotFoundKhongLeak()
    {
        var task = MakeTask();
        task.UserId = Guid.NewGuid();
        _subtasks.GetByIdAsync(Arg.Any<Guid>()).Returns(MakeSubtask(task));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().GetByIdAsync(Guid.NewGuid(), UserId));
    }

    [Fact]
    public async Task GetById_TaskDaXoa_NemKeyNotFound()
    {
        var task = MakeTask();
        task.IsDeleted = true;
        _subtasks.GetByIdAsync(Arg.Any<Guid>()).Returns(MakeSubtask(task));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().GetByIdAsync(Guid.NewGuid(), UserId));
    }

    [Fact]
    public async Task GetById_HopLe_MapDung()
    {
        var task = MakeTask();
        var subtask = MakeSubtask(task, isCompleted: true, position: 2);
        subtask.Title = "abc";
        _subtasks.GetByIdAsync(subtask.Id).Returns(subtask);

        var res = await Sut().GetByIdAsync(subtask.Id, UserId);

        Assert.Equal(subtask.Id, res.Id);
        Assert.Equal("abc", res.Title);
        Assert.True(res.IsCompleted);
        Assert.Equal(2, res.Position);
        Assert.Equal(task.Id, res.TaskId);
    }

    [Fact]
    public async Task GetByTask_TaskKhongTonTai_NemKeyNotFound()
    {
        _tasks.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((TaskItem?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().GetByTaskAsync(Guid.NewGuid(), UserId));
    }

    [Fact]
    public async Task GetByTask_HopLe_TraDanhSach()
    {
        var task = MakeTask();
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);
        _subtasks.GetByTaskIdAsync(task.Id).Returns([MakeSubtask(task), MakeSubtask(task)]);

        var res = await Sut().GetByTaskAsync(task.Id, UserId);

        Assert.Equal(2, res.Count);
    }

    // ---------------- Create ----------------

    [Fact]
    public async Task Create_TaskKhongTonTai_NemKeyNotFound()
    {
        _tasks.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((TaskItem?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => Sut().CreateAsync(new CreateSubtaskRequest { TaskId = Guid.NewGuid(), Title = "x" }, UserId));
    }

    [Fact]
    public async Task Create_TitleTrong_NemArgument()
    {
        var task = MakeTask();
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().CreateAsync(new CreateSubtaskRequest { TaskId = task.Id, Title = "   " }, UserId));

        Assert.Equal("Title is required", ex.Message);
    }

    [Fact]
    public async Task Create_TitleQua255_NemArgument()
    {
        var task = MakeTask();
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().CreateAsync(new CreateSubtaskRequest { TaskId = task.Id, Title = new string('x', 256) }, UserId));

        Assert.Equal("Title must be at most 255 characters", ex.Message);
    }

    [Fact]
    public async Task Create_HopLe_TitleDuocTrimVaPositionCuoi()
    {
        var task = MakeTask();
        task.Subtasks.Add(MakeSubtask(task, position: 0));
        task.Subtasks.Add(MakeSubtask(task, isDeleted: true, position: 1));
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);
        SubtaskItem? added = null;
        await _subtasks.AddAsync(Arg.Do<SubtaskItem>(s => added = s));

        var res = await Sut().CreateAsync(new CreateSubtaskRequest { TaskId = task.Id, Title = "  Moi  " }, UserId);

        Assert.Equal("Moi", res.Title);
        Assert.NotNull(added);
        Assert.Equal(1, added!.Position);
        Assert.False(added.IsCompleted);
    }

    [Fact]
    public async Task Create_ThemSubtaskVaoTaskDone_TrangThaiHienTai_GiuNguyenDone()
    {
        var task = MakeTask(Status.Done);
        task.Subtasks.Add(MakeSubtask(task, isCompleted: true));
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        await Sut().CreateAsync(new CreateSubtaskRequest { TaskId = task.Id, Title = "new" }, UserId);

        Assert.Equal(Status.Done, task.Status);
        _tasks.Received(1).Update(task);
    }

    // ---------------- Update / Toggle / Delete ----------------

    [Fact]
    public async Task Update_HopLe_CapNhatTitleVaUpdatedAt()
    {
        var task = MakeTask();
        var subtask = MakeSubtask(task);
        _subtasks.GetByIdAsync(subtask.Id).Returns(subtask);

        var res = await Sut().UpdateAsync(subtask.Id, UserId, new UpdateSubtaskRequest { Title = " renamed " });

        Assert.Equal("renamed", res.Title);
        _subtasks.Received(1).Update(subtask);
        await _subtasks.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Toggle_ChuaHoanThanhSangHoanThanh_DuTaskDone()
    {
        var task = MakeTask(Status.InProgress);
        var s1 = MakeSubtask(task, isCompleted: true);
        var s2 = MakeSubtask(task);
        task.Subtasks.Add(s1);
        task.Subtasks.Add(s2);
        _subtasks.GetByIdAsync(s2.Id).Returns(s2);
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        var res = await Sut().ToggleAsync(s2.Id, UserId);

        Assert.True(res.IsCompleted);
        Assert.Equal(Status.Done, task.Status);
        _tasks.Received(1).Update(task);
    }

    [Fact]
    public async Task Toggle_BoTick1SubtaskTrongTaskDone_TaskVeInProgress()
    {
        var task = MakeTask(Status.Done);
        var s1 = MakeSubtask(task, isCompleted: true);
        var s2 = MakeSubtask(task, isCompleted: true);
        task.Subtasks.Add(s1);
        task.Subtasks.Add(s2);
        _subtasks.GetByIdAsync(s2.Id).Returns(s2);
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        await Sut().ToggleAsync(s2.Id, UserId);

        Assert.False(s2.IsCompleted);
        Assert.Equal(Status.InProgress, task.Status);
    }

    [Fact]
    public async Task Delete_SoftDelete_KhongMatDuLieu()
    {
        var task = MakeTask();
        var subtask = MakeSubtask(task);
        task.Subtasks.Add(subtask);
        _subtasks.GetByIdAsync(subtask.Id).Returns(subtask);
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        await Sut().DeleteAsync(subtask.Id, UserId);

        Assert.True(subtask.IsDeleted);
        _subtasks.Received(1).Delete(subtask);
        await _subtasks.Received(1).SaveChangesAsync();
    }

    // ---------------- Reorder ----------------

    [Fact]
    public async Task Reorder_SoLuongKhongKhop_NemArgument()
    {
        var task = MakeTask();
        var s1 = MakeSubtask(task);
        var s2 = MakeSubtask(task);
        task.Subtasks.Add(s1);
        task.Subtasks.Add(s2);
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().ReorderAsync(new ReorderSubtasksRequest { TaskId = task.Id, OrderedSubtaskIds = [s1.Id] }, UserId));
    }

    [Fact]
    public async Task Reorder_IdTrungNhau_NemArgument()
    {
        var task = MakeTask();
        var s1 = MakeSubtask(task);
        var s2 = MakeSubtask(task);
        task.Subtasks.Add(s1);
        task.Subtasks.Add(s2);
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().ReorderAsync(new ReorderSubtasksRequest { TaskId = task.Id, OrderedSubtaskIds = [s1.Id, s1.Id] }, UserId));
    }

    [Fact]
    public async Task Reorder_IdLa_NemArgument()
    {
        var task = MakeTask();
        var s1 = MakeSubtask(task);
        var s2 = MakeSubtask(task);
        task.Subtasks.Add(s1);
        task.Subtasks.Add(s2);
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().ReorderAsync(new ReorderSubtasksRequest { TaskId = task.Id, OrderedSubtaskIds = [s1.Id, Guid.NewGuid()] }, UserId));
    }

    [Fact]
    public async Task Reorder_HopLe_GanPositionTheoThuongMoi()
    {
        var task = MakeTask();
        var s1 = MakeSubtask(task, position: 0);
        var s2 = MakeSubtask(task, position: 1);
        task.Subtasks.Add(s1);
        task.Subtasks.Add(s2);
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        var res = await Sut().ReorderAsync(new ReorderSubtasksRequest { TaskId = task.Id, OrderedSubtaskIds = [s2.Id, s1.Id] }, UserId);

        Assert.Equal(0, s2.Position);
        Assert.Equal(1, s1.Position);
        Assert.Equal([s2.Id, s1.Id], res.Select(s => s.Id));
        _subtasks.Received(2).Update(Arg.Any<SubtaskItem>());
        await _subtasks.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Reorder_SubtaskDaXoa_KhongTinhVaoDanhSach()
    {
        var task = MakeTask();
        var s1 = MakeSubtask(task, position: 0);
        var deleted = MakeSubtask(task, isDeleted: true, position: 1);
        task.Subtasks.Add(s1);
        task.Subtasks.Add(deleted);
        _tasks.GetByIdAsync(task.Id, UserId).Returns(task);

        var res = await Sut().ReorderAsync(new ReorderSubtasksRequest { TaskId = task.Id, OrderedSubtaskIds = [s1.Id] }, UserId);

        Assert.Single(res);
    }
}
