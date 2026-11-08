using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PhysioClinic.Models;

namespace PhysioClinic.Data
{
    public class PhysioDbContext : IdentityDbContext<ApplicationUser>
    {
        public PhysioDbContext(DbContextOptions<PhysioDbContext> options) : base(options)
        {
        }

        public DbSet<Client> Clients { get; set; } = null!;
        public DbSet<Therapist> Therapists { get; set; } = null!;
        public DbSet<IntakeForm> IntakeForms { get; set; } = null!;
        public DbSet<Consultation> Consultations { get; set; } = null!;
        public DbSet<TreatmentPlan> TreatmentPlans { get; set; } = null!;
        public DbSet<Appointment> Appointments { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User - Therapist link (optional)
            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Therapist)
                .WithMany()
                .HasForeignKey(u => u.TherapistId)
                .OnDelete(DeleteBehavior.SetNull);

            // Client Relationships
            modelBuilder.Entity<Client>()
                .HasMany(c => c.IntakeForms)
                .WithOne(i => i.Client)
                .HasForeignKey(i => i.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Client>()
                .HasMany(c => c.Consultations)
                .WithOne(c => c.Client)
                .HasForeignKey(c => c.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Client>()
                .HasMany(c => c.TreatmentPlans)
                .WithOne(t => t.Client)
                .HasForeignKey(t => t.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Client>()
                .HasMany(c => c.Appointments)
                .WithOne(a => a.Client)
                .HasForeignKey(a => a.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            // Therapist Relationships
            modelBuilder.Entity<Therapist>()
                .HasMany(t => t.Appointments)
                .WithOne(a => a.Therapist)
                .HasForeignKey(a => a.TherapistId)
                .OnDelete(DeleteBehavior.Restrict);

            // TreatmentPlan to Appointment
            modelBuilder.Entity<TreatmentPlan>()
                .HasMany(tp => tp.Appointments)
                .WithOne(a => a.TreatmentPlan)
                .HasForeignKey(a => a.TreatmentPlanId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
