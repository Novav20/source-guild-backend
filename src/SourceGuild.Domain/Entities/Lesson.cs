using SourceGuild.Domain.Common;
using SourceGuild.Domain.Entities.Content;
using SourceGuild.Domain.Enums;

namespace SourceGuild.Domain.Entities;

public class Lesson : IOrderable
{
    private readonly List<ContentBlock> _contentBlocks = [];

    // Constructor privado para EF Core
    private Lesson() { }

    internal static Result<Lesson> Create(Guid sectionId, string title, int order)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result<Lesson>.Failure(Error.Validation("Lesson.EmptyTitle", "El título de la lección es obligatorio."));

        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            SectionId = sectionId,
            Title = title.Trim(),
            Order = order,
            Status = LessonStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return Result<Lesson>.Success(lesson);
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public LessonStatus Status { get; private set; } = LessonStatus.Draft;

    public Guid SectionId { get; private set; }
    public Section Section { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyCollection<ContentBlock> ContentBlocks => _contentBlocks.OrderBy(c => c.Order).ToList().AsReadOnly();

    public Result UpdateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure(Error.Validation("Lesson.EmptyTitle", "El título de la lección es obligatorio."));

        Title = title.Trim();
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    internal void SetOrder(int newOrder)
    {
        Order = newOrder;
        UpdatedAt = DateTime.UtcNow;
    }

    public Result<TextContent> AddTextContent(string text)
    {
        int nextOrder = _contentBlocks.Count;
        var textResult = TextContent.Create(Id, text, nextOrder);

        if (textResult.IsFailure)
            return Result<TextContent>.Failure(textResult.Error);

        _contentBlocks.Add(textResult.Value);
        UpdatedAt = DateTime.UtcNow;

        return textResult;
    }

    public Result<VideoContent> AddVideoContent(string videoUrl, int durationMinutes, string? thumbnailUrl = null, string? transcription = null)
    {
        int nextOrder = _contentBlocks.Count;
        var videoResult = VideoContent.Create(Id, videoUrl, durationMinutes, nextOrder, thumbnailUrl, transcription);

        if (videoResult.IsFailure)
            return Result<VideoContent>.Failure(videoResult.Error);

        _contentBlocks.Add(videoResult.Value);
        UpdatedAt = DateTime.UtcNow;

        return videoResult;
    }

    public Result RemoveContentBlock(Guid contentBlockId)
    {
        var block = _contentBlocks.FirstOrDefault(c => c.Id == contentBlockId);
        if (block is null)
            return Result.Failure(Error.NotFound("ContentBlock.NotFound", "El bloque de contenido no existe en esta lección."));

        _contentBlocks.Remove(block);

        _contentBlocks.Reindex();

        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    void IOrderable.SetOrder(int newOrder) => SetOrder(newOrder);
}