using SourceGuild.Domain.Common;
using SourceGuild.Domain.Enums;

namespace SourceGuild.Domain.Entities;

public class Course
{
    private readonly List<Category> _categories = [];
    private readonly List<Section> _sections = [];
    private readonly List<Enrollment> _enrollments = [];
    private readonly List<Review> _reviews = [];

    // Constructor privado para EF Core
    private Course() { }

    // Factory method para creación controlada
    public static Result<Course> Create(
        string title,
        Guid instructorId,
        decimal price = 0,
        string? description = null,
        int? durationInWeeks = null,
        string? imageUrl = null,
        DateTime? startDate = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result<Course>.Failure(Error.Validation("Course.EmptyTitle", "El título del curso es obligatorio."));

        if (price < 0)
            return Result<Course>.Failure(Error.Validation("Course.NegativePrice", "El precio del curso no puede ser negativo."));

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Description = description?.Trim(),
            Price = price,
            DurationInWeeks = durationInWeeks,
            ImageUrl = imageUrl,
            StartDate = startDate,
            InstructorId = instructorId,
            Status = CourseStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return Result<Course>.Success(course);
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public CourseStatus Status { get; private set; } = CourseStatus.Draft;
    public int? DurationInWeeks { get; private set; }
    public string? ImageUrl { get; private set; }
    public DateTime? StartDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public Guid InstructorId { get; private set; }
    public ApplicationUser? Instructor { get; private set; }

    // Colecciones encapsuladas de solo lectura (evita modificaciones externas directas)
    public IReadOnlyCollection<Category> Categories => _categories.AsReadOnly();
    public IReadOnlyCollection<Section> Sections => _sections.OrderBy(s => s.Order).ToList().AsReadOnly();
    public IReadOnlyCollection<Enrollment> Enrollments => _enrollments.AsReadOnly();
    public IReadOnlyCollection<Review> Reviews => _reviews.AsReadOnly();

    // ==========================================
    // Métodos de Negocio del Agregado (Invariantes)
    // ==========================================

    public Result UpdateDetails(string title, string? description, int? durationInWeeks, string? imageUrl, DateTime? startDate)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure(Error.Validation("Course.EmptyTitle", "El título del curso es obligatorio."));

        Title = title.Trim();
        Description = description?.Trim();
        DurationInWeeks = durationInWeeks;
        ImageUrl = imageUrl;
        StartDate = startDate;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success();
    }

    public Result UpdatePrice(decimal newPrice)
    {
        if (newPrice < 0)
            return Result.Failure(Error.Validation("Course.NegativePrice", "El precio del curso no puede ser negativo."));

        Price = newPrice;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success();
    }

    public Result Publish()
    {
        if (Status == CourseStatus.Published)
            return Result.Failure(Error.Conflict("Course.AlreadyPublished", "El curso ya se encuentra publicado."));

        if (_sections.Count == 0)
            return Result.Failure(Error.Validation("Course.NoSections", "Un curso debe tener al menos una sección para ser publicado."));

        bool hasLessons = _sections.Any(s => s.Lessons.Count > 0);
        if (!hasLessons)
            return Result.Failure(Error.Validation("Course.NoLessons", "El curso debe contener al menos una lección antes de publicarse."));

        Status = CourseStatus.Published;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success();
    }

    public Result Archive()
    {
        Status = CourseStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result<Section> AddSection(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result<Section>.Failure(Error.Validation("Section.EmptyTitle", "El título de la sección es obligatorio."));

        int nextOrder = _sections.Count; // Mantiene el orden 0, 1, 2... sin huecos
        var sectionResult = Section.Create(Id, title, nextOrder);

        if (sectionResult.IsFailure)
            return Result<Section>.Failure(sectionResult.Error);

        _sections.Add(sectionResult.Value);
        UpdatedAt = DateTime.UtcNow;

        return sectionResult;
    }

    public Result RemoveSection(Guid sectionId)
    {
        var section = _sections.FirstOrDefault(s => s.Id == sectionId);
        if (section is null)
            return Result.Failure(Error.NotFound("Section.NotFound", "La sección no existe en este curso."));

        _sections.Remove(section);

        // Re-indexar el orden de las secciones restantes para no dejar huecos
        _sections.Reindex();

        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public void AddCategory(Category category)
    {
        if (!_categories.Any(c => c.Id == category.Id))
        {
            _categories.Add(category);
            UpdatedAt = DateTime.UtcNow;
        }
    }
}