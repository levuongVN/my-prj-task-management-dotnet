using TaskFlow.Application.Features.Tasks.DTOs;
using TaskFlow.Application.Features.Tasks.Interfaces;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Application.Features.Tasks.Services;

public class SubtaskService(
    ISubtaskRepository subtaskRepository,
    ITaskRepository taskRepository
) : ISubtaskService
{
    private readonly ISubtaskRepository _subtaskRepository =
        subtaskRepository;

    private readonly ITaskRepository _taskRepository =
        taskRepository;

    public async Task<SubtaskResponse> GetByIdAsync(Guid subtaskId, Guid userId)
    {
        var subtask =
            await GetOwnedSubtaskAsync(subtaskId, userId);

        return Map(subtask);
    }

    public async Task<List<SubtaskResponse>> GetByTaskAsync(Guid taskId, Guid userId)
    {
        var task =
            await GetOwnedTaskAsync(taskId, userId);

        var subtasks =
            await _subtaskRepository.GetByTaskIdAsync(task.Id);

        return subtasks
            .Select(Map)
            .ToList();
    }

    public async Task<SubtaskResponse> CreateAsync(CreateSubtaskRequest request, Guid userId)
    {
        var task =
            await GetOwnedTaskAsync(request.TaskId, userId);

        var title = ValidateTitle(request.Title);

        var subtask = new SubtaskItem
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Title = title,
            IsCompleted = false,
            Position = task.Subtasks.Count(s => !s.IsDeleted)
        };

        await _subtaskRepository.AddAsync(subtask);

        ApplyTaskStatusRule(task);
        _taskRepository.Update(task);

        await _subtaskRepository.SaveChangesAsync();

        return Map(subtask);
    }

    public async Task<SubtaskResponse> UpdateAsync(Guid subtaskId, Guid userId, UpdateSubtaskRequest request)
    {
        var subtask =
            await GetOwnedSubtaskAsync(subtaskId, userId);

        subtask.Title = ValidateTitle(request.Title);
        subtask.UpdatedAt = DateTime.UtcNow;

        _subtaskRepository.Update(subtask);

        await _subtaskRepository.SaveChangesAsync();

        return Map(subtask);
    }

    public async Task<SubtaskResponse> ToggleAsync(Guid subtaskId, Guid userId)
    {
        var subtask =
            await GetOwnedSubtaskAsync(subtaskId, userId);

        var task =
            await GetOwnedTaskAsync(subtask.TaskId, userId);

        subtask.IsCompleted = !subtask.IsCompleted;
        subtask.UpdatedAt = DateTime.UtcNow;

        _subtaskRepository.Update(subtask);

        ApplyTaskStatusRule(task);
        _taskRepository.Update(task);

        await _subtaskRepository.SaveChangesAsync();

        return Map(subtask);
    }

    public async Task DeleteAsync(Guid subtaskId, Guid userId)
    {
        var subtask =
            await GetOwnedSubtaskAsync(subtaskId, userId);

        var task =
            await GetOwnedTaskAsync(subtask.TaskId, userId);

        subtask.IsDeleted = true;
        subtask.UpdatedAt = DateTime.UtcNow;

        _subtaskRepository.Delete(subtask);

        ApplyTaskStatusRule(task);
        _taskRepository.Update(task);

        await _subtaskRepository.SaveChangesAsync();
    }

    public async Task<List<SubtaskResponse>> ReorderAsync(ReorderSubtasksRequest request, Guid userId)
    {
        var task =
            await GetOwnedTaskAsync(request.TaskId, userId);

        var activeSubtasks =
            task.Subtasks
                .Where(s => !s.IsDeleted)
                .ToList();

        if (request.OrderedSubtaskIds.Count != activeSubtasks.Count ||
            request.OrderedSubtaskIds.Distinct().Count() != request.OrderedSubtaskIds.Count ||
            request.OrderedSubtaskIds.Any(id => activeSubtasks.All(s => s.Id != id)))
        {
            throw new ArgumentException(
                "Ordered subtask ids must match the task's subtasks"
            );
        }

        var subtasksById =
            activeSubtasks.ToDictionary(s => s.Id);

        for (var index = 0; index < request.OrderedSubtaskIds.Count; index++)
        {
            var subtask =
                subtasksById[request.OrderedSubtaskIds[index]];

            subtask.Position = index;
            subtask.UpdatedAt = DateTime.UtcNow;

            _subtaskRepository.Update(subtask);
        }

        await _subtaskRepository.SaveChangesAsync();

        return activeSubtasks
            .OrderBy(s => s.Position)
            .Select(Map)
            .ToList();
    }

    private async Task<TaskItem> GetOwnedTaskAsync(Guid taskId, Guid userId)
    {
        var task =
            await _taskRepository.GetByIdAsync(taskId, userId);

        if (task == null)
        {
            throw new KeyNotFoundException(
                "Task not found"
            );
        }

        return task;
    }

    private async Task<SubtaskItem> GetOwnedSubtaskAsync(Guid subtaskId, Guid userId)
    {
        var subtask =
            await _subtaskRepository.GetByIdAsync(subtaskId);

        if (subtask == null || subtask.IsDeleted ||
            subtask.Task.UserId != userId ||
            subtask.Task.IsDeleted)
        {
            throw new KeyNotFoundException(
                "Subtask not found"
            );
        }

        return subtask;
    }

    private static string ValidateTitle(string title)
    {
        var trimmed = title.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException(
                "Title is required"
            );
        }

        if (trimmed.Length > 255)
        {
            throw new ArgumentException(
                "Title must be at most 255 characters"
            );
        }

        return trimmed;
    }

    private static void ApplyTaskStatusRule(TaskItem task)
    {
        var activeSubtasks =
            task.Subtasks
                .Where(s => !s.IsDeleted)
                .ToList();

        if (activeSubtasks.Count == 0)
        {
            return;
        }

        if (activeSubtasks.All(s => s.IsCompleted))
        {
            task.Status = TaskStatus.Done;
        }
        else if (task.Status == TaskStatus.Done)
        {
            task.Status = TaskStatus.InProgress;
        }
    }

    private static SubtaskResponse Map(SubtaskItem subtask)
    {
        return new SubtaskResponse
        {
            Id = subtask.Id,
            TaskId = subtask.TaskId,
            Title = subtask.Title,
            IsCompleted = subtask.IsCompleted,
            Position = subtask.Position,
            CreatedAt = subtask.CreatedAt,
            UpdatedAt = subtask.UpdatedAt
        };
    }
}
