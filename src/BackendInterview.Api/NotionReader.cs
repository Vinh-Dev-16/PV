using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BackendInterview.Api;

public sealed class NotionOptions
{
    public string RootPageId { get; set; } = "3c150726-d06c-8083-87c3-e035b655ac91";
    public string ApiVersion { get; set; } = "2026-03-11";
    public int CacheSeconds { get; set; } = 120;
    public string? ApiToken { get; set; }
    public string QuickQuestionsDataSourceId { get; set; } = "3b750726-d06c-807f-80db-000bf18aaa30";
}

public sealed class NotionReader
{
    private const int PageSize = 100;
    private const int MaximumBlocks = 5000;
    private const int MaximumDepth = 6;
    private static readonly Regex DatePattern = new(@"\b(20\d{2})[-/.](\d{1,2})[-/.](\d{1,2})\b", RegexOptions.Compiled);
    private readonly HttpClient _http;
    private readonly NotionOptions _options;
    private readonly IMemoryCache _cache;

    public NotionReader(HttpClient http, IOptions<NotionOptions> options, IMemoryCache cache)
    {
        _http = http;
        _options = options.Value;
        _cache = cache;

        var token = string.IsNullOrWhiteSpace(_options.ApiToken)
            ? Environment.GetEnvironmentVariable("NOTION_API_TOKEN")
            : _options.ApiToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        _http.DefaultRequestHeaders.Add("Notion-Version", _options.ApiVersion);
    }

    public Task<IReadOnlyList<TopicCard>> GetRoadmapAsync(CancellationToken cancellationToken) =>
        _cache.GetOrCreateAsync("roadmap:v1", async entry =>
        {
            SetCache(entry);
            using var document = await GetJsonAsync($"blocks/{_options.RootPageId}/children?page_size={PageSize}", cancellationToken);
            var topics = new List<TopicCard>();
            foreach (var block in ReadResults(document.RootElement))
            {
                if (!block.TryGetProperty("type", out var type) || type.GetString() != "child_page") continue;
                var id = block.GetProperty("id").GetString();
                var title = block.GetProperty("child_page").TryGetProperty("title", out var titleNode)
                    ? titleNode.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title)) continue;
                topics.Add(new TopicCard(id, title, topics.Count + 1, $"https://www.notion.so/{id.Replace("-", "", StringComparison.Ordinal)}"));
            }

