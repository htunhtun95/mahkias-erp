using Mahkias.Core.Modules.Projects.Data;
using Microsoft.EntityFrameworkCore;

namespace Mahkias.Data
{
    public class MahkiasDbContext : DbContext
    {
        public MahkiasDbContext(DbContextOptions<MahkiasDbContext> options) : base(options)
        {
            if (options == null)
                throw new Exception("Options cannot be null");
        }

        public DbSet<Project> Projects { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<ActivityType> ActivityTypes { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.HasDefaultSchema("dbo");

            base.OnModelCreating(builder);

            builder.Entity<ActivityType>(e =>
            {
                e.ToTable("ActivityTypes", "projects");
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.Name).HasMaxLength(64);
                e.Property(x => x.Slug).HasMaxLength(64);
                e.HasIndex(x => x.Slug).IsUnique();
            });

            builder.Entity<Project>(e =>
            {
                e.ToTable("Projects", "projects");
                e.Property(x => x.Name).HasMaxLength(250).IsRequired();
                e.Property(x => x.Reference).HasMaxLength(100);
                e.Property(x => x.Description).HasColumnType("nvarchar(max)");
                e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()").ValueGeneratedOnAdd();
                e.Property(x => x.ModifiedAt).HasColumnType("datetime");
                e.Property(x => x.IsDeleted).HasDefaultValue(false);
                e.Property(x => x.DeletedAt).HasColumnType("datetime2");
                e.Property(x => x.DeletedBy).HasMaxLength(100);
                e.HasQueryFilter(x => !x.IsDeleted);
            });

            builder.Entity<Activity>(e =>
            {
                e.ToTable("Activities", "projects");
                e.Property(x => x.PartNo).HasColumnType("nvarchar(max)");
                e.Property(x => x.Description).HasColumnType("nvarchar(max)");
                e.Property(x => x.DSNNo).HasMaxLength(100);
                e.Property(x => x.Budget).HasColumnType("decimal(18,2)");
                e.Property(x => x.Quantity).HasColumnType("decimal(18,2)");
                e.Property(x => x.ActivityTypeId).IsRequired(false);
                e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()").ValueGeneratedOnAdd();
                e.Property(x => x.ModifiedAt).HasColumnType("datetime");
                e.Property(x => x.IsDeleted).HasDefaultValue(false);
                e.Property(x => x.DeletedAt).HasColumnType("datetime2");
                e.Property(x => x.DeletedBy).HasMaxLength(100);
                e.HasQueryFilter(x => !x.IsDeleted);
                e.HasOne(x => x.Project)
                    .WithMany(x => x.Activities)
                    .HasForeignKey(x => x.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Type)
                    .WithMany()
                    .HasForeignKey(x => x.ActivityTypeId);
            });
        }
    }
}
