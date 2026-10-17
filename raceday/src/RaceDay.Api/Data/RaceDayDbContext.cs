using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// Code-first model that mirrors docs/part-1-raceday-schema.sql: same table names, column sizes,
/// keys, UNIQUE/CHECK constraints and defaults, so the migration produces the Part 1 schema.
/// </summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<WeatherSnapshot> WeatherSnapshots => Set<WeatherSnapshot>();
    public DbSet<RoutePoint> RoutePoints => Set<RoutePoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users", t => t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Organiser','Participant')"));
            e.HasKey(u => u.UserId).HasName("PK_Users");
            e.Property(u => u.FirstName).HasMaxLength(80).IsRequired();
            e.Property(u => u.LastName).HasMaxLength(80).IsRequired();
            e.Property(u => u.Email).HasMaxLength(255).IsRequired();
            e.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UQ_Users_Email");
            e.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
            e.Property(u => u.Role).HasMaxLength(20).IsRequired();
            e.Property(u => u.Phone).HasMaxLength(30);
            e.Property(u => u.ProfileImageUrl).HasMaxLength(500);
            e.Property(u => u.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.ToTable("Events", t =>
            {
                t.HasCheckConstraint("CK_Events_Distance", "[DistanceKm] > 0");
                t.HasCheckConstraint("CK_Events_Type", "[EventType] IN ('Run','Walk','Cycle')");
            });
            e.HasKey(x => x.EventId).HasName("PK_Events");
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            e.Property(x => x.EventDate).HasColumnType("datetime2");
            e.Property(x => x.Location).HasMaxLength(160).IsRequired();
            e.Property(x => x.DistanceKm).HasPrecision(6, 2);
            e.Property(x => x.EventType).HasMaxLength(20).IsRequired();
            e.Property(x => x.BannerImageUrl).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne(x => x.Organiser).WithMany(u => u.OrganisedEvents)
                .HasForeignKey(x => x.OrganiserId)
                .HasConstraintName("FK_Events_Organiser")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Categories", t =>
            {
                t.HasCheckConstraint("CK_Categories_Type", "[CategoryType] IN ('Age','Distance')");
                t.HasCheckConstraint("CK_Categories_AgeRange",
                    "[MinimumAge] IS NULL OR [MaximumAge] IS NULL OR [MinimumAge] <= [MaximumAge]");
            });
            e.HasKey(x => x.CategoryId).HasName("PK_Categories");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.CategoryType).HasMaxLength(20).IsRequired();
            e.Property(x => x.DistanceKm).HasPrecision(6, 2);
            e.Property(x => x.EntryFee).HasPrecision(10, 2).HasDefaultValue(0m);
            e.HasIndex(x => new { x.EventId, x.Name }).IsUnique().HasDatabaseName("UQ_Categories_Event_Name");
            e.HasOne(x => x.Event).WithMany(ev => ev.Categories)
                .HasForeignKey(x => x.EventId)
                .HasConstraintName("FK_Categories_Event")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolments", t =>
                t.HasCheckConstraint("CK_Enrolments_Status", "[Status] IN ('Pending','Confirmed','Cancelled')"));
            e.HasKey(x => x.EnrolmentId).HasName("PK_Enrolments");
            e.Property(x => x.Status).HasMaxLength(20).IsRequired().HasDefaultValue(EnrolmentStatuses.Pending);
            e.Property(x => x.EnrolledAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(x => new { x.ParticipantId, x.EventId }).IsUnique().HasDatabaseName("UQ_Enrolments_Participant_Event");
            e.HasOne(x => x.Participant).WithMany(u => u.Enrolments)
                .HasForeignKey(x => x.ParticipantId)
                .HasConstraintName("FK_Enrolments_Participant")
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Event).WithMany(ev => ev.Enrolments)
                .HasForeignKey(x => x.EventId)
                .HasConstraintName("FK_Enrolments_Event")
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryId)
                .HasConstraintName("FK_Enrolments_Category")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Result>(e =>
        {
            e.ToTable("Results", t => t.HasCheckConstraint("CK_Results_Position", "[Position] > 0"));
            e.HasKey(x => x.ResultId).HasName("PK_Results");
            e.Property(x => x.FinishTime).HasColumnType("time(0)");
            e.HasIndex(x => x.EnrolmentId).IsUnique().HasDatabaseName("UQ_Results_Enrolment");
            e.HasOne(x => x.Enrolment).WithOne(en => en.Result)
                .HasForeignKey<Result>(x => x.EnrolmentId)
                .HasConstraintName("FK_Results_Enrolment")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WeatherSnapshot>(e =>
        {
            e.ToTable("WeatherSnapshots");
            e.HasKey(x => x.WeatherId).HasName("PK_WeatherSnapshots");
            e.Property(x => x.ObservedAt).HasColumnType("datetime2");
            e.Property(x => x.TemperatureC).HasPrecision(5, 2);
            e.Property(x => x.Conditions).HasMaxLength(120).IsRequired();
            e.Property(x => x.WindKph).HasPrecision(5, 2);
            e.HasOne(x => x.Event).WithMany(ev => ev.WeatherSnapshots)
                .HasForeignKey(x => x.EventId)
                .HasConstraintName("FK_WeatherSnapshots_Event")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoutePoint>(e =>
        {
            e.ToTable("RoutePoints");
            e.HasKey(x => x.RoutePointId).HasName("PK_RoutePoints");
            e.Property(x => x.Latitude).HasPrecision(9, 6);
            e.Property(x => x.Longitude).HasPrecision(9, 6);
            e.Property(x => x.ElevationMetres).HasPrecision(7, 2);
            e.HasIndex(x => new { x.EventId, x.SequenceNo }).IsUnique().HasDatabaseName("UQ_RoutePoints_Event_Sequence");
            e.HasOne(x => x.Event).WithMany(ev => ev.RoutePoints)
                .HasForeignKey(x => x.EventId)
                .HasConstraintName("FK_RoutePoints_Event")
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
