using SourceGuild.Domain.Common;
using SourceGuild.Domain.Enums;

namespace SourceGuild.Domain.Entities;

public class Section : IOrderable
{
    private readonly List<Lesson> _lessons = [];

    // Constructor privado para EF Core
    private Section() { }

    internal static Result<Section> Create(Guid courseId, string title, int order)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result<Section>.Failure(Error.Validation("Section.EmptyTitle", "El título de la sección es obligatorio."));

        var section = new Section
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Title = title.Trim(),
            Order = order,
            Status = SectionStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return Result<Section>.Success(section);
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public SectionStatus Status { get; private set; } = SectionStatus.Draft;
    
    public Guid CourseId { get; private set; }
    public Course Course { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyCollection<Lesson> Lessons => _lessons.OrderBy(l => l.Order).ToList().AsReadOnly();

    public Result UpdateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure(Error.Validation("Section.EmptyTitle", "El título de la sección es obligatorio."));

        Title = title.Trim();
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    internal void SetOrder(int newOrder)
    {
        Order = newOrder;
        UpdatedAt = DateTime.UtcNow;
    }

    public Result<Lesson> AddLesson(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result<Lesson>.Failure(Error.Validation("Lesson.EmptyTitle", "El título de la lección es obligatorio."));

        int nextOrder = _lessons.Count;
        var lessonResult = Lesson.Create(Id, title, nextOrder);

        if (lessonResult.IsFailure)
            return Result<Lesson>.Failure(lessonResult.Error);

        _lessons.Add(lessonResult.Value);
        UpdatedAt = DateTime.UtcNow;

        return lessonResult;
    }

    public Result RemoveLesson(Guid lessonId)
    {
        var lesson = _lessons.FirstOrDefault(l => l.Id == lessonId);
        if (lesson is null)
            return Result.Failure(Error.NotFound("Lesson.NotFound", "La lección no existe en esta sección."));

        _lessons.Remove(lesson);

        // Re-indexar para mantener la secuencia 0, 1, 2...
        _lessons.Reindex();

        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    void IOrderable.SetOrder(int newOrder) => SetOrder(newOrder);
}