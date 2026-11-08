using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Data;

public class AdmissionDbContext(DbContextOptions<AdmissionDbContext> options) : DbContext(options)
{
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<Hold> Holds => Set<Hold>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<SchoolClass>(e =>
        {
            e.ToTable("classes", t => t.HasCheckConstraint("ck_classes_total_seats", "\"TotalSeats\" > 0"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(40);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });

        b.Entity<Hold>(e =>
        {
            e.ToTable("holds");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(40);
            e.Property(x => x.ClassId).HasMaxLength(40).IsRequired();
            e.Property(x => x.StudentId).HasMaxLength(17).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);

            // Hard guarantee: a student can have only one active (HELD/CONFIRMED) hold per class.
            // Stale HELD rows (past expiresAt) are flipped to EXPIRED under the class lock before inserting.
            e.HasIndex(x => new { x.ClassId, x.StudentId })
                .IsUnique()
                .HasDatabaseName("ux_holds_active_per_student_class")
                .HasFilter("\"Status\" IN ('HELD', 'CONFIRMED')");

            // Seat counting and the expiry sweep.
            e.HasIndex(x => new { x.ClassId, x.Status, x.ExpiresAt }).HasDatabaseName("ix_holds_class_status_expires");
            e.HasIndex(x => x.StudentId).HasDatabaseName("ix_holds_student");
        });
    }
}
