namespace BackendInterview.Api;

public sealed record TopicCard(string Id, string Title, int Position, string Url);

public sealed record TopicPage(
    TopicCard Topic,
    IReadOnlyList<ContentBlock> Content,
    bool IsComplete,
    DateTimeOffset RetrievedAt);

public sealed record ContentBlock(
    string Id,
    string Type,
    string Text,
    bool? Checked,
    string? Url,
    IReadOnlyList<ContentBlock> Children,
    string? Language = null);

public sealed record ProgressSummary(
    int Topics,
    int ReadableTopics,
    int CompletedItems,
    int TotalItems,
    double? Percent,
    string State);

public sealed record ReviewEntry(
    string Id,
    string TopicId,
    string TopicTitle,
    string Note,
    DateOnly? Date,
    string? Url);

public sealed record QuickQuestion(
    string Id,
    string Title,
    string StandardQuestion,
    string Topic,
    string Category,
    string Difficulty,
    string ProcessingStatus,
    string AnswerStatus,
    string Status,
    DateOnly? Date,
    string Url);

public sealed record QuickQuestionResults(
    IReadOnlyList<QuickQuestion> Items,
    string? NextCursor,
    bool HasMore);

public sealed record QuickQuestionDetail(
    QuickQuestion Question,
    IReadOnlyList<ContentBlock> Content,
    bool IsComplete,
    DateTimeOffset RetrievedAt);

public sealed class NotionReadException(string title, string message, int statusCode = 502)
    : Exception(message)
{
    public string Title { get; } = title;
    public int StatusCode { get; } = statusCode;
}
