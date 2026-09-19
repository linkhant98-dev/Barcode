IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [ApprovalInstances] (
        [Id] bigint NOT NULL IDENTITY,
        [EntityType] nvarchar(450) NOT NULL,
        [EntityId] bigint NOT NULL,
        [EntityReference] nvarchar(max) NOT NULL,
        [RouteVersion] int NOT NULL,
        [Status] int NOT NULL,
        [SubmittedByUserId] nvarchar(max) NOT NULL,
        [SubmittedAtUtc] datetime2 NOT NULL,
        [CurrentSequence] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ApprovalInstances] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [ApprovalMatrixRules] (
        [Id] bigint NOT NULL IDENTITY,
        [Module] nvarchar(450) NOT NULL,
        [ShareholderGroupId] bigint NULL,
        [ShareClassId] bigint NULL,
        [MinAmount] decimal(19,4) NULL,
        [MaxAmount] decimal(19,4) NULL,
        [Sequence] int NOT NULL,
        [StepName] nvarchar(max) NOT NULL,
        [ApproverRole] nvarchar(max) NOT NULL,
        [IsMandatory] bit NOT NULL,
        [StageMode] int NOT NULL,
        [IsConditional] bit NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ApprovalMatrixRules] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [DescriptionEn] nvarchar(max) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [FullName] nvarchar(max) NOT NULL,
        [DepartmentId] bigint NULL,
        [Status] int NOT NULL,
        [EffectiveFrom] date NULL,
        [EffectiveTo] date NULL,
        [LastLoginUtc] datetime2 NULL,
        [MfaRequired] bit NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [Attachments] (
        [Id] bigint NOT NULL IDENTITY,
        [OwnerType] int NOT NULL,
        [OwnerId] bigint NOT NULL,
        [DocumentTypeId] bigint NULL,
        [FileName] nvarchar(max) NOT NULL,
        [ContentType] nvarchar(max) NOT NULL,
        [FileSizeBytes] bigint NOT NULL,
        [StoragePath] nvarchar(max) NOT NULL,
        [Sha256Checksum] nvarchar(max) NOT NULL,
        [Version] int NOT NULL,
        [UploadedByUserId] nvarchar(max) NOT NULL,
        [UploadedAtUtc] datetime2 NOT NULL,
        [IsSoftDeleted] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Attachments] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogEntries] (
        [AuditId] bigint NOT NULL IDENTITY,
        [TimestampUtc] datetime2 NOT NULL,
        [UserId] nvarchar(max) NOT NULL,
        [UserName] nvarchar(max) NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [Module] nvarchar(max) NOT NULL,
        [EntityType] nvarchar(max) NOT NULL,
        [EntityReference] nvarchar(max) NULL,
        [BeforeValueJson] nvarchar(max) NULL,
        [AfterValueJson] nvarchar(max) NULL,
        [IpAddress] nvarchar(max) NULL,
        [CorrelationId] nvarchar(max) NULL,
        [Result] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_AuditLogEntries] PRIMARY KEY ([AuditId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [BankBranches] (
        [Id] bigint NOT NULL IDENTITY,
        [Address] nvarchar(max) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [NameMm] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        CONSTRAINT [PK_BankBranches] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [BonusEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [BonusEventNo] nvarchar(450) NOT NULL,
        [FinancialYear] nvarchar(max) NOT NULL,
        [RecordDate] date NOT NULL,
        [BonusNumerator] int NOT NULL,
        [BonusDenominator] int NOT NULL,
        [CashBonusRatePerRemainderShare] decimal(19,4) NOT NULL,
        [ShareClassId] bigint NOT NULL,
        [BatchVersion] nvarchar(max) NOT NULL,
        [RoundingMethod] nvarchar(max) NOT NULL,
        [Status] int NOT NULL,
        [MakerUserId] nvarchar(max) NOT NULL,
        [CalculationFileAttachmentId] bigint NULL,
        [ApprovalInstanceId] bigint NULL,
        [PostedDate] date NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_BonusEvents] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [BonusParameters] (
        [Id] bigint NOT NULL IDENTITY,
        [FinancialYear] nvarchar(max) NOT NULL,
        [BonusNumerator] int NOT NULL,
        [BonusDenominator] int NOT NULL,
        [CashBonusRatePerRemainderShare] decimal(19,4) NOT NULL,
        [RecordDate] date NOT NULL,
        [IsApproved] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_BonusParameters] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [DashboardSnapshots] (
        [DashboardSnapshotId] bigint NOT NULL IDENTITY,
        [WidgetCode] nvarchar(max) NOT NULL,
        [FilterDimensionsJson] nvarchar(max) NOT NULL,
        [DataJson] nvarchar(max) NOT NULL,
        [DataCutoffUtc] datetime2 NOT NULL,
        [SourceVersion] nvarchar(max) NOT NULL,
        [GeneratedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DashboardSnapshots] PRIMARY KEY ([DashboardSnapshotId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [Delegations] (
        [Id] bigint NOT NULL IDENTITY,
        [FromUserId] nvarchar(max) NOT NULL,
        [ToUserId] nvarchar(max) NOT NULL,
        [ApproverRole] nvarchar(max) NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [Reason] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Delegations] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [Departments] (
        [Id] bigint NOT NULL IDENTITY,
        [ParentDepartmentId] bigint NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [NameMm] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Departments_Departments_ParentDepartmentId] FOREIGN KEY ([ParentDepartmentId]) REFERENCES [Departments] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [DividendEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [DividendEventNo] nvarchar(450) NOT NULL,
        [FinancialYear] nvarchar(max) NOT NULL,
        [RecordDate] date NOT NULL,
        [DividendPercentage] decimal(19,4) NOT NULL,
        [CapitalValuePerShare] decimal(19,4) NOT NULL,
        [BatchVersion] nvarchar(max) NOT NULL,
        [Status] int NOT NULL,
        [MakerUserId] nvarchar(max) NOT NULL,
        [CalculationFileAttachmentId] bigint NULL,
        [ApprovalInstanceId] bigint NULL,
        [PostedDate] date NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_DividendEvents] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [DividendParameters] (
        [Id] bigint NOT NULL IDENTITY,
        [FinancialYear] nvarchar(max) NOT NULL,
        [DividendPercentage] decimal(19,4) NOT NULL,
        [CapitalValuePerShare] decimal(19,4) NOT NULL,
        [RecordDate] date NOT NULL,
        [IsApproved] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_DividendParameters] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [DocumentTypes] (
        [Id] bigint NOT NULL IDENTITY,
        [AllowedExtensions] nvarchar(max) NOT NULL,
        [MaxFileSizeBytes] bigint NOT NULL,
        [IsMandatoryForKyc] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [NameMm] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        CONSTRAINT [PK_DocumentTypes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [Geographies] (
        [Id] bigint NOT NULL IDENTITY,
        [Country] nvarchar(max) NOT NULL,
        [StateRegion] nvarchar(max) NOT NULL,
        [City] nvarchar(max) NOT NULL,
        [Township] nvarchar(max) NOT NULL,
        [PostalCode] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [NameMm] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        CONSTRAINT [PK_Geographies] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [NotificationId] bigint NOT NULL IDENTITY,
        [UserId] nvarchar(max) NOT NULL,
        [Channel] int NOT NULL,
        [TemplateCode] nvarchar(max) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [EntityReference] nvarchar(max) NULL,
        [LinkUrl] nvarchar(max) NULL,
        [Status] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [SentAtUtc] datetime2 NULL,
        [IsRead] bit NOT NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([NotificationId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [NrcPrefixes] (
        [Id] bigint NOT NULL IDENTITY,
        [StateRegion] nvarchar(max) NOT NULL,
        [TownshipCode] nvarchar(max) NOT NULL,
        [CitizenshipType] nvarchar(max) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [NameMm] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        CONSTRAINT [PK_NrcPrefixes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [NumberSequences] (
        [NumberSequenceId] bigint NOT NULL IDENTITY,
        [Module] nvarchar(450) NOT NULL,
        [Year] int NOT NULL,
        [LastValue] bigint NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_NumberSequences] PRIMARY KEY ([NumberSequenceId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [ReasonCodes] (
        [Id] bigint NOT NULL IDENTITY,
        [Category] nvarchar(max) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [NameMm] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        CONSTRAINT [PK_ReasonCodes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [ReconciliationResults] (
        [Id] bigint NOT NULL IDENTITY,
        [ControlName] nvarchar(max) NOT NULL,
        [BusinessDate] date NOT NULL,
        [ExpectedValue] decimal(19,4) NOT NULL,
        [ActualValue] decimal(19,4) NOT NULL,
        [Difference] decimal(19,4) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [Resolution] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_ReconciliationResults] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [ReportDefinitions] (
        [ReportDefinitionId] bigint NOT NULL IDENTITY,
        [ReportCode] nvarchar(max) NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [NameMm] nvarchar(max) NOT NULL,
        [Category] nvarchar(max) NOT NULL,
        [Version] int NOT NULL,
        [RequiredPermission] nvarchar(max) NOT NULL,
        [SupportsExcel] bit NOT NULL,
        [SupportsPdf] bit NOT NULL,
        [SupportsCsv] bit NOT NULL,
        CONSTRAINT [PK_ReportDefinitions] PRIMARY KEY ([ReportDefinitionId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [ReportExecutions] (
        [ReportExecutionId] bigint NOT NULL IDENTITY,
        [ReportCode] nvarchar(max) NOT NULL,
        [RequestedByUserId] nvarchar(max) NOT NULL,
        [ParametersJson] nvarchar(max) NOT NULL,
        [Format] int NOT NULL,
        [Status] int NOT NULL,
        [RequestedAtUtc] datetime2 NOT NULL,
        [StartedAtUtc] datetime2 NULL,
        [CompletedAtUtc] datetime2 NULL,
        [RowCount] int NULL,
        [FileReference] nvarchar(max) NULL,
        [Sha256Checksum] nvarchar(max) NULL,
        [ExpiresAtUtc] datetime2 NULL,
        [CorrelationId] nvarchar(max) NULL,
        [ErrorMessage] nvarchar(max) NULL,
        CONSTRAINT [PK_ReportExecutions] PRIMARY KEY ([ReportExecutionId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [ShareClasses] (
        [Id] bigint NOT NULL IDENTITY,
        [CarriesCertificate] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [NameMm] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        CONSTRAINT [PK_ShareClasses] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [ShareholderGroups] (
        [Id] bigint NOT NULL IDENTITY,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [NameMm] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        CONSTRAINT [PK_ShareholderGroups] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [ShareTransactions] (
        [Id] bigint NOT NULL IDENTITY,
        [TransactionNo] nvarchar(20) NOT NULL,
        [Type] int NOT NULL,
        [Status] int NOT NULL,
        [EffectiveDate] date NOT NULL,
        [MakerUserId] nvarchar(max) NOT NULL,
        [TotalAmount] decimal(19,4) NOT NULL,
        [ApprovalInstanceId] bigint NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ShareTransactions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [ApprovalSteps] (
        [Id] bigint NOT NULL IDENTITY,
        [ApprovalInstanceId] bigint NOT NULL,
        [Sequence] int NOT NULL,
        [StepName] nvarchar(max) NOT NULL,
        [ApproverRole] nvarchar(max) NOT NULL,
        [AssignedUserId] nvarchar(max) NULL,
        [IsMandatory] bit NOT NULL,
        [Status] int NOT NULL,
        [Decision] int NULL,
        [DecisionByUserId] nvarchar(max) NULL,
        [DecisionAtUtc] datetime2 NULL,
        [Comment] nvarchar(max) NULL,
        [DelegatedToUserId] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ApprovalSteps] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ApprovalSteps_ApprovalInstances_ApprovalInstanceId] FOREIGN KEY ([ApprovalInstanceId]) REFERENCES [ApprovalInstances] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [ShareholderApplications] (
        [Id] bigint NOT NULL IDENTITY,
        [ApplicationNo] nvarchar(20) NOT NULL,
        [Type] int NOT NULL,
        [Status] int NOT NULL,
        [SubmittedDate] date NULL,
        [MakerUserId] nvarchar(max) NOT NULL,
        [ShareholderGroupId] bigint NOT NULL,
        [NameEn] nvarchar(max) NULL,
        [NameMm] nvarchar(max) NULL,
        [DateOfBirth] date NULL,
        [FatherName] nvarchar(max) NULL,
        [NrcPrefixCode] nvarchar(max) NULL,
        [NrcNumber] nvarchar(max) NULL,
        [MaritalStatus] nvarchar(max) NULL,
        [SpouseName] nvarchar(max) NULL,
        [LegalNameEn] nvarchar(max) NULL,
        [RegistrationNumber] nvarchar(max) NULL,
        [CorporateRegistrationDate] date NULL,
        [LegalForm] nvarchar(max) NULL,
        [TaxIdentifier] nvarchar(max) NULL,
        [AddressLine1] nvarchar(max) NULL,
        [Township] nvarchar(max) NULL,
        [City] nvarchar(max) NULL,
        [StateRegion] nvarchar(max) NULL,
        [Mobile] nvarchar(max) NULL,
        [Email] nvarchar(max) NULL,
        [CreatedShareholderId] bigint NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ShareholderApplications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ShareholderApplications_ShareholderGroups_ShareholderGroupId] FOREIGN KEY ([ShareholderGroupId]) REFERENCES [ShareholderGroups] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [Shareholders] (
        [Id] bigint NOT NULL IDENTITY,
        [ShareholderNo] nvarchar(20) NOT NULL,
        [Type] int NOT NULL,
        [ShareholderGroupId] bigint NOT NULL,
        [Status] int NOT NULL,
        [RegistrationDate] date NOT NULL,
        [KycStatus] int NOT NULL,
        [KycApprovalDate] date NULL,
        [RiskRating] int NOT NULL,
        [SourceApplicationId] bigint NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Shareholders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Shareholders_ShareholderGroups_ShareholderGroupId] FOREIGN KEY ([ShareholderGroupId]) REFERENCES [ShareholderGroups] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [ApplicationJointHolders] (
        [ApplicationJointHolderId] bigint NOT NULL IDENTITY,
        [ShareholderApplicationId] bigint NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [NrcNumber] nvarchar(max) NOT NULL,
        [OwnershipPercentage] decimal(19,4) NOT NULL,
        [IsPrimaryContact] bit NOT NULL,
        CONSTRAINT [PK_ApplicationJointHolders] PRIMARY KEY ([ApplicationJointHolderId]),
        CONSTRAINT [FK_ApplicationJointHolders_ShareholderApplications_ShareholderApplicationId] FOREIGN KEY ([ShareholderApplicationId]) REFERENCES [ShareholderApplications] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [KycCases] (
        [Id] bigint NOT NULL IDENTITY,
        [ShareholderApplicationId] bigint NOT NULL,
        [RequestedAtUtc] datetime2 NOT NULL,
        [RequestedByUserId] nvarchar(max) NOT NULL,
        [Result] int NOT NULL,
        [RiskRating] int NOT NULL,
        [ScreeningReference] nvarchar(max) NULL,
        [ScreeningTimestampUtc] datetime2 NULL,
        [DecisionUserId] nvarchar(max) NULL,
        [DecisionAtUtc] datetime2 NULL,
        [DecisionRemark] nvarchar(max) NULL,
        [NextReviewDate] date NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_KycCases] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_KycCases_ShareholderApplications_ShareholderApplicationId] FOREIGN KEY ([ShareholderApplicationId]) REFERENCES [ShareholderApplications] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [Addresses] (
        [AddressId] bigint NOT NULL IDENTITY,
        [ShareholderId] bigint NOT NULL,
        [AddressType] int NOT NULL,
        [Line1] nvarchar(max) NOT NULL,
        [Line2] nvarchar(max) NULL,
        [Township] nvarchar(max) NOT NULL,
        [City] nvarchar(max) NOT NULL,
        [StateRegion] nvarchar(max) NOT NULL,
        [PostalCode] nvarchar(max) NULL,
        CONSTRAINT [PK_Addresses] PRIMARY KEY ([AddressId]),
        CONSTRAINT [FK_Addresses_Shareholders_ShareholderId] FOREIGN KEY ([ShareholderId]) REFERENCES [Shareholders] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [BankAccounts] (
        [BankAccountId] bigint NOT NULL IDENTITY,
        [ShareholderId] bigint NOT NULL,
        [IsCbBank] bit NOT NULL,
        [BranchId] bigint NULL,
        [OtherBankName] nvarchar(max) NULL,
        [OtherBranchName] nvarchar(max) NULL,
        [AccountNumberMasked] nvarchar(max) NOT NULL,
        [AccountNumberEncrypted] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_BankAccounts] PRIMARY KEY ([BankAccountId]),
        CONSTRAINT [FK_BankAccounts_BankBranches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [BankBranches] ([Id]),
        CONSTRAINT [FK_BankAccounts_Shareholders_ShareholderId] FOREIGN KEY ([ShareholderId]) REFERENCES [Shareholders] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [BonusEntitlements] (
        [BonusEntitlementId] bigint NOT NULL IDENTITY,
        [BonusEventId] bigint NOT NULL,
        [ShareholderId] bigint NOT NULL,
        [EligibleShares] decimal(19,6) NOT NULL,
        [RawBonusEntitlement] decimal(19,6) NOT NULL,
        [BonusShares] decimal(19,6) NOT NULL,
        [RemainderShares] decimal(19,6) NOT NULL,
        [CashBonusAmount] decimal(19,4) NOT NULL,
        [NewTotalShares] decimal(19,6) NOT NULL,
        [IsPosted] bit NOT NULL,
        [SettlementStatus] nvarchar(max) NULL,
        CONSTRAINT [PK_BonusEntitlements] PRIMARY KEY ([BonusEntitlementId]),
        CONSTRAINT [FK_BonusEntitlements_BonusEvents_BonusEventId] FOREIGN KEY ([BonusEventId]) REFERENCES [BonusEvents] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_BonusEntitlements_Shareholders_ShareholderId] FOREIGN KEY ([ShareholderId]) REFERENCES [Shareholders] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [Contacts] (
        [ContactId] bigint NOT NULL IDENTITY,
        [ShareholderId] bigint NOT NULL,
        [ContactType] int NOT NULL,
        [Value] nvarchar(max) NOT NULL,
        [IsPrimary] bit NOT NULL,
        CONSTRAINT [PK_Contacts] PRIMARY KEY ([ContactId]),
        CONSTRAINT [FK_Contacts_Shareholders_ShareholderId] FOREIGN KEY ([ShareholderId]) REFERENCES [Shareholders] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [Corporates] (
        [CorporateId] bigint NOT NULL IDENTITY,
        [ShareholderId] bigint NOT NULL,
        [LegalNameEn] nvarchar(max) NOT NULL,
        [LegalNameMm] nvarchar(max) NOT NULL,
        [RegistrationNumber] nvarchar(max) NOT NULL,
        [RegistrationDate] date NOT NULL,
        [LegalForm] nvarchar(max) NOT NULL,
        [TaxIdentifier] nvarchar(max) NULL,
        [ContactPersonName] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Corporates] PRIMARY KEY ([CorporateId]),
        CONSTRAINT [FK_Corporates_Shareholders_ShareholderId] FOREIGN KEY ([ShareholderId]) REFERENCES [Shareholders] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [DividendEntitlements] (
        [Id] bigint NOT NULL IDENTITY,
        [DividendEventId] bigint NOT NULL,
        [ShareholderId] bigint NOT NULL,
        [OldShares] decimal(19,6) NOT NULL,
        [NewShares] decimal(19,6) NOT NULL,
        [EligibleDaysForNewShares] int NOT NULL,
        [OldShareDividend] decimal(19,4) NOT NULL,
        [NewShareDividend] decimal(19,4) NOT NULL,
        [TotalDividend] decimal(19,4) NOT NULL,
        [CashWithdrawal] decimal(19,4) NOT NULL,
        [AccountTransfer] decimal(19,4) NOT NULL,
        [ReinvestedAmount] decimal(19,4) NOT NULL,
        [AdjustmentAmount] decimal(19,4) NOT NULL,
        [OutstandingBalance] decimal(19,4) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_DividendEntitlements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DividendEntitlements_DividendEvents_DividendEventId] FOREIGN KEY ([DividendEventId]) REFERENCES [DividendEvents] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DividendEntitlements_Shareholders_ShareholderId] FOREIGN KEY ([ShareholderId]) REFERENCES [Shareholders] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [JointHolders] (
        [JointHolderId] bigint NOT NULL IDENTITY,
        [ShareholderId] bigint NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [NameMm] nvarchar(max) NOT NULL,
        [NrcNumber] nvarchar(max) NOT NULL,
        [OwnershipPercentage] decimal(19,4) NOT NULL,
        [IsPrimaryContact] bit NOT NULL,
        [JointOperatingInstruction] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_JointHolders] PRIMARY KEY ([JointHolderId]),
        CONSTRAINT [FK_JointHolders_Shareholders_ShareholderId] FOREIGN KEY ([ShareholderId]) REFERENCES [Shareholders] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [Persons] (
        [PersonId] bigint NOT NULL IDENTITY,
        [ShareholderId] bigint NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [NameMm] nvarchar(max) NOT NULL,
        [DateOfBirth] date NOT NULL,
        [Gender] int NOT NULL,
        [FatherName] nvarchar(max) NOT NULL,
        [NrcPrefixCode] nvarchar(max) NOT NULL,
        [NrcNumber] nvarchar(max) NOT NULL,
        [MaritalStatus] int NOT NULL,
        [SpouseName] nvarchar(max) NULL,
        [Qualification] nvarchar(max) NULL,
        [Specialization] nvarchar(max) NULL,
        [WorkNature] nvarchar(max) NULL,
        [Position] nvarchar(max) NULL,
        [YearsOfService] int NULL,
        [CompanyAddress] nvarchar(max) NULL,
        CONSTRAINT [PK_Persons] PRIMARY KEY ([PersonId]),
        CONSTRAINT [FK_Persons_Shareholders_ShareholderId] FOREIGN KEY ([ShareholderId]) REFERENCES [Shareholders] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [RelationshipDeclarations] (
        [DeclarationId] bigint NOT NULL IDENTITY,
        [ShareholderId] bigint NOT NULL,
        [DeclarationType] nvarchar(max) NOT NULL,
        [Answer] bit NOT NULL,
        [Details] nvarchar(max) NULL,
        CONSTRAINT [PK_RelationshipDeclarations] PRIMARY KEY ([DeclarationId]),
        CONSTRAINT [FK_RelationshipDeclarations_Shareholders_ShareholderId] FOREIGN KEY ([ShareholderId]) REFERENCES [Shareholders] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [ShareCertificates] (
        [Id] bigint NOT NULL IDENTITY,
        [CertificateNumber] nvarchar(450) NOT NULL,
        [ShareholderId] bigint NOT NULL,
        [ShareClassId] bigint NOT NULL,
        [Quantity] decimal(19,6) NOT NULL,
        [Status] int NOT NULL,
        [IssueTransactionId] bigint NULL,
        [IssueDate] date NOT NULL,
        [StartSerialNumber] nvarchar(max) NULL,
        [EndSerialNumber] nvarchar(max) NULL,
        [PrintedDate] date NULL,
        [PrintedByUserId] nvarchar(max) NULL,
        [DeliveredDate] date NULL,
        [DeliveredByUserId] nvarchar(max) NULL,
        [CancelledDate] date NULL,
        [ReplacementCertificateId] bigint NULL,
        [CancellationReason] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ShareCertificates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ShareCertificates_ShareClasses_ShareClassId] FOREIGN KEY ([ShareClassId]) REFERENCES [ShareClasses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShareCertificates_Shareholders_ShareholderId] FOREIGN KEY ([ShareholderId]) REFERENCES [Shareholders] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [ShareIssues] (
        [TransactionId] bigint NOT NULL,
        [ApplyType] int NOT NULL,
        [ShareholderId] bigint NOT NULL,
        [ShareClassId] bigint NOT NULL,
        [NumberOfShares] decimal(19,6) NOT NULL,
        [CapitalValuePerShare] decimal(19,4) NOT NULL,
        [PremiumValuePerShare] decimal(19,4) NOT NULL,
        [CapitalAmount] decimal(19,4) NOT NULL,
        [PremiumAmount] decimal(19,4) NOT NULL,
        [TotalAmount] decimal(19,4) NOT NULL,
        [CashAmount] decimal(19,4) NOT NULL,
        [ChequeAmount] decimal(19,4) NOT NULL,
        [ChequeNumber] nvarchar(max) NULL,
        [ChequeDate] date NULL,
        [BankBranchId] bigint NULL,
        [AccountTransferReference] nvarchar(max) NULL,
        [DividendEntitlementId] bigint NULL,
        [CertificateRangeFrom] nvarchar(max) NULL,
        [CertificateRangeTo] nvarchar(max) NULL,
        CONSTRAINT [PK_ShareIssues] PRIMARY KEY ([TransactionId]),
        CONSTRAINT [FK_ShareIssues_ShareClasses_ShareClassId] FOREIGN KEY ([ShareClassId]) REFERENCES [ShareClasses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShareIssues_ShareTransactions_TransactionId] FOREIGN KEY ([TransactionId]) REFERENCES [ShareTransactions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ShareIssues_Shareholders_ShareholderId] FOREIGN KEY ([ShareholderId]) REFERENCES [Shareholders] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [ShareLedgerEntries] (
        [LedgerId] bigint NOT NULL IDENTITY,
        [ShareholderId] bigint NOT NULL,
        [ShareClassId] bigint NOT NULL,
        [SourceTransactionId] bigint NULL,
        [SourceBonusEventId] bigint NULL,
        [SourceDividendEventId] bigint NULL,
        [SourceReference] nvarchar(max) NOT NULL,
        [QuantityDelta] decimal(19,6) NOT NULL,
        [CapitalAmountDelta] decimal(19,4) NOT NULL,
        [PremiumAmountDelta] decimal(19,4) NOT NULL,
        [RunningQuantityBalance] decimal(19,6) NOT NULL,
        [RunningPaidUpCapital] decimal(19,4) NOT NULL,
        [EffectiveDate] date NOT NULL,
        [PostedAtUtc] datetime2 NOT NULL,
        [PostedByUserId] nvarchar(max) NOT NULL,
        [IsReversed] bit NOT NULL,
        [ReversalOfLedgerId] bigint NULL,
        CONSTRAINT [PK_ShareLedgerEntries] PRIMARY KEY ([LedgerId]),
        CONSTRAINT [FK_ShareLedgerEntries_ShareClasses_ShareClassId] FOREIGN KEY ([ShareClassId]) REFERENCES [ShareClasses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShareLedgerEntries_Shareholders_ShareholderId] FOREIGN KEY ([ShareholderId]) REFERENCES [Shareholders] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [ShareTransfers] (
        [TransactionId] bigint NOT NULL,
        [FromShareholderId] bigint NOT NULL,
        [ToShareholderId] bigint NOT NULL,
        [ShareClassId] bigint NOT NULL,
        [Quantity] decimal(19,6) NOT NULL,
        [TransferType] int NOT NULL,
        [CapitalAmount] decimal(19,4) NOT NULL,
        [PremiumAmount] decimal(19,4) NOT NULL,
        [TotalConsideration] decimal(19,4) NOT NULL,
        [CashAmount] decimal(19,4) NOT NULL,
        [ChequeAmount] decimal(19,4) NOT NULL,
        [NonTradeReason] int NULL,
        [NonTradeReasonDetail] nvarchar(max) NULL,
        [CancelledCertificateNumbers] nvarchar(max) NULL,
        [NewCertificateNumbers] nvarchar(max) NULL,
        CONSTRAINT [PK_ShareTransfers] PRIMARY KEY ([TransactionId]),
        CONSTRAINT [FK_ShareTransfers_ShareClasses_ShareClassId] FOREIGN KEY ([ShareClassId]) REFERENCES [ShareClasses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShareTransfers_ShareTransactions_TransactionId] FOREIGN KEY ([TransactionId]) REFERENCES [ShareTransactions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ShareTransfers_Shareholders_FromShareholderId] FOREIGN KEY ([FromShareholderId]) REFERENCES [Shareholders] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShareTransfers_Shareholders_ToShareholderId] FOREIGN KEY ([ToShareholderId]) REFERENCES [Shareholders] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [BeneficialOwners] (
        [BeneficialOwnerId] bigint NOT NULL IDENTITY,
        [CorporateId] bigint NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [NrcOrRegistrationNumber] nvarchar(max) NOT NULL,
        [OwnershipPercentage] decimal(19,4) NOT NULL,
        CONSTRAINT [PK_BeneficialOwners] PRIMARY KEY ([BeneficialOwnerId]),
        CONSTRAINT [FK_BeneficialOwners_Corporates_CorporateId] FOREIGN KEY ([CorporateId]) REFERENCES [Corporates] ([CorporateId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [CorporateSignatories] (
        [CorporateSignatoryId] bigint NOT NULL IDENTITY,
        [CorporateId] bigint NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Position] nvarchar(max) NOT NULL,
        [IsAuthorizedSigner] bit NOT NULL,
        [IsDirector] bit NOT NULL,
        CONSTRAINT [PK_CorporateSignatories] PRIMARY KEY ([CorporateSignatoryId]),
        CONSTRAINT [FK_CorporateSignatories_Corporates_CorporateId] FOREIGN KEY ([CorporateId]) REFERENCES [Corporates] ([CorporateId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE TABLE [DividendSettlements] (
        [Id] bigint NOT NULL IDENTITY,
        [DividendEntitlementId] bigint NOT NULL,
        [Method] int NOT NULL,
        [Amount] decimal(19,4) NOT NULL,
        [SettlementDate] date NOT NULL,
        [Reference] nvarchar(max) NULL,
        [Status] nvarchar(max) NOT NULL,
        [LinkedShareTransactionId] bigint NULL,
        [IsRolledBack] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_DividendSettlements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DividendSettlements_DividendEntitlements_DividendEntitlementId] FOREIGN KEY ([DividendEntitlementId]) REFERENCES [DividendEntitlements] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Addresses_ShareholderId] ON [Addresses] ([ShareholderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ApplicationJointHolders_ShareholderApplicationId] ON [ApplicationJointHolders] ([ShareholderApplicationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ApprovalInstances_EntityType_EntityId] ON [ApprovalInstances] ([EntityType], [EntityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ApprovalMatrixRules_Module_Sequence_EffectiveFrom] ON [ApprovalMatrixRules] ([Module], [Sequence], [EffectiveFrom]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ApprovalSteps_ApprovalInstanceId_Sequence] ON [ApprovalSteps] ([ApprovalInstanceId], [Sequence]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogEntries_TimestampUtc] ON [AuditLogEntries] ([TimestampUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BankAccounts_BranchId] ON [BankAccounts] ([BranchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BankAccounts_ShareholderId] ON [BankAccounts] ([ShareholderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_BankBranches_Code] ON [BankBranches] ([Code]) WHERE [IsActive] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BeneficialOwners_CorporateId] ON [BeneficialOwners] ([CorporateId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BonusEntitlements_BonusEventId] ON [BonusEntitlements] ([BonusEventId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BonusEntitlements_ShareholderId] ON [BonusEntitlements] ([ShareholderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BonusEvents_BonusEventNo] ON [BonusEvents] ([BonusEventNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Contacts_ShareholderId] ON [Contacts] ([ShareholderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Corporates_ShareholderId] ON [Corporates] ([ShareholderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CorporateSignatories_CorporateId] ON [CorporateSignatories] ([CorporateId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Departments_Code] ON [Departments] ([Code]) WHERE [IsActive] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Departments_ParentDepartmentId] ON [Departments] ([ParentDepartmentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DividendEntitlements_DividendEventId] ON [DividendEntitlements] ([DividendEventId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DividendEntitlements_ShareholderId] ON [DividendEntitlements] ([ShareholderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DividendEvents_DividendEventNo] ON [DividendEvents] ([DividendEventNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DividendSettlements_DividendEntitlementId] ON [DividendSettlements] ([DividendEntitlementId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DocumentTypes_Code] ON [DocumentTypes] ([Code]) WHERE [IsActive] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Geographies_Code] ON [Geographies] ([Code]) WHERE [IsActive] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_JointHolders_ShareholderId] ON [JointHolders] ([ShareholderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_KycCases_ShareholderApplicationId] ON [KycCases] ([ShareholderApplicationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_NrcPrefixes_Code] ON [NrcPrefixes] ([Code]) WHERE [IsActive] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NumberSequences_Module_Year] ON [NumberSequences] ([Module], [Year]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Persons_ShareholderId] ON [Persons] ([ShareholderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ReasonCodes_Code] ON [ReasonCodes] ([Code]) WHERE [IsActive] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RelationshipDeclarations_ShareholderId] ON [RelationshipDeclarations] ([ShareholderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ShareCertificates_CertificateNumber] ON [ShareCertificates] ([CertificateNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShareCertificates_ShareClassId] ON [ShareCertificates] ([ShareClassId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShareCertificates_ShareholderId] ON [ShareCertificates] ([ShareholderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ShareClasses_Code] ON [ShareClasses] ([Code]) WHERE [IsActive] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ShareholderApplications_ApplicationNo] ON [ShareholderApplications] ([ApplicationNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShareholderApplications_ShareholderGroupId] ON [ShareholderApplications] ([ShareholderGroupId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ShareholderGroups_Code] ON [ShareholderGroups] ([Code]) WHERE [IsActive] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shareholders_ShareholderGroupId] ON [Shareholders] ([ShareholderGroupId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Shareholders_ShareholderNo] ON [Shareholders] ([ShareholderNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShareIssues_ShareClassId] ON [ShareIssues] ([ShareClassId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShareIssues_ShareholderId] ON [ShareIssues] ([ShareholderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShareLedgerEntries_ShareClassId] ON [ShareLedgerEntries] ([ShareClassId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShareLedgerEntries_ShareholderId_ShareClassId_EffectiveDate] ON [ShareLedgerEntries] ([ShareholderId], [ShareClassId], [EffectiveDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ShareTransactions_TransactionNo] ON [ShareTransactions] ([TransactionNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShareTransfers_FromShareholderId] ON [ShareTransfers] ([FromShareholderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShareTransfers_ShareClassId] ON [ShareTransfers] ([ShareClassId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShareTransfers_ToShareholderId] ON [ShareTransfers] ([ToShareholderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718093401_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260718093401_InitialCreate', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718233809_AddApprovalStepReminderTracking'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ReconciliationResults]') AND [c].[name] = N'RowVersion');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [ReconciliationResults] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [ReconciliationResults] ALTER COLUMN [RowVersion] rowversion NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718233809_AddApprovalStepReminderTracking'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[KycCases]') AND [c].[name] = N'RowVersion');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [KycCases] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [KycCases] ALTER COLUMN [RowVersion] rowversion NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718233809_AddApprovalStepReminderTracking'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DividendSettlements]') AND [c].[name] = N'RowVersion');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [DividendSettlements] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [DividendSettlements] ALTER COLUMN [RowVersion] rowversion NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718233809_AddApprovalStepReminderTracking'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DividendParameters]') AND [c].[name] = N'RowVersion');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [DividendParameters] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [DividendParameters] ALTER COLUMN [RowVersion] rowversion NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718233809_AddApprovalStepReminderTracking'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Delegations]') AND [c].[name] = N'RowVersion');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Delegations] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [Delegations] ALTER COLUMN [RowVersion] rowversion NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718233809_AddApprovalStepReminderTracking'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BonusParameters]') AND [c].[name] = N'RowVersion');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [BonusParameters] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [BonusParameters] ALTER COLUMN [RowVersion] rowversion NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718233809_AddApprovalStepReminderTracking'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Attachments]') AND [c].[name] = N'RowVersion');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Attachments] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [Attachments] ALTER COLUMN [RowVersion] rowversion NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718233809_AddApprovalStepReminderTracking'
)
BEGIN
    ALTER TABLE [ApprovalSteps] ADD [LastReminderAtUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718233809_AddApprovalStepReminderTracking'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260718233809_AddApprovalStepReminderTracking', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718234416_AddRolePermissions'
)
BEGIN
    CREATE TABLE [RolePermissions] (
        [RolePermissionId] bigint NOT NULL IDENTITY,
        [RoleName] nvarchar(64) NOT NULL,
        [PermissionKey] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RolePermissionId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718234416_AddRolePermissions'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RolePermissions_RoleName_PermissionKey] ON [RolePermissions] ([RoleName], [PermissionKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718234416_AddRolePermissions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260718234416_AddRolePermissions', N'8.0.10');
END;
GO

COMMIT;
GO

