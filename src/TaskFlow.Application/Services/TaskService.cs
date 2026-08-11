using TaskFlow.Application.Features.Tasks.DTOs;
using TaskFlow.Application.Features.Tasks.Interfaces;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Features.Tasks.Services;

public class TaskService(
    ITaskRepository taskRepository,
    IProjectRepository projectRepository
) : ITaskService
{
    private readonly ITaskRepository _taskRepository =
        taskRepository;

    private readonly IProjectRepository _projectRepository =
        projectRepository;

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
            var project =
                await _projectRepository.GetByIdAsync(
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
            ProjectId = request?.ProjectId
        };

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

        task.Title = request.Title;
        task.Description = request.Description;
        task.Status = request.Status;
        task.Priority = request.Priority;
        task.Deadline = request.Deadline;
        task.ProjectId = request.ProjectId;
        task.UpdatedAt = DateTime.UtcNow;

        await _taskRepository
            .UpdateAsync(task);

        await _taskRepository
            .SaveChangesAsync();

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

        await _taskRepository
            .DeleteAsync(task);

        await _taskRepository
            .SaveChangesAsync();
    }

    private static TaskResponse Map(TaskItem task)
    {
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
            UpdatedAt = task.UpdatedAt
        };
    }

    public async Task<List<TaskResponse>> GetAllByUserAsync(Guid userId)
    {
        var tasks = await _taskRepository.GetAllByUserIdAsync(userId);
        return tasks.Select(Map).ToList();
    }
}