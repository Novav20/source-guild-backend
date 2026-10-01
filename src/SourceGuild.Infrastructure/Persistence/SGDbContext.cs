using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using SourceGuild.Domain.Entities;
using SourceGuild.Domain.Entities.Content;
using SourceGuild.Domain.Enums;

namespace SourceGuild.Infrastructure.Persistence;

public class SGDbContext(DbContextOptions<SGDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Course> Courses { get; set; }
    public DbSet<Section> Sections { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Lesson> Lessons { get; set; }
    public DbSet<ContentBlock> ContentBlocks { get; set; }
    public DbSet<Enrollment> Enrollments { get; set; }
    public DbSet<Review> Reviews { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // CRUCIAL first call

        modelBuilder.Entity<Course>(entity =>
        {
            entity.Property(c => c.Price)
                .HasColumnType("decimal(18,2)");

            entity.HasMany(c => c.Categories)
                .WithMany(cat => cat.Courses);

            entity.HasOne(c => c.Instructor)
                .WithMany(u => u.CreatedCourses)
                .HasForeignKey(c => c.InstructorId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(c => c.Description)
                .HasMaxLength(500);
        });

        modelBuilder.Entity<ApplicationUser>(b => { b.ToTable("AppUsers"); });
        modelBuilder.Entity<IdentityRole<Guid>>(b => { b.ToTable("AppRoles"); });
        modelBuilder.Entity<IdentityUserRole<Guid>>(b => { b.ToTable("AppUserRoles"); }); // Join table for User-Role
        modelBuilder.Entity<IdentityUserClaim<Guid>>(b => { b.ToTable("AppUserClaims"); }); // User claims
        modelBuilder.Entity<IdentityUserLogin<Guid>>(b => { b.ToTable("AppUserLogins"); }); // External logins (Google, FB, etc.)
        modelBuilder.Entity<IdentityRoleClaim<Guid>>(b => { b.ToTable("AppRoleClaims"); }); // Role claims
        modelBuilder.Entity<IdentityUserToken<Guid>>(b => { b.ToTable("AppUserTokens"); }); // Refresh tokens, 2FA tokens, etc.

        modelBuilder.Entity<Section>(entity =>
        {
            entity.Property(s => s.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(s => s.Order)
                .IsRequired();

            entity.HasOne(s => s.Course)
                .WithMany(c => c.Sections)
                .HasForeignKey(s => s.CourseId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.Property(l => l.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.HasOne(l => l.Section)
                .WithMany(s => s.Lessons)
                .HasForeignKey(l => l.SectionId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        });


        modelBuilder.Entity<ContentBlock>(entity =>
        {
            entity.HasDiscriminator<string>("ContentType")
                .HasValue<TextContent>(nameof(ContentBlockType.Text))
                .HasValue<VideoContent>(nameof(ContentBlockType.Video));

            entity.Property(cb => cb.Order)
                .IsRequired();

            entity.HasOne(cb => cb.Lesson)
                .WithMany(l => l.ContentBlocks)
                .HasForeignKey(cb => cb.LessonId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.HasOne(e => e.Course)
                .WithMany(c => c.Enrollments)
                .HasForeignKey(e => e.CourseId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Enrollments)
                .HasForeignKey(e => e.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            // Unique constraint to prevent duplicate enrollments
            entity.HasIndex(e => new { e.UserId, e.CourseId })
                .IsUnique();
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasOne(r => r.User)
                .WithMany(u => u.Reviews)
                .HasForeignKey(r => r.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Course)
                .WithMany(c => c.Reviews)
                .HasForeignKey(r => r.CourseId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(r => new { r.UserId, r.CourseId }).IsUnique();
        });


    }
}