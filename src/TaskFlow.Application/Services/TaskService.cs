using TaskFlow.Application.Common;
using TaskFlow.Application.DTOs.Labels;
using TaskFlow.Application.Features.Tasks.DTOs;
using TaskFlow.Application.Features.Tasks.Interfaces;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Features.Tasks.Services;

public class TaskService(
    ITaskRepository taskRepository,
    IProjectRepository projectRepository,
    ILabelRepository labelRepository
) : ITaskService
{
    private readonly ITaskRepository _taskRepository =
        taskRepository;

    private readonly IProjectRepository _projectRepository =
        projectRepository;

    private readonly ILabelRepository _labelRepository = labelRepository;

    public async Task<List<TaskResponse>>
        GetProjectTasksAsync(
            Guid projectId,
            Guid userId
        )
    {
        var tasks =
            await _taskRepository
                .GetByProjectIdAsync(
                    projectId,
                    userId
                );

        return tasks
            .Select(Map)
            .ToList();
    }

    public async Task<TaskResponse> GetByIdAsync(Guid taskId, Guid userId)
    {
        var task =
            await _taskRepository
                .GetByIdAsync(
                    taskId,
                    userId
                );

        if (task == null)
        {
            throw new KeyNotFoundException(
                "Task not found"
            );
        }

        return Map(task);
    }

    public async Task<TaskResponse> CreateAsync(CreateOrUpdateTaskRequest request, Guid userId)
    {
        if (request.ProjectId.HasValue)
        {
            var project = await _projectRepository.GetByIdAsync(
                    request.ProjectId.Value,
                    userId
                );

            if (project == null)
            {
                throw new Exception(
                    "Project not found"
                );
            }
        }

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Status = request.Status,
            Priority = request.Priority,
            Deadline = request.Deadline,
            UserId = userId,
            ProjectId = request.ProjectId
        };

        // Create: labelIds được gửi -> gán ngay (null/[] = không có label)
        if (request.LabelIds != null)
        {
            task.Labels = await ResolveLabelsAsync(userId, request.LabelIds);
        }

        await _taskRepository.AddAsync(task);
        await _taskRepository.SaveChangesAsync();

        return Map(task);
    }

    public async Task<TaskResponse>
        UpdateAsync(
            Guid taskId,
            Guid userId,
            CreateOrUpdateTaskRequest request
        )
    {
        var task = await _taskRepository
                .GetByIdAsync(
                    taskId,
                    userId
                );

        if (task == null)
        {
            throw new Exception(
                "Task not found"
            );
        }

        task.Title = request.Title;
        task.Description = request.Description;
        task.Status = request.Status;
        task.Priority = request.Priority;
        task.Deadline = request.Deadline;
        task.ProjectId = request.ProjectId;
        task.UpdatedAt = DateTime.UtcNow;

        // labelIds == null -> client không đụng đến labels, giữ nguyên quan hệ
        if (request.LabelIds != null)
        {
            task.Labels = await ResolveLabelsAsync(userId, request.LabelIds);
        }

        _taskRepository.Update(task);

        await _taskRepository.SaveChangesAsync();

        return Map(task);
    }

    public async Task DeleteAsync(
        Guid taskId,
        Guid userId
    )
    {
        var task =
            await _taskRepository
                .GetByIdAsync(
                    taskId,
                    userId
                );

        if (task == null)
        {
            throw new Exception(
                "Task not found"
            );
        }

        _taskRepository.Delete(task);

        await _taskRepository
            .SaveChangesAsync();
    }

    // Gán labels vào task: verify toàn bộ id thuộc user trước khi gán
    // (id của user khác/giả mạo -> IllegalArgumentException -> middleware trả 400)
    private async Task<List<Label>> ResolveLabelsAsync(Guid userId, List<Guid> labelIds)
    {
        var ids = labelIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        var labels = await _labelRepository.GetByIdsAsync(userId, ids);

        if (labels.Count != ids.Count)
        {
            throw new ArgumentException("One or more labels not found");
        }

        return labels;
    }

    private static TaskResponse Map(TaskItem task)
    {
        var activeSubtasks =
            task.Subtasks
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.Position)
                .ToList();

        var completedSubtasks =
            activeSubtasks.Count(s => s.IsCompleted);

        return new TaskResponse
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Status = (int)task.Status,
            Priority = (int)task.Priority,
            Deadline = task.Deadline,
            UserId = task.UserId,
            Position = task.Position,
            ProjectId = task.ProjectId,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            Subtasks = activeSubtasks.Select(MapSubtask).ToList(),
            Labels = task.Labels
                .Select(l => new LabelDto
                {
                    Id = l.Id,
                    Name = l.Name,
                    Color = l.Color
                })
                .ToList(),
            TotalSubtasks = activeSubtasks.Count,
            CompletedSubtasks = completedSubtasks,
            ProgressPercent = activeSubtasks.Count == 0
                ? 0
                : (int)Math.Round(100.0 * completedSubtasks / activeSubtasks.Count)
        };
    }

    private static SubtaskResponse MapSubtask(SubtaskItem subtask)
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

    public async Task<List<TaskResponse>> GetAllByUserAsync(Guid userId)
    {
        var tasks = await _taskRepository.GetAllByUserIdAsync(userId);
        return tasks.Select(Map).ToList();
    }

    public async Task<PagedResult<TaskResponse>> GetPagedByUserAsync(
        Guid userId,
        int page,
        int pageSize,
        Guid? labelId = null
    )
    {
        (page, pageSize) = PagedResult<TaskResponse>.Normalize(page, pageSize);

        var (tasks, totalCount) = await _taskRepository.GetPagedByUserIdAsync(
            userId,
            page,
            pageSize,
            labelId
        );

        return new PagedResult<TaskResponse>
        {
            Items = tasks.Select(Map).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<PagedResult<TaskResponse>> GetPagedProjectTasksAsync(
        Guid projectId,
        Guid userId,
        int page,
        int pageSize
    )
    {
        (page, pageSize) = PagedResult<TaskResponse>.Normalize(page, pageSize);

        var (tasks, totalCount) = await _taskRepository.GetPagedByProjectIdAsync(
            projectId,
            userId,
            page,
            pageSize
        );

        return new PagedResult<TaskResponse>
        {
            Items = tasks.Select(Map).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
