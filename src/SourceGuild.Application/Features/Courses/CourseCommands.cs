using SourceGuild.Application.Common.Mappings;
using SourceGuild.Application.DTOs.ContentBlock;
using SourceGuild.Application.DTOs.Course;
using SourceGuild.Application.DTOs.Lesson;
using SourceGuild.Application.DTOs.Section;
using SourceGuild.Application.Interfaces.Common;
using SourceGuild.Application.Interfaces.Persistence;
using SourceGuild.Domain.Common;
using SourceGuild.Domain.Entities;

namespace SourceGuild.Application.Features.Courses;

public class CourseCommands(
    ICourseRepository courseRepository,
    ICategoryRepository categoryRepository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<CourseDto>> CreateAsync(CreateCourseDto dto, CancellationToken cancellationToken = default)
    {
        if (!currentUserService.UserId.HasValue || currentUserService.UserId.Value == Guid.Empty)
            return Result<CourseDto>.Failure(Error.Validation("Auth.Unauthorized", "Usuario no autenticado."));

        var instructorId = currentUserService.UserId.Value;

        var courseResult = Course.Create(
            dto.Title,
            instructorId,
            dto.Price,
            dto.Description,
            dto.DurationInWeeks,
            dto.ImageUrl,
            dto.StartDate);

        if (courseResult.IsFailure)
            return Result<CourseDto>.Failure(courseResult.Error);

        var course = courseResult.Value;

        // Asignación de Categorías si aplican
        if (dto.CategoryIds is not null && dto.CategoryIds.Count > 0)
        {
            foreach (var catId in dto.CategoryIds)
            {
                var category = await categoryRepository.GetByIdAsync(catId, cancellationToken);
                if (category is not null)
                {
                    // EF Core rastreará la relación Many-to-Many
                    ((List<Category>)course.Categories).Add(category);
                }
            }
        }

        var createdCourse = await courseRepository.AddAsync(course, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CourseDto>.Success(createdCourse.ToDto());
    }

    public async Task<Result> UpdatePriceAsync(Guid courseId, decimal newPrice, CancellationToken cancellationToken = default)
    {
        var courseResult = await GetCourseAndVerifyOwnershipAsync(courseId, includeDetails: false, cancellationToken);
        if (courseResult.IsFailure) return Result.Failure(courseResult.Error);
        
        var course = courseResult.Value;

        var updateResult = course.UpdatePrice(newPrice);
        if (updateResult.IsFailure)
            return updateResult;

        await courseRepository.UpdateAsync(course, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> PublishAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var courseResult = await GetCourseAndVerifyOwnershipAsync(courseId, includeDetails: true, cancellationToken);
        if (courseResult.IsFailure) return Result.Failure(courseResult.Error);
        
        var course = courseResult.Value;

        var publishResult = course.Publish();
        if (publishResult.IsFailure)
            return publishResult;

        await courseRepository.UpdateAsync(course, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    // ==========================================
    // Operaciones sobre Secciones (Vía Aggregate Root)
    // ==========================================

    public async Task<Result<SectionDto>> AddSectionAsync(Guid courseId, CreateSectionDto dto, CancellationToken cancellationToken = default)
    {
        var courseResult = await GetCourseAndVerifyOwnershipAsync(courseId, includeDetails: true, cancellationToken);
        if (courseResult.IsFailure) return Result<SectionDto>.Failure(courseResult.Error);
        
        var course = courseResult.Value;

        var sectionResult = course.AddSection(dto.Title);
        if (sectionResult.IsFailure)
            return Result<SectionDto>.Failure(sectionResult.Error);

        await courseRepository.UpdateAsync(course, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<SectionDto>.Success(sectionResult.Value.ToDto());
    }

    public async Task<Result<LessonDto>> AddLessonToSectionAsync(
        Guid courseId,
        Guid sectionId,
        CreateLessonDto dto,
        CancellationToken cancellationToken = default)
    {
        var courseResult = await GetCourseAndVerifyOwnershipAsync(courseId, includeDetails: true, cancellationToken);
        if (courseResult.IsFailure) return Result<LessonDto>.Failure(courseResult.Error);
        
        var course = courseResult.Value;

        var section = course.Sections.FirstOrDefault(s => s.Id == sectionId);
        if (section is null)
            return Result<LessonDto>.Failure(Error.NotFound("Section.NotFound", "La sección no pertenece a este curso."));

        var lessonResult = section.AddLesson(dto.Title);
        if (lessonResult.IsFailure)
            return Result<LessonDto>.Failure(lessonResult.Error);

        var lesson = lessonResult.Value;

        // Agregar bloques de contenido iniciales si vienen en el DTO
        if (dto.ContentBlocks is not null)
        {
            foreach (var blockDto in dto.ContentBlocks)
            {
                if (blockDto is CreateTextContentDto textDto)
                {
                    lesson.AddTextContent(textDto.Text);
                }
                else if (blockDto is CreateVideoContentDto videoDto)
                {
                    lesson.AddVideoContent(videoDto.VideoUrl, videoDto.DurationMinutes, videoDto.ThumbnailUrl, videoDto.Transcription);
                }
            }
        }

        await courseRepository.UpdateAsync(course, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<LessonDto>.Success(lesson.ToDto());
    }

    private async Task<Result<Course>> GetCourseAndVerifyOwnershipAsync(Guid courseId, bool includeDetails, CancellationToken cancellationToken)
    {
        var course = includeDetails
            ? await courseRepository.GetWithDetailsAsync(courseId, cancellationToken)
            : await courseRepository.GetByIdAsync(courseId, cancellationToken);

        if (course is null)
            return Result<Course>.Failure(Error.NotFound("Course.NotFound", "El curso no existe."));

        if (course.InstructorId != currentUserService.UserId)
            return Result<Course>.Failure(Error.Validation("Auth.Forbidden", "No tiene permisos para modificar este curso."));

        return Result<Course>.Success(course);
    }
}