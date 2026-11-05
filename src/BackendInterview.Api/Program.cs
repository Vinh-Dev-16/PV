using BackendInterview.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<NotionOptions>(builder.Configuration.GetSection("Notion"));
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<NotionReader>(client =>
{
    client.BaseAddress = new Uri("https://api.notion.com/v1/");
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/roadmap", async (NotionReader notion, CancellationToken cancellationToken) =>
    await SafeRead(() => notion.GetRoadmapAsync(cancellationToken)));
app.MapGet("/api/topics/{pageId}", async (string pageId, NotionReader notion, CancellationToken cancellationToken) =>
    await SafeRead(() => notion.GetTopicAsync(pageId, cancellationToken)));
app.MapGet("/api/progress", async (NotionReader notion, CancellationToken cancellationToken) =>
    await SafeRead(() => notion.GetProgressAsync(cancellationToken)));
app.MapGet("/api/reviews", async (NotionReader notion, string? topicId, CancellationToken cancellationToken) =>
    await SafeRead(() => notion.GetReviewsAsync(topicId, cancellationToken)));
app.MapGet("/api/quick-questions", async (NotionReader notion, string? search, string? category, string? difficulty, string? status, string? cursor, CancellationToken cancellationToken) =>
    await SafeRead(() => notion.GetQuickQuestionsAsync(search, category, difficulty, status, cursor, cancellationToken)));
app.MapGet("/api/quick-questions/{pageId}", async (string pageId, NotionReader notion, CancellationToken cancellationToken) =>
    await SafeRead(() => notion.GetQuickQuestionAsync(pageId, cancellationToken)));

app.Run();

static async Task<IResult> SafeRead<T>(Func<Task<T>> read)
{
    try
    {
        return Results.Ok(await read());
    }
    catch (NotionReadException exception)
    {
        return Results.Problem(
            title: exception.Title,
            detail: exception.Message,
            statusCode: exception.StatusCode,
            extensions: new Dictionary<string, object?> { ["source"] = "notion" });
    }
}
