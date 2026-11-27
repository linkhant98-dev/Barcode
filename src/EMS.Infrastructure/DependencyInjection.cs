using EMS.Application.Abstractions;
using EMS.Application.CorporateActions;
using EMS.Application.Reporting;
using EMS.Application.Shareholders;
using EMS.Application.Shares;
using EMS.Application.Workflow;
using EMS.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEmsInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<IReferenceNumberService, ReferenceNumberService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IDelegationService, DelegationService>();

        services.AddScoped<IShareholderApplicationService, ShareholderApplicationService>();
        services.AddScoped<IKycService, KycService>();
        services.AddScoped<IShareholderRegistrationService, ShareholderRegistrationService>();

        services.AddScoped<IShareLedgerService, ShareLedgerService>();
        services.AddScoped<IIssueShareService, IssueShareService>();
        services.AddScoped<ITransferShareService, TransferShareService>();

        services.AddScoped<IBonusService, BonusService>();
        services.AddScoped<IDividendService, DividendService>();

        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
