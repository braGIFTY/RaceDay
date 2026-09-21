using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data
{
    public class RaceDayDbContext : DbContext
    {
        public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options)
            : base(options)
        {
        }

        public DbSet<Organiser> Organisers { get; set; }
        public DbSet<Participant> Participants { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Enrolment> Enrolments { get; set; }
        public DbSet<Result> Results { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---------- Organisers ----------
            modelBuilder.Entity<Organiser>(entity =>
            {
                entity.Property(o => o.FullName).HasMaxLength(100).IsRequired();
                entity.Property(o => o.Email).HasMaxLength(150).IsRequired();
                entity.HasIndex(o => o.Email).IsUnique();
                entity.Property(o => o.PasswordHash).HasMaxLength(255).IsRequired();
                entity.Property(o => o.Phone).HasMaxLength(20);
                entity.Property(o => o.CreatedAt).HasDefaultValueSql("SYSDATETIME()");
            });

            // ---------- Participants ----------
            modelBuilder.Entity<Participant>(entity =>
            {
                entity.Property(p => p.FullName).HasMaxLength(100).IsRequired();
                entity.Property(p => p.Email).HasMaxLength(150).IsRequired();
                entity.HasIndex(p => p.Email).IsUnique();
                entity.Property(p => p.PasswordHash).HasMaxLength(255).IsRequired();
                entity.Property(p => p.Phone).HasMaxLength(20);
                entity.Property(p => p.CreatedAt).HasDefaultValueSql("SYSDATETIME()");
            });

            // ---------- Events ----------
            modelBuilder.Entity<Event>(entity =>
            {
                entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
                entity.Property(e => e.EventDate).HasColumnType("date");
                entity.Property(e => e.Location).HasMaxLength(150).IsRequired();
                entity.Property(e => e.DistanceKm).HasPrecision(6, 2);
                entity.Property(e => e.EventType).HasMaxLength(10).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

                entity.HasOne(e => e.Organiser)
                      .WithMany(o => o.Events)
                      .HasForeignKey(e => e.OrganiserId);

                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_Events_EventType",
                    "[EventType] IN ('Run', 'Walk', 'Cycle')"));
            });

            // ---------- Categories ----------
            modelBuilder.Entity<Category>(entity =>
            {
                entity.Property(c => c.Name).HasMaxLength(50).IsRequired();
                entity.Property(c => c.Description).HasMaxLength(255);

                entity.HasOne(c => c.Event)
                      .WithMany(e => e.Categories)
                      .HasForeignKey(c => c.EventId);

                entity.HasIndex(c => new { c.EventId, c.Name }).IsUnique();
            });

            // ---------- Enrolments ----------
            modelBuilder.Entity<Enrolment>(entity =>
            {
                entity.Property(en => en.EnrolmentDate).HasDefaultValueSql("SYSDATETIME()");
                entity.Property(en => en.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Confirmed");

                entity.HasOne(en => en.Participant)
                      .WithMany(p => p.Enrolments)
                      .HasForeignKey(en => en.ParticipantId);

                entity.HasOne(en => en.Event)
                      .WithMany(e => e.Enrolments)
                      .HasForeignKey(en => en.EventId);

                entity.HasOne(en => en.Category)
                      .WithMany(c => c.Enrolments)
                      .HasForeignKey(en => en.CategoryId);

                entity.HasIndex(en => new { en.ParticipantId, en.EventId }).IsUnique();

                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_Enrolments_Status",
                    "[Status] IN ('Confirmed', 'Cancelled')"));

                // One-to-zero-or-one with Results. EF Core can't infer this from
                // convention alone, since Result has its own Id rather than
                // sharing Enrolment's primary key, so it has to be spelled out.
                entity.HasOne(en => en.Result)
                      .WithOne(r => r.Enrolment)
                      .HasForeignKey<Result>(r => r.EnrolmentId);
            });

            // ---------- Results ----------
            modelBuilder.Entity<Result>(entity =>
            {
                entity.Property(r => r.CreatedAt).HasDefaultValueSql("SYSDATETIME()");
                entity.HasIndex(r => r.EnrolmentId).IsUnique();
            });
        }
    }
}
