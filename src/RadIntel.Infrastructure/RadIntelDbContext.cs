using Framework.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadIntel.Application;
using RadIntel.Domain;

namespace RadIntel.Infrastructure;

public sealed class RadIntelDbContext(DbContextOptions<RadIntelDbContext> options, ICurrentActor actor)
    : FrameworkDbContext(options)
{
    public Guid TenantId => actor.OrganizationId;
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<ImagingOrder> Orders => Set<ImagingOrder>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ReportVersion> ReportVersions => Set<ReportVersion>();
    public DbSet<Signature> Signatures => Set<Signature>();
    public DbSet<ReportTemplate> Templates => Set<ReportTemplate>();
    public DbSet<DictationTranscript> Transcripts => Set<DictationTranscript>();
    public DbSet<AIProposal> Proposals => Set<AIProposal>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<ImagingStudy> ImagingStudies => Set<ImagingStudy>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.Entity<Organization>().HasKey(x => x.Id);
        model.Entity<Organization>().Property(x => x.Name).HasMaxLength(200);
        model.Entity<Organization>().HasQueryFilter(x => TenantId != Guid.Empty && x.Id == TenantId);
        model.Entity<Facility>().HasKey(x => x.Id);
        model.Entity<Facility>().HasAlternateKey(x => new { x.OrganizationId, x.Id });
        model.Entity<Facility>().Property(x => x.Name).HasMaxLength(200);
        model.Entity<Facility>().Property(x => x.TimeZone).HasMaxLength(100);
        model.Entity<Facility>().HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Facility>().HasQueryFilter(x => TenantId != Guid.Empty && x.OrganizationId == TenantId);
        Configure(model.Entity<Membership>()); Configure(model.Entity<Patient>());
        Configure(model.Entity<ImagingOrder>()); Configure(model.Entity<Equipment>());
        Configure(model.Entity<Appointment>()); Configure(model.Entity<Report>());
        Configure(model.Entity<ReportVersion>()); Configure(model.Entity<Signature>());
        Configure(model.Entity<ReportTemplate>()); Configure(model.Entity<DictationTranscript>());
        Configure(model.Entity<AIProposal>()); Configure(model.Entity<AuditEvent>()); Configure(model.Entity<OutboxMessage>());
        Configure(model.Entity<ImagingStudy>());
        model.Entity<ImagingStudy>().Property(x => x.ArchiveKey).HasMaxLength(100);
        model.Entity<ImagingStudy>().Property(x => x.StudyInstanceUid).HasMaxLength(64);
        model.Entity<ImagingStudy>().Property(x => x.VerifiedBy).HasMaxLength(200);
        model.Entity<ImagingStudy>().HasIndex(x => new { x.OrganizationId, x.ArchiveKey, x.StudyInstanceUid }).IsUnique();
        model.Entity<ImagingStudy>().HasOne<ImagingOrder>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.FacilityId, x.OrderId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.FacilityId, x.Id }).OnDelete(DeleteBehavior.Restrict);

        model.Entity<Membership>().Property(x => x.UserId).HasMaxLength(200);
        model.Entity<Membership>().HasIndex(x => new { x.OrganizationId, x.FacilityId, x.UserId }).IsUnique();
        model.Entity<Patient>().Property(x => x.Identifier).HasMaxLength(100);
        model.Entity<Patient>().Property(x => x.IdentifierIssuer).HasMaxLength(100);
        model.Entity<Patient>().Property(x => x.DisplayName).HasMaxLength(200);
        model.Entity<Patient>().HasIndex(x => new { x.OrganizationId, x.FacilityId, x.IdentifierIssuer, x.Identifier }).IsUnique();
        model.Entity<ImagingOrder>().HasOne<Patient>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.FacilityId, x.PatientId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.FacilityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ImagingOrder>().Property(x => x.Modality).HasMaxLength(20);
        model.Entity<ImagingOrder>().Property(x => x.BodyRegion).HasMaxLength(100);
        model.Entity<ImagingOrder>().Property(x => x.Referral).HasMaxLength(4000);
        model.Entity<ImagingOrder>().Property(x => x.Priority).HasMaxLength(20);
        model.Entity<ImagingOrder>().Property(x => x.AssignedTo).HasMaxLength(200);
        model.Entity<ImagingOrder>().HasIndex(x => new { x.OrganizationId, x.FacilityId, x.Status, x.CreatedAt });
        model.Entity<Appointment>().HasOne<ImagingOrder>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.FacilityId, x.OrderId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.FacilityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Appointment>().HasOne<Equipment>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.FacilityId, x.EquipmentId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.FacilityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Appointment>().HasIndex(x => new { x.OrganizationId, x.FacilityId, x.OrderId }).IsUnique().HasFilter("\"Cancelled\" = false");
        model.Entity<Appointment>().HasIndex(x => new { x.OrganizationId, x.FacilityId, x.EquipmentId, x.StartsAt });
        model.Entity<Report>().HasOne<ImagingOrder>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.FacilityId, x.OrderId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.FacilityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Report>().HasOne<Report>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.FacilityId, x.ParentReportId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.FacilityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Report>().HasIndex(x => new { x.OrganizationId, x.FacilityId, x.OrderId }).IsUnique().HasFilter("\"Kind\" = 0");
        model.Entity<ReportVersion>().HasOne<Report>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.FacilityId, x.ReportId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.FacilityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ReportVersion>().HasIndex(x => new { x.OrganizationId, x.FacilityId, x.ReportId, x.Number }).IsUnique();
        model.Entity<Signature>().HasOne<ReportVersion>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.FacilityId, x.ReportVersionId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.FacilityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Signature>().HasIndex(x => new { x.OrganizationId, x.FacilityId, x.ReportVersionId }).IsUnique();
        model.Entity<Signature>().HasIndex(x => new { x.OrganizationId, x.FacilityId, x.SignedBy, x.RequestKey }).IsUnique();
        model.Entity<Signature>().Property(x => x.RequestKey).HasMaxLength(100);
        model.Entity<Signature>().Property(x => x.SignedBy).HasMaxLength(200);
        model.Entity<Signature>().Property(x => x.ContentHash).HasMaxLength(64);
        model.Entity<ReportTemplate>().HasIndex(x => new { x.OrganizationId, x.FacilityId, x.Name, x.Version }).IsUnique();
        model.Entity<ReportTemplate>().Property(x => x.Name).HasMaxLength(200);
        model.Entity<DictationTranscript>().HasOne<Report>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.FacilityId, x.ReportId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.FacilityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AIProposal>().HasOne<Report>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.FacilityId, x.ReportId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.FacilityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AIProposal>().HasOne<DictationTranscript>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.FacilityId, x.TranscriptId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.FacilityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AIProposal>().HasOne<ReportTemplate>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.FacilityId, x.TemplateId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.FacilityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<OutboxMessage>().HasOne<ReportVersion>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.FacilityId, x.ReportVersionId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.FacilityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }

    private void Configure<T>(EntityTypeBuilder<T> entity) where T : TenantEntity
    {
        entity.HasBaseType((Type?)null);
        entity.HasKey(x => x.Id);
        entity.HasAlternateKey(x => new { x.OrganizationId, x.FacilityId, x.Id });
        entity.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        entity.HasQueryFilter(x => TenantId != Guid.Empty && x.OrganizationId == TenantId);
        entity.HasOne<Facility>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.FacilityId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }

    private void ProtectWrites()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity is TenantEntity owned)
            {
                if (TenantId == Guid.Empty || owned.OrganizationId != TenantId) throw new AccessDeniedException();
                if (entry.State != EntityState.Added && (entry.OriginalValues.GetValue<Guid>("OrganizationId") != owned.OrganizationId ||
                    entry.OriginalValues.GetValue<Guid>("FacilityId") != owned.FacilityId)) throw new AccessDeniedException();
                if (entry.State is EntityState.Modified or EntityState.Deleted &&
                    entry.Entity is ReportVersion or Signature or DictationTranscript or AuditEvent or ImagingStudy)
                    throw new RuleViolation("conflict", "Historical clinical and audit records are immutable.");
                if (entry.Entity is Report && entry.State != EntityState.Added && entry.OriginalValues.GetValue<ReportStatus>("Status") == ReportStatus.Signed)
                    throw new RuleViolation("conflict", "Signed reports are immutable.");
                if (entry.Entity is ReportTemplate && entry.State == EntityState.Modified && entry.Properties.Any(p =>
                    p.IsModified && p.Metadata.Name is not ("Published" or "ConcurrencyToken")))
                    throw new RuleViolation("conflict", "Template versions are immutable. Create a new version.");
                if (entry.State == EntityState.Modified) owned.ConcurrencyToken = Guid.NewGuid();
            }
            if (entry.Entity is Organization org && (TenantId == Guid.Empty || org.Id != TenantId)) throw new AccessDeniedException();
            if (entry.Entity is Facility facility && (TenantId == Guid.Empty || facility.OrganizationId != TenantId)) throw new AccessDeniedException();
        }
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectWrites();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ProtectWrites();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