            return (IReadOnlyList<TopicCard>)topics;
        })!;

    public async Task<TopicPage> GetTopicAsync(string pageId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(pageId, out _)) throw new NotionReadException("Topic không hợp lệ", "Page ID không đúng định dạng.", 400);
        var topics = await GetRoadmapAsync(cancellationToken);
        var topic = topics.FirstOrDefault(item => string.Equals(item.Id, pageId, StringComparison.OrdinalIgnoreCase));
        if (topic is null) throw new NotionReadException("Không tìm thấy topic", "Page không thuộc mục lục Kiến thức sâu.", 404);

        var cachedPage = await _cache.GetOrCreateAsync<TopicPage>($"topic:{topic.Id}", async entry =>
        {
            SetCache(entry);
            using var page = await GetJsonAsync($"pages/{topic.Id}", cancellationToken);
            var title = GetPageTitle(page.RootElement);
            var counter = new BlockCounter();
            var content = await ReadBlocksAsync(topic.Id, 0, counter, cancellationToken);
            var resolvedTopic = topic with { Title = string.IsNullOrWhiteSpace(title) ? topic.Title : title };
            return new TopicPage(resolvedTopic, content, !counter.Truncated, DateTimeOffset.UtcNow);
        });
        return cachedPage ?? throw new InvalidOperationException("Topic cache factory returned no page.");
    }

    public async Task<ProgressSummary> GetProgressAsync(CancellationToken cancellationToken)
    {
        var cachedProgress = await _cache.GetOrCreateAsync<ProgressSummary>("progress:v1", async entry =>
        {
            SetCache(entry);
            var topics = await GetRoadmapAsync(cancellationToken);
            if (topics.Count == 0) return new ProgressSummary(0, 0, 0, 0, null, "no-data");

            var readable = 0;
            var completed = 0;
            var total = 0;
            var contentComplete = true;
            foreach (var topic in topics)
            {
                try
                {
                    var page = await GetTopicAsync(topic.Id, cancellationToken);
                    readable++;
                    contentComplete &= page.IsComplete;
                    CountChecklist(page.Content, ref completed, ref total);
                }
                catch (NotionReadException)
                {
                    // Keep inaccessible topics visible in the roadmap; never infer their checklist state.
                }
            }

            var allTopicsReadable = readable == topics.Count;
            var allContentComplete = allTopicsReadable && contentComplete;
            double? percent = total == 0 || !allTopicsReadable || !allContentComplete ? null : Math.Round((double)completed / total * 100, 1);
            var state = !allTopicsReadable ? "partial-source" : !allContentComplete ? "partial-content" : total == 0 ? "no-status-data" : "available";
            return new ProgressSummary(topics.Count, readable, completed, total, percent, state);
        });
        return cachedProgress ?? throw new InvalidOperationException("Progress cache factory returned no summary.");
    }

    public async Task<IReadOnlyList<ReviewEntry>> GetReviewsAsync(string? topicId, CancellationToken cancellationToken)
    {
        var topics = await GetRoadmapAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(topicId))
        {
            topics = topics.Where(topic => string.Equals(topic.Id, topicId, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        var reviews = new List<ReviewEntry>();
        foreach (var topic in topics)
        {
            try
            {
                var page = await GetTopicAsync(topic.Id, cancellationToken);
                CollectReviews(topic, page.Content, reviews);
            }
            catch (NotionReadException)
            {
                // An unreadable page is omitted from review history and remains visible as a topic load error.
            }
        }

        return reviews.OrderByDescending(item => item.Date ?? DateOnly.MinValue).ToArray();
    }

    public async Task<QuickQuestionResults> GetQuickQuestionsAsync(
        string? search, string? category, string? difficulty, string? status, string? cursor, CancellationToken cancellationToken)
    {
        var cacheKey = $"quick-questions:v1:{search}:{category}:{difficulty}:{status}:{cursor}";
        var result = await _cache.GetOrCreateAsync<QuickQuestionResults>(cacheKey, async entry =>
        {
            SetCache(entry);
            var filters = new List<object>();
            if (!string.IsNullOrWhiteSpace(search))
            {
                filters.Add(new { or = new object[]
                {
                    new { property = "Content", title = new { contains = search.Trim() } },
                    new { property = "Câu chuẩn", rich_text = new { contains = search.Trim() } },
                    new { property = "Chủ đề", rich_text = new { contains = search.Trim() } }
                }});
            }
            if (!string.IsNullOrWhiteSpace(category)) filters.Add(new { property = "Nhóm chủ đề", select = new { equals = category } });
            if (!string.IsNullOrWhiteSpace(difficulty)) filters.Add(new { property = "Độ khó", select = new { equals = difficulty } });
            if (!string.IsNullOrWhiteSpace(status)) filters.Add(new { property = "Trạng thái xử lý", select = new { equals = status } });

            var body = new Dictionary<string, object?> { ["page_size"] = 40 };
            if (!string.IsNullOrWhiteSpace(cursor)) body["start_cursor"] = cursor;
            if (filters.Count == 1) body["filter"] = filters[0];
            else if (filters.Count > 1) body["filter"] = new { and = filters };
            using var document = await PostJsonAsync($"data_sources/{_options.QuickQuestionsDataSourceId}/query", body, cancellationToken);
            var items = ReadResults(document.RootElement).Select(ReadQuickQuestion).Where(item => item is not null).Cast<QuickQuestion>().ToArray();
            var nextCursor = document.RootElement.TryGetProperty("next_cursor", out var next) && next.ValueKind == JsonValueKind.String ? next.GetString() : null;
            var hasMore = document.RootElement.TryGetProperty("has_more", out var more) && more.GetBoolean();
            return new QuickQuestionResults(items, nextCursor, hasMore);
        });
        return result ?? new QuickQuestionResults([], null, false);
    }

    public async Task<QuickQuestionDetail> GetQuickQuestionAsync(string pageId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(pageId, out _)) throw new NotionReadException("Invalid question", "Page ID is not a valid identifier.", 400);
        var cached = await _cache.GetOrCreateAsync<QuickQuestionDetail>($"quick-question:{pageId}", async entry =>
        {
            SetCache(entry);
            using var page = await GetJsonAsync($"pages/{pageId}", cancellationToken);
            var root = page.RootElement;
            if (!root.TryGetProperty("parent", out var parent) ||
                !parent.TryGetProperty("data_source_id", out var sourceId) ||
                !string.Equals(sourceId.GetString(), _options.QuickQuestionsDataSourceId, StringComparison.OrdinalIgnoreCase))
                throw new NotionReadException("Question not found", "This page is not in the Quick Interview Questions data source.", 404);
            var question = ReadQuickQuestion(root);
            if (question is null) throw new NotionReadException("Question not found", "This page has no question content.", 404);
            var counter = new BlockCounter();
            var content = await ReadBlocksAsync(pageId, 0, counter, cancellationToken);
            return new QuickQuestionDetail(question, content, !counter.Truncated, DateTimeOffset.UtcNow);
        });
        return cached ?? throw new InvalidOperationException("Quick question cache factory returned no page.");
    }

    private async Task<IReadOnlyList<ContentBlock>> ReadBlocksAsync(string blockId, int depth, BlockCounter counter, CancellationToken cancellationToken)
    {
        if (depth > MaximumDepth || counter.Value >= MaximumBlocks)
        {
            counter.Truncated = true;
            return [];
        }
        var blocks = new List<ContentBlock>();
        string? cursor = null;
        do
        {
            var path = $"blocks/{blockId}/children?page_size={PageSize}";
            if (!string.IsNullOrWhiteSpace(cursor)) path += $"&start_cursor={Uri.EscapeDataString(cursor)}";
            using var document = await GetJsonAsync(path, cancellationToken);
            foreach (var item in ReadResults(document.RootElement))
            {
                if (++counter.Value > MaximumBlocks)
                {
                    counter.Truncated = true;
                    break;
                }
                var type = item.TryGetProperty("type", out var typeNode) ? typeNode.GetString() ?? "unknown" : "unknown";
                var id = item.TryGetProperty("id", out var idNode) ? idNode.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
                var payload = item.TryGetProperty(type, out var value) ? value : default;
                var text = ReadRichText(payload);
                var url = ReadUrl(payload);
                var language = type == "code" && payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("language", out var languageNode)
                    ? languageNode.GetString()
                    : null;
                bool? isChecked = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("checked", out var checkedNode)
                    ? checkedNode.GetBoolean()
                    : null;
                IReadOnlyList<ContentBlock> children = [];
                if (item.TryGetProperty("has_children", out var hasChildren) && hasChildren.GetBoolean() && depth < MaximumDepth)
                {
                    children = await ReadBlocksAsync(id, depth + 1, counter, cancellationToken);
                }
                else if (item.TryGetProperty("has_children", out hasChildren) && hasChildren.GetBoolean())
                {
                    counter.Truncated = true;
                }
                blocks.Add(new ContentBlock(id, type, text, isChecked, url, children, language));
            }

            cursor = document.RootElement.TryGetProperty("next_cursor", out var cursorNode) && cursorNode.ValueKind == JsonValueKind.String
                ? cursorNode.GetString()
                : null;
            var hasMore = document.RootElement.TryGetProperty("has_more", out var moreNode) && moreNode.GetBoolean();
            if (!hasMore) break;
            if (counter.Value >= MaximumBlocks)
            {
                counter.Truncated = true;
                break;
            }
            if (string.IsNullOrWhiteSpace(cursor))
            {
                counter.Truncated = true;
                break;
            }
        } while (true);

        return blocks;
    }

    private async Task<JsonDocument> GetJsonAsync(string path, CancellationToken cancellationToken)
    {
        if (_http.DefaultRequestHeaders.Authorization is null)
            throw new NotionReadException("Notion chưa được cấu hình", "Hãy đặt biến môi trường NOTION_API_TOKEN cho tiến trình backend.", 503);

        using var response = await _http.GetAsync(path, cancellationToken);
        if (response.IsSuccessStatusCode) return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        var status = (int)response.StatusCode;
        var detail = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Notion từ chối token. Kiểm tra integration token ở backend.",
            HttpStatusCode.Forbidden => "Integration chưa được share page cần đọc.",
            HttpStatusCode.NotFound => "Page hoặc block không tồn tại hoặc chưa được share với integration.",
            (HttpStatusCode)429 => "Notion giới hạn tần suất đọc. Hãy thử lại sau.",
            _ when status >= 500 => "Notion đang gặp sự cố. Thử lại sau.",
            _ => "Không thể đọc dữ liệu từ Notion."
        };
        throw new NotionReadException("Không đọc được Notion", detail, status is >= 400 and < 500 ? status : 502);
    }

    // Notion's data-source query endpoint uses POST for a read-only query; this method never writes page data.
    private async Task<JsonDocument> PostJsonAsync(string path, object body, CancellationToken cancellationToken)
    {
        if (_http.DefaultRequestHeaders.Authorization is null)
            throw new NotionReadException("Notion chưa được cấu hình", "Hãy cấu hình Notion:ApiToken bằng User Secrets hoặc biến môi trường.", 503);
        using var response = await _http.PostAsJsonAsync(path, body, cancellationToken);
        if (response.IsSuccessStatusCode)
            return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var status = (int)response.StatusCode;
        var detail = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Notion rejected the token. Check the backend integration token.",
            HttpStatusCode.Forbidden => "The integration has not been shared with the question data source.",
            HttpStatusCode.NotFound => "The question data source was not found or is not shared with the integration.",
            (HttpStatusCode)429 => "Notion rate limited this read. Retry later.",
            _ when status >= 500 => "Notion is temporarily unavailable. Retry later.",
            _ => "The question list could not be read from Notion."
        };
        throw new NotionReadException("Notion read failed", detail, status is >= 400 and < 500 ? status : 502);
    }

    private static QuickQuestion? ReadQuickQuestion(JsonElement page)
    {
        if (!page.TryGetProperty("properties", out var properties)) return null;
        var id = page.TryGetProperty("id", out var idNode) ? idNode.GetString() : null;
        var title = ReadPropertyText(properties, "Content");
        var standard = ReadPropertyText(properties, "Câu chuẩn");
        if (string.IsNullOrWhiteSpace(title)) title = standard;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title)) return null;
        var dateText = ReadPropertyDate(properties, "Date");
        DateOnly? date = DateOnly.TryParse(dateText, CultureInfo.InvariantCulture, out var parsedDate) ? parsedDate : null;
        var url = page.TryGetProperty("url", out var urlNode) ? urlNode.GetString() : null;
        return new QuickQuestion(
            id, title, standard, ReadPropertyText(properties, "Chủ đề"), ReadPropertySelect(properties, "Nhóm chủ đề"),
            ReadPropertySelect(properties, "Độ khó"), ReadPropertySelect(properties, "Trạng thái xử lý"),
            ReadPropertySelect(properties, "Trạng thái đáp án"), ReadPropertySelect(properties, "Status"), date, url ?? string.Empty);
    }

    private static string ReadPropertyText(JsonElement properties, string name) =>
        properties.TryGetProperty(name, out var property) ? ReadRichText(property) : string.Empty;

    private static string ReadPropertySelect(JsonElement properties, string name)
    {
        if (!properties.TryGetProperty(name, out var property)) return string.Empty;
        foreach (var key in new[] { "select", "status" })
            if (property.TryGetProperty(key, out var selected) && selected.ValueKind == JsonValueKind.Object && selected.TryGetProperty("name", out var value)) return value.GetString() ?? string.Empty;
        return string.Empty;
    }

    private static string? ReadPropertyDate(JsonElement properties, string name) =>
        properties.TryGetProperty(name, out var property) && property.TryGetProperty("date", out var date) && date.ValueKind == JsonValueKind.Object && date.TryGetProperty("start", out var start)
            ? start.GetString() : null;

    private static IEnumerable<JsonElement> ReadResults(JsonElement root) =>
        root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array
            ? results.EnumerateArray()
            : [];

    private static string GetPageTitle(JsonElement page)
    {
        if (!page.TryGetProperty("properties", out var properties)) return string.Empty;
        foreach (var property in properties.EnumerateObject())
        {
            if (property.Value.TryGetProperty("type", out var type) && type.GetString() == "title" &&
                property.Value.TryGetProperty("title", out var title)) return ReadTextArray(title);
        }
        return string.Empty;
    }

    private static string ReadRichText(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object) return string.Empty;
        foreach (var key in new[] { "rich_text", "title", "caption" })
            if (payload.TryGetProperty(key, out var text) && text.ValueKind == JsonValueKind.Array) return ReadTextArray(text);
        if (payload.TryGetProperty("title", out var titleNode) && titleNode.ValueKind == JsonValueKind.String) return titleNode.GetString() ?? string.Empty;
        return string.Empty;
    }

    private static string ReadTextArray(JsonElement array) => string.Join("", array.EnumerateArray().Select(item =>
    {
        if (item.TryGetProperty("plain_text", out var plain)) return plain.GetString() ?? string.Empty;
        if (item.TryGetProperty("text", out var text) && text.TryGetProperty("content", out var content)) return content.GetString() ?? string.Empty;
        return string.Empty;
    }));

    private static string? ReadUrl(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object) return null;
        if (payload.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String) return url.GetString();
        if (payload.TryGetProperty("rich_text", out var richText))
        {
            foreach (var item in richText.EnumerateArray())
                if (item.TryGetProperty("href", out var href) && href.ValueKind == JsonValueKind.String) return href.GetString();
        }
        return null;
    }

    private static void CountChecklist(IEnumerable<ContentBlock> blocks, ref int completed, ref int total)
    {
        foreach (var block in blocks)
        {
            if (block.Type == "to_do" && block.Checked.HasValue)
            {
                total++;
                if (block.Checked.Value) completed++;
            }
            CountChecklist(block.Children, ref completed, ref total);
        }
    }

    private static void CollectReviews(TopicCard topic, IEnumerable<ContentBlock> blocks, List<ReviewEntry> reviews)
    {
        foreach (var block in blocks)
        {
            var dateMatch = DatePattern.Match(block.Text);
            DateOnly? date = null;
            if (dateMatch.Success && DateOnly.TryParseExact(dateMatch.Value.Replace('/', '-').Replace('.', '-'), "yyyy-M-d", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) date = parsed;
            if (!string.IsNullOrWhiteSpace(block.Text) &&
                (block.Text.Contains("review", StringComparison.OrdinalIgnoreCase) || block.Text.Contains("đánh giá", StringComparison.OrdinalIgnoreCase) || date.HasValue))
            {
                reviews.Add(new ReviewEntry(block.Id, topic.Id, topic.Title, block.Text, date, block.Url));
            }
            CollectReviews(topic, block.Children, reviews);
        }
    }

    private void SetCache(ICacheEntry entry) => entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Clamp(_options.CacheSeconds, 15, 600));
    private sealed class BlockCounter { public int Value { get; set; } public bool Truncated { get; set; } }
}
