using EMS.Domain.Applications;
using EMS.Domain.Audit;
using EMS.Domain.CorporateActions;
using EMS.Domain.Documents;
using EMS.Domain.MasterData;
using EMS.Domain.Notifications;
using EMS.Domain.Reporting;
using EMS.Domain.Shareholders;
using EMS.Domain.Shares;
using EMS.Domain.Workflow;
using EMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Persistence;

/// <summary>
/// Single EF Core context for the EMS domain model plus ASP.NET Core Identity (16 Database Design).
/// Targets Microsoft SQL Server in production; a lighter provider may be selected for local development
/// (see Program.cs) without changing this model.
/// </summary>
public class EmsDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public EmsDbContext(DbContextOptions<EmsDbContext> options) : base(options) { }

    public DbSet<EMS.Domain.Common.NumberSequence> NumberSequences => Set<EMS.Domain.Common.NumberSequence>();

    // Shareholders
    public DbSet<Shareholder> Shareholders => Set<Shareholder>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<Corporate> Corporates => Set<Corporate>();
    public DbSet<CorporateSignatory> CorporateSignatories => Set<CorporateSignatory>();
    public DbSet<BeneficialOwner> BeneficialOwners => Set<BeneficialOwner>();
    public DbSet<JointHolder> JointHolders => Set<JointHolder>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<RelationshipDeclaration> RelationshipDeclarations => Set<RelationshipDeclaration>();

    // Applications / KYC
    public DbSet<ShareholderApplication> ShareholderApplications => Set<ShareholderApplication>();
    public DbSet<ApplicationJointHolder> ApplicationJointHolders => Set<ApplicationJointHolder>();
    public DbSet<KycCase> KycCases => Set<KycCase>();

    // Master data
    public DbSet<NrcPrefix> NrcPrefixes => Set<NrcPrefix>();
    public DbSet<Geography> Geographies => Set<Geography>();
    public DbSet<BankBranch> BankBranches => Set<BankBranch>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<ShareholderGroup> ShareholderGroups => Set<ShareholderGroup>();
    public DbSet<ShareClass> ShareClasses => Set<ShareClass>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<ReasonCode> ReasonCodes => Set<ReasonCode>();
    public DbSet<DividendParameter> DividendParameters => Set<DividendParameter>();
    public DbSet<BonusParameter> BonusParameters => Set<BonusParameter>();

    // Shares
    public DbSet<ShareTransaction> ShareTransactions => Set<ShareTransaction>();
    public DbSet<ShareIssue> ShareIssues => Set<ShareIssue>();
    public DbSet<ShareTransfer> ShareTransfers => Set<ShareTransfer>();
    public DbSet<ShareLedgerEntry> ShareLedgerEntries => Set<ShareLedgerEntry>();
    public DbSet<ShareCertificate> ShareCertificates => Set<ShareCertificate>();

    // Corporate actions
    public DbSet<BonusEvent> BonusEvents => Set<BonusEvent>();
    public DbSet<BonusEntitlement> BonusEntitlements => Set<BonusEntitlement>();
    public DbSet<DividendEvent> DividendEvents => Set<DividendEvent>();
    public DbSet<DividendEntitlement> DividendEntitlements => Set<DividendEntitlement>();
    public DbSet<DividendSettlement> DividendSettlements => Set<DividendSettlement>();

    // Workflow
    public DbSet<ApprovalMatrixRule> ApprovalMatrixRules => Set<ApprovalMatrixRule>();
    public DbSet<ApprovalInstance> ApprovalInstances => Set<ApprovalInstance>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
    public DbSet<Delegation> Delegations => Set<Delegation>();

    // Documents / audit / notifications
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // Reporting
    public DbSet<ReportDefinition> ReportDefinitions => Set<ReportDefinition>();
    public DbSet<ReportExecution> ReportExecutions => Set<ReportExecution>();
    public DbSet<DashboardSnapshot> DashboardSnapshots => Set<DashboardSnapshot>();
    public DbSet<ReconciliationResult> ReconciliationResults => Set<ReconciliationResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EmsDbContext).Assembly);

        // COM-010: money uses DECIMAL(19,4) by default. Individual configurations override share-quantity
        // columns to DECIMAL(19,6) where fractional calculations are required (bonus/dividend proration).
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                {
                    if (property.GetPrecision() is null)
                    {
                        property.SetPrecision(19);
                        property.SetScale(4);
                    }
                }
            }
        }

        // Uniform concurrency-token handling for every "RowVersion" byte[] column (AuditableEntity). SQL Server
        // gets a true server-generated rowversion; the SQLite dev fallback (EnsureCreated, no migrations/triggers)
        // gets a randomblob default so inserts succeed, since SQLite has no native rowversion type.
        var isSqlite = Database.IsSqlite();
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.FindProperty("RowVersion") is { ClrType.Name: "Byte[]" })
            {
                var propertyBuilder = modelBuilder.Entity(entityType.ClrType).Property<byte[]>("RowVersion");
                if (isSqlite)
                    propertyBuilder.HasDefaultValueSql("randomblob(8)").ValueGeneratedOnAdd().IsConcurrencyToken();
                else
                    propertyBuilder.IsRowVersion();
            }
        }
    }
}
