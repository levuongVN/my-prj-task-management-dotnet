using TaskFlow.Application.Common;
using TaskFlow.Application.Features.AI.DTOs;

namespace TaskFlow.Application.Features.AI.Interfaces;

public interface IAiService
{
    IAsyncEnumerable<AiStreamEvent> StreamChatAsync(
        AiChatRequest request,
        Guid userId,
        CancellationToken cancellationToken = default
    );

    Task<PagedResult<AiSessionResponse>> GetSessionsAsync(Guid userId, int page, int pageSize);

    Task<PagedResult<AiChatMessageResponse>> GetSessionMessagesAsync(
        Guid sessionId,
        Guid userId,
        int page,
        int pageSize
    );

    Task DeleteSessionAsync(Guid sessionId, Guid userId);

    Task<AiTaskDraftResponse> ParseTaskDraftAsync(string text, Guid userId);

    Task<List<AiSubtaskSuggestionResponse>> SuggestTaskBreakdownAsync(Guid taskId, Guid userId);
}
