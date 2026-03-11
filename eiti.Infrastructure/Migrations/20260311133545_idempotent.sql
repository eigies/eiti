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
    WHERE [MigrationId] = N'20260228001204_InitialCreate'
)
BEGIN
    CREATE TABLE [Companies] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [PrimaryDomain] nvarchar(255) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228001204_InitialCreate'
)
BEGIN
    ALTER TABLE [Users] ADD [CompanyId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228001204_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [Price] decimal(18,2) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Products_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228001204_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Companies_PrimaryDomain] ON [Companies] ([PrimaryDomain]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228001204_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_CompanyId_Name] ON [Products] ([CompanyId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228001204_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'PrimaryDomain', N'CreatedAt') AND [object_id] = OBJECT_ID(N'[Companies]'))
        SET IDENTITY_INSERT [Companies] ON;
    EXEC(N'INSERT INTO [Companies] ([Id], [Name], [PrimaryDomain], [CreatedAt])
    VALUES (''11111111-1111-1111-1111-111111111111'', N''Legacy Company'', N''legacy.local'', ''2026-03-11T14:01:12.7703195Z'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'PrimaryDomain', N'CreatedAt') AND [object_id] = OBJECT_ID(N'[Companies]'))
        SET IDENTITY_INSERT [Companies] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228001204_InitialCreate'
)
BEGIN
    UPDATE [Users] SET [CompanyId] = '11111111-1111-1111-1111-111111111111' WHERE [CompanyId] IS NULL
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228001204_InitialCreate'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'CompanyId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Users] ALTER COLUMN [CompanyId] uniqueidentifier NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228001204_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_CompanyId] ON [Users] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228001204_InitialCreate'
)
BEGIN
    ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228001204_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260228001204_InitialCreate', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228022543_AddProductBrandAndSales'
)
BEGIN
    ALTER TABLE [Products] ADD [Brand] nvarchar(100) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228022543_AddProductBrandAndSales'
)
BEGIN
    CREATE TABLE [Sales] (
        [Id] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Sales] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Sales_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Sales_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228022543_AddProductBrandAndSales'
)
BEGIN
    CREATE INDEX [IX_Sales_CompanyId_CreatedAt] ON [Sales] ([CompanyId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228022543_AddProductBrandAndSales'
)
BEGIN
    CREATE INDEX [IX_Sales_ProductId] ON [Sales] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228022543_AddProductBrandAndSales'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260228022543_AddProductBrandAndSales', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228024414_RefactorSalesToDetailsAndStatus'
)
BEGIN
    ALTER TABLE [Sales] DROP CONSTRAINT [FK_Sales_Products_ProductId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228024414_RefactorSalesToDetailsAndStatus'
)
BEGIN
    DROP INDEX [IX_Sales_ProductId] ON [Sales];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228024414_RefactorSalesToDetailsAndStatus'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sales]') AND [c].[name] = N'ProductId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Sales] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Sales] DROP COLUMN [ProductId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228024414_RefactorSalesToDetailsAndStatus'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sales]') AND [c].[name] = N'UnitPrice');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Sales] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [Sales] DROP COLUMN [UnitPrice];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228024414_RefactorSalesToDetailsAndStatus'
)
BEGIN
    EXEC sp_rename N'[Sales].[Quantity]', N'IdSaleStatus', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228024414_RefactorSalesToDetailsAndStatus'
)
BEGIN
    CREATE TABLE [SaleDetails] (
        [SaleId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_SaleDetails] PRIMARY KEY ([SaleId], [ProductId]),
        CONSTRAINT [FK_SaleDetails_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SaleDetails_Sales_SaleId] FOREIGN KEY ([SaleId]) REFERENCES [Sales] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228024414_RefactorSalesToDetailsAndStatus'
)
BEGIN
    CREATE INDEX [IX_SaleDetails_ProductId] ON [SaleDetails] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228024414_RefactorSalesToDetailsAndStatus'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260228024414_RefactorSalesToDetailsAndStatus', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228030420_AddSalesListingAndEditingSupport'
)
BEGIN
    ALTER TABLE [Sales] ADD [IsModified] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228030420_AddSalesListingAndEditingSupport'
)
BEGIN
    ALTER TABLE [Sales] ADD [UpdatedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228030420_AddSalesListingAndEditingSupport'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260228030420_AddSalesListingAndEditingSupport', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    CREATE TABLE [Branches] (
        [Id] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Code] nvarchar(40) NULL,
        [Address] nvarchar(255) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Branches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Branches_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    CREATE TABLE [CashDrawers] (
        [Id] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_CashDrawers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CashDrawers_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CashDrawers_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    CREATE TABLE [CashSessions] (
        [Id] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [CashDrawerId] uniqueidentifier NOT NULL,
        [OpenedByUserId] uniqueidentifier NOT NULL,
        [ClosedByUserId] uniqueidentifier NULL,
        [OpenedAt] datetime2 NOT NULL,
        [ClosedAt] datetime2 NULL,
        [OpeningAmount] decimal(18,2) NOT NULL,
        [ActualClosingAmount] decimal(18,2) NULL,
        [Status] int NOT NULL,
        [Notes] nvarchar(255) NULL,
        CONSTRAINT [PK_CashSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CashSessions_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CashSessions_CashDrawers_CashDrawerId] FOREIGN KEY ([CashDrawerId]) REFERENCES [CashDrawers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CashSessions_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CashSessions_Users_ClosedByUserId] FOREIGN KEY ([ClosedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CashSessions_Users_OpenedByUserId] FOREIGN KEY ([OpenedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    CREATE TABLE [CashMovements] (
        [Id] uniqueidentifier NOT NULL,
        [CashSessionId] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [Direction] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [OccurredAt] datetime2 NOT NULL,
        [ReferenceType] nvarchar(50) NULL,
        [ReferenceId] uniqueidentifier NULL,
        [Description] nvarchar(255) NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_CashMovements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CashMovements_CashSessions_CashSessionId] FOREIGN KEY ([CashSessionId]) REFERENCES [CashSessions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CashMovements_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    ALTER TABLE [Sales] ADD [BranchId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    ALTER TABLE [Sales] ADD [CashSessionId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    ALTER TABLE [Sales] ADD [PaidAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Branches_CompanyId_Name] ON [Branches] ([CompanyId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CashDrawers_BranchId_Name] ON [CashDrawers] ([BranchId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    CREATE INDEX [IX_CashDrawers_CompanyId] ON [CashDrawers] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    CREATE INDEX [IX_CashMovements_CashSessionId_OccurredAt] ON [CashMovements] ([CashSessionId], [OccurredAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    CREATE INDEX [IX_CashMovements_CreatedByUserId] ON [CashMovements] ([CreatedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    CREATE INDEX [IX_CashSessions_BranchId] ON [CashSessions] ([BranchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    CREATE INDEX [IX_CashSessions_CashDrawerId_OpenedAt] ON [CashSessions] ([CashDrawerId], [OpenedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    CREATE INDEX [IX_CashSessions_CashDrawerId_Status] ON [CashSessions] ([CashDrawerId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    CREATE INDEX [IX_CashSessions_ClosedByUserId] ON [CashSessions] ([ClosedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    CREATE INDEX [IX_CashSessions_CompanyId] ON [CashSessions] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    CREATE INDEX [IX_CashSessions_OpenedByUserId] ON [CashSessions] ([OpenedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    CREATE INDEX [IX_Sales_BranchId] ON [Sales] ([BranchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    CREATE INDEX [IX_Sales_CashSessionId] ON [Sales] ([CashSessionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    DECLARE @BranchMap TABLE
    (
        CompanyId UNIQUEIDENTIFIER NOT NULL,
        BranchId UNIQUEIDENTIFIER NOT NULL
    );
    INSERT INTO Branches (Id, CompanyId, Name, Code, Address, CreatedAt, UpdatedAt)
    OUTPUT inserted.CompanyId, inserted.Id INTO @BranchMap (CompanyId, BranchId)
    SELECT NEWID(), c.Id, 'Sucursal Principal', 'MAIN', NULL, SYSUTCDATETIME(), NULL
    FROM Companies c;
    INSERT INTO CashDrawers (Id, CompanyId, BranchId, Name, IsActive, CreatedAt, UpdatedAt)
    SELECT NEWID(), b.CompanyId, b.BranchId, 'Caja Principal', 1, SYSUTCDATETIME(), NULL
    FROM @BranchMap b;
    UPDATE s
    SET s.BranchId = m.BranchId
    FROM Sales s
    INNER JOIN @BranchMap m ON m.CompanyId = s.CompanyId;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    DROP INDEX [IX_Sales_BranchId] ON [Sales];
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sales]') AND [c].[name] = N'BranchId');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Sales] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [Sales] ALTER COLUMN [BranchId] uniqueidentifier NOT NULL;
    CREATE INDEX [IX_Sales_BranchId] ON [Sales] ([BranchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    ALTER TABLE [Sales] ADD CONSTRAINT [FK_Sales_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    ALTER TABLE [Sales] ADD CONSTRAINT [FK_Sales_CashSessions_CashSessionId] FOREIGN KEY ([CashSessionId]) REFERENCES [CashSessions] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228042439_AddBranchesAndCashManagementGenerated'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260228042439_AddBranchesAndCashManagementGenerated', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    ALTER TABLE [Sales] ADD [HasDelivery] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    ALTER TABLE [Sales] ADD [TransportAssignmentId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    CREATE TABLE [DriverProfiles] (
        [EmployeeId] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [LicenseNumber] nvarchar(60) NOT NULL,
        [LicenseCategory] nvarchar(40) NULL,
        [LicenseExpiresAt] datetime2 NULL,
        [EmergencyContactName] nvarchar(120) NULL,
        [EmergencyContactPhone] nvarchar(40) NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_DriverProfiles] PRIMARY KEY ([EmployeeId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    CREATE TABLE [Employees] (
        [Id] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NULL,
        [FirstName] nvarchar(80) NOT NULL,
        [LastName] nvarchar(80) NOT NULL,
        [DocumentNumber] nvarchar(40) NULL,
        [Phone] nvarchar(40) NULL,
        [Email] nvarchar(160) NULL,
        [EmployeeRole] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    CREATE TABLE [FleetLogs] (
        [Id] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [VehicleId] uniqueidentifier NOT NULL,
        [PerformedByEmployeeId] uniqueidentifier NULL,
        [Type] int NOT NULL,
        [OccurredAt] datetime2 NOT NULL,
        [Odometer] decimal(18,2) NULL,
        [FuelLiters] decimal(18,2) NULL,
        [FuelCost] decimal(18,2) NULL,
        [MaintenanceType] nvarchar(80) NULL,
        [Description] nvarchar(240) NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_FleetLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    CREATE TABLE [SaleTransportAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [SaleId] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [DriverEmployeeId] uniqueidentifier NOT NULL,
        [VehicleId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [AssignedAt] datetime2 NOT NULL,
        [DispatchedAt] datetime2 NULL,
        [DeliveredAt] datetime2 NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_SaleTransportAssignments] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    CREATE TABLE [Vehicles] (
        [Id] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NULL,
        [AssignedDriverEmployeeId] uniqueidentifier NULL,
        [Plate] nvarchar(20) NOT NULL,
        [Model] nvarchar(120) NOT NULL,
        [Brand] nvarchar(120) NULL,
        [Year] int NULL,
        [FuelType] int NOT NULL,
        [CurrentOdometer] decimal(18,2) NULL,
        [LastFuelLoadedAt] datetime2 NULL,
        [LastMaintenanceAt] datetime2 NULL,
        [Notes] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Vehicles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    CREATE INDEX [IX_Sales_TransportAssignmentId] ON [Sales] ([TransportAssignmentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DriverProfiles_CompanyId_LicenseNumber] ON [DriverProfiles] ([CompanyId], [LicenseNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    CREATE INDEX [IX_Employees_CompanyId_EmployeeRole_IsActive] ON [Employees] ([CompanyId], [EmployeeRole], [IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    CREATE INDEX [IX_Employees_CompanyId_LastName_FirstName] ON [Employees] ([CompanyId], [LastName], [FirstName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    CREATE INDEX [IX_FleetLogs_CompanyId_Type_OccurredAt] ON [FleetLogs] ([CompanyId], [Type], [OccurredAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    CREATE INDEX [IX_FleetLogs_VehicleId_OccurredAt] ON [FleetLogs] ([VehicleId], [OccurredAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    CREATE INDEX [IX_SaleTransportAssignments_CompanyId_DriverEmployeeId_Status] ON [SaleTransportAssignments] ([CompanyId], [DriverEmployeeId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    CREATE INDEX [IX_SaleTransportAssignments_CompanyId_VehicleId_Status] ON [SaleTransportAssignments] ([CompanyId], [VehicleId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SaleTransportAssignments_SaleId] ON [SaleTransportAssignments] ([SaleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    CREATE INDEX [IX_Vehicles_CompanyId_AssignedDriverEmployeeId_IsActive] ON [Vehicles] ([CompanyId], [AssignedDriverEmployeeId], [IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Vehicles_CompanyId_Plate] ON [Vehicles] ([CompanyId], [Plate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    ALTER TABLE [Sales] ADD CONSTRAINT [FK_Sales_SaleTransportAssignments_TransportAssignmentId] FOREIGN KEY ([TransportAssignmentId]) REFERENCES [SaleTransportAssignments] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228060902_AddTransportManagement'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260228060902_AddTransportManagement', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228211740_AddCustomerAddressesAndSaleCustomer'
)
BEGIN
    ALTER TABLE [Sales] ADD [CustomerId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228211740_AddCustomerAddressesAndSaleCustomer'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Customers]') AND [c].[name] = N'Name');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Customers] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [Customers] ALTER COLUMN [Name] nvarchar(201) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228211740_AddCustomerAddressesAndSaleCustomer'
)
BEGIN
    ALTER TABLE [Customers] ADD [AddressId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228211740_AddCustomerAddressesAndSaleCustomer'
)
BEGIN
    ALTER TABLE [Customers] ADD [DocumentNumber] nvarchar(30) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228211740_AddCustomerAddressesAndSaleCustomer'
)
BEGIN
    ALTER TABLE [Customers] ADD [DocumentType] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228211740_AddCustomerAddressesAndSaleCustomer'
)
BEGIN
    ALTER TABLE [Customers] ADD [FirstName] nvarchar(100) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228211740_AddCustomerAddressesAndSaleCustomer'
)
BEGIN
    ALTER TABLE [Customers] ADD [LastName] nvarchar(100) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228211740_AddCustomerAddressesAndSaleCustomer'
)
BEGIN
    ALTER TABLE [Customers] ADD [Phone] nvarchar(30) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228211740_AddCustomerAddressesAndSaleCustomer'
)
BEGIN
    ALTER TABLE [Customers] ADD [TaxId] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228211740_AddCustomerAddressesAndSaleCustomer'
)
BEGIN
    CREATE TABLE [Addresses] (
        [Id] uniqueidentifier NOT NULL,
        [Street] nvarchar(120) NOT NULL,
        [StreetNumber] nvarchar(20) NOT NULL,
        [Floor] nvarchar(20) NULL,
        [Apartment] nvarchar(20) NULL,
        [PostalCode] nvarchar(20) NOT NULL,
        [City] nvarchar(100) NOT NULL,
        [StateOrProvince] nvarchar(100) NOT NULL,
        [Country] nvarchar(100) NOT NULL,
        [Reference] nvarchar(200) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Addresses] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228211740_AddCustomerAddressesAndSaleCustomer'
)
BEGIN
    CREATE INDEX [IX_Sales_CustomerId] ON [Sales] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228211740_AddCustomerAddressesAndSaleCustomer'
)
BEGIN
    CREATE INDEX [IX_Customers_AddressId] ON [Customers] ([AddressId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228211740_AddCustomerAddressesAndSaleCustomer'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Customers_DocumentType_DocumentNumber] ON [Customers] ([DocumentType], [DocumentNumber]) WHERE [DocumentType] IS NOT NULL AND [DocumentNumber] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228211740_AddCustomerAddressesAndSaleCustomer'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Customers_TaxId] ON [Customers] ([TaxId]) WHERE [TaxId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228211740_AddCustomerAddressesAndSaleCustomer'
)
BEGIN
    ALTER TABLE [Customers] ADD CONSTRAINT [FK_Customers_Addresses_AddressId] FOREIGN KEY ([AddressId]) REFERENCES [Addresses] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228211740_AddCustomerAddressesAndSaleCustomer'
)
BEGIN
    ALTER TABLE [Sales] ADD CONSTRAINT [FK_Sales_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228211740_AddCustomerAddressesAndSaleCustomer'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260228211740_AddCustomerAddressesAndSaleCustomer', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228230538_AddProductCodeAndSku'
)
BEGIN
    ALTER TABLE [Products] ADD [Code] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228230538_AddProductCodeAndSku'
)
BEGIN
    ALTER TABLE [Products] ADD [Sku] nvarchar(80) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228230538_AddProductCodeAndSku'
)
BEGIN
    UPDATE [Products]
    SET [Code] = CONCAT('PRD-', RIGHT(REPLACE(CONVERT(varchar(36), [Id]), '-', ''), 12))
    WHERE [Code] IS NULL OR LTRIM(RTRIM([Code])) = '';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228230538_AddProductCodeAndSku'
)
BEGIN
    UPDATE [Products]
    SET [Sku] = CONCAT('SKU-', RIGHT(REPLACE(CONVERT(varchar(36), [Id]), '-', ''), 16))
    WHERE [Sku] IS NULL OR LTRIM(RTRIM([Sku])) = '';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228230538_AddProductCodeAndSku'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'Code');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [Products] ALTER COLUMN [Code] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228230538_AddProductCodeAndSku'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'Sku');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [Products] ALTER COLUMN [Sku] nvarchar(80) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228230538_AddProductCodeAndSku'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_CompanyId_Code] ON [Products] ([CompanyId], [Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228230538_AddProductCodeAndSku'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_CompanyId_Sku] ON [Products] ([CompanyId], [Sku]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228230538_AddProductCodeAndSku'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260228230538_AddProductCodeAndSku', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228233348_AddCompanyOnboarding'
)
BEGIN
    CREATE TABLE [CompanyOnboarding] (
        [CompanyId] uniqueidentifier NOT NULL,
        [HasCreatedBranch] bit NOT NULL,
        [HasCreatedCashDrawer] bit NOT NULL,
        [HasCompletedInitialCashOpen] bit NOT NULL,
        [HasCreatedProduct] bit NOT NULL,
        [CompletedAt] datetime2 NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CompanyOnboarding] PRIMARY KEY ([CompanyId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228233348_AddCompanyOnboarding'
)
BEGIN
    INSERT INTO [CompanyOnboarding]
        ([CompanyId], [HasCreatedBranch], [HasCreatedCashDrawer], [HasCompletedInitialCashOpen], [HasCreatedProduct], [CompletedAt], [UpdatedAt])
    SELECT
        [Id],
        CAST(1 AS bit),
        CAST(1 AS bit),
        CAST(1 AS bit),
        CAST(1 AS bit),
        GETUTCDATE(),
        GETUTCDATE()
    FROM [Companies]
    WHERE NOT EXISTS (
        SELECT 1
        FROM [CompanyOnboarding] co
        WHERE co.[CompanyId] = [Companies].[Id]
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260228233348_AddCompanyOnboarding'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260228233348_AddCompanyOnboarding', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301191612_AddBranchProductStock'
)
BEGIN
    CREATE TABLE [BranchProductStocks] (
        [Id] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [OnHandQuantity] int NOT NULL,
        [ReservedQuantity] int NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_BranchProductStocks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BranchProductStocks_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BranchProductStocks_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BranchProductStocks_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301191612_AddBranchProductStock'
)
BEGIN
    CREATE TABLE [StockMovements] (
        [Id] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [BranchProductStockId] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [Quantity] int NOT NULL,
        [ReferenceType] nvarchar(50) NULL,
        [ReferenceId] uniqueidentifier NULL,
        [Description] nvarchar(255) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedByUserId] uniqueidentifier NULL,
        CONSTRAINT [PK_StockMovements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockMovements_BranchProductStocks_BranchProductStockId] FOREIGN KEY ([BranchProductStockId]) REFERENCES [BranchProductStocks] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockMovements_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockMovements_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockMovements_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301191612_AddBranchProductStock'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BranchProductStocks_BranchId_ProductId] ON [BranchProductStocks] ([BranchId], [ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301191612_AddBranchProductStock'
)
BEGIN
    CREATE INDEX [IX_BranchProductStocks_CompanyId_BranchId] ON [BranchProductStocks] ([CompanyId], [BranchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301191612_AddBranchProductStock'
)
BEGIN
    CREATE INDEX [IX_BranchProductStocks_CompanyId_ProductId] ON [BranchProductStocks] ([CompanyId], [ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301191612_AddBranchProductStock'
)
BEGIN
    CREATE INDEX [IX_BranchProductStocks_ProductId] ON [BranchProductStocks] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301191612_AddBranchProductStock'
)
BEGIN
    CREATE INDEX [IX_StockMovements_BranchId_ProductId_CreatedAt] ON [StockMovements] ([BranchId], [ProductId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301191612_AddBranchProductStock'
)
BEGIN
    CREATE INDEX [IX_StockMovements_BranchProductStockId] ON [StockMovements] ([BranchProductStockId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301191612_AddBranchProductStock'
)
BEGIN
    CREATE INDEX [IX_StockMovements_CompanyId_CreatedAt] ON [StockMovements] ([CompanyId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301191612_AddBranchProductStock'
)
BEGIN
    CREATE INDEX [IX_StockMovements_ProductId] ON [StockMovements] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301191612_AddBranchProductStock'
)
BEGIN
    CREATE INDEX [IX_StockMovements_ReferenceId] ON [StockMovements] ([ReferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301191612_AddBranchProductStock'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260301191612_AddBranchProductStock', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301200055_AddOnboardingInitialStock'
)
BEGIN
    ALTER TABLE [CompanyOnboarding] ADD [HasLoadedInitialStock] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301200055_AddOnboardingInitialStock'
)
BEGIN
    UPDATE [CompanyOnboarding]
    SET [HasLoadedInitialStock] = CAST(1 AS bit)
    WHERE [CompletedAt] IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301200055_AddOnboardingInitialStock'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260301200055_AddOnboardingInitialStock', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307015459_AddUserRolesAndPermissions'
)
BEGIN
    ALTER TABLE [Users] ADD [EmployeeId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307015459_AddUserRolesAndPermissions'
)
BEGIN
    ALTER TABLE [Users] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307015459_AddUserRolesAndPermissions'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleCode] nvarchar(40) NOT NULL,
        [AssignedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleCode]),
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307015459_AddUserRolesAndPermissions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260307015459_AddUserRolesAndPermissions', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307020838_AddUserRoleAudit'
)
BEGIN
    CREATE TABLE [UserRoleAudits] (
        [Id] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [TargetUserId] uniqueidentifier NOT NULL,
        [ChangedByUserId] uniqueidentifier NULL,
        [PreviousRolesCsv] nvarchar(500) NOT NULL,
        [NewRolesCsv] nvarchar(500) NOT NULL,
        [ChangedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserRoleAudits] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307020838_AddUserRoleAudit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260307020838_AddUserRoleAudit', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308233044_AddCustomerCompanyScope'
)
BEGIN
    DROP INDEX [IX_Customers_DocumentType_DocumentNumber] ON [Customers];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308233044_AddCustomerCompanyScope'
)
BEGIN
    DROP INDEX [IX_Customers_Email] ON [Customers];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308233044_AddCustomerCompanyScope'
)
BEGIN
    DROP INDEX [IX_Customers_TaxId] ON [Customers];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308233044_AddCustomerCompanyScope'
)
BEGIN
    ALTER TABLE [Customers] ADD [CompanyId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308233044_AddCustomerCompanyScope'
)
BEGIN
    DECLARE @FallbackCompanyId uniqueidentifier = '11111111-1111-1111-1111-111111111111';
    IF NOT EXISTS (SELECT 1 FROM [Companies] WHERE [Id] = @FallbackCompanyId)
        THROW 50001, 'Migration aborted: fallback company 11111111-1111-1111-1111-111111111111 was not found.', 1;
    CREATE TABLE #CustomerCompanyMap
    (
        [OriginalCustomerId] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [TargetCustomerId] uniqueidentifier NULL,
        [IsPrimary] bit NOT NULL DEFAULT(0),
        CONSTRAINT [PK_CustomerCompanyMap] PRIMARY KEY ([OriginalCustomerId], [CompanyId])
    );
    INSERT INTO #CustomerCompanyMap ([OriginalCustomerId], [CompanyId])
    SELECT DISTINCT s.[CustomerId], s.[CompanyId]
    FROM [Sales] s
    WHERE s.[CustomerId] IS NOT NULL;
    INSERT INTO #CustomerCompanyMap ([OriginalCustomerId], [CompanyId])
    SELECT c.[Id], @FallbackCompanyId
    FROM [Customers] c
    WHERE NOT EXISTS (
        SELECT 1
        FROM #CustomerCompanyMap m
        WHERE m.[OriginalCustomerId] = c.[Id]
    );
    ;WITH RankedMap AS
    (
        SELECT
            m.[OriginalCustomerId],
            m.[CompanyId],
            ROW_NUMBER() OVER (PARTITION BY m.[OriginalCustomerId] ORDER BY m.[CompanyId]) AS [RowNumber]
        FROM #CustomerCompanyMap m
    )
    UPDATE m
    SET
        m.[IsPrimary] = 1,
        m.[TargetCustomerId] = m.[OriginalCustomerId]
    FROM #CustomerCompanyMap m
    INNER JOIN RankedMap r
        ON r.[OriginalCustomerId] = m.[OriginalCustomerId]
        AND r.[CompanyId] = m.[CompanyId]
    WHERE r.[RowNumber] = 1;
    UPDATE c
    SET c.[CompanyId] = m.[CompanyId]
    FROM [Customers] c
    INNER JOIN #CustomerCompanyMap m
        ON m.[OriginalCustomerId] = c.[Id]
    WHERE m.[IsPrimary] = 1;
    CREATE TABLE #CustomersToClone
    (
        [OriginalCustomerId] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [NewCustomerId] uniqueidentifier NOT NULL,
        [Name] nvarchar(201) NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [Email] nvarchar(255) NOT NULL,
        [Phone] nvarchar(30) NOT NULL,
        [DocumentType] int NULL,
        [DocumentNumber] nvarchar(30) NULL,
        [TaxId] nvarchar(20) NULL,
        [AddressId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_CustomersToClone] PRIMARY KEY ([OriginalCustomerId], [CompanyId])
    );
    INSERT INTO #CustomersToClone
    (
        [OriginalCustomerId],
        [CompanyId],
        [NewCustomerId],
        [Name],
        [FirstName],
        [LastName],
        [Email],
        [Phone],
        [DocumentType],
        [DocumentNumber],
        [TaxId],
        [AddressId],
        [CreatedAt],
        [UpdatedAt]
    )
    SELECT
        m.[OriginalCustomerId],
        m.[CompanyId],
        NEWID(),
        c.[Name],
        c.[FirstName],
        c.[LastName],
        c.[Email],
        c.[Phone],
        c.[DocumentType],
        c.[DocumentNumber],
        c.[TaxId],
        c.[AddressId],
        c.[CreatedAt],
        c.[UpdatedAt]
    FROM #CustomerCompanyMap m
    INNER JOIN [Customers] c
        ON c.[Id] = m.[OriginalCustomerId]
    WHERE m.[IsPrimary] = 0;
    INSERT INTO [Customers]
    (
        [Id],
        [CompanyId],
        [Name],
        [FirstName],
        [LastName],
        [Email],
        [Phone],
        [DocumentType],
        [DocumentNumber],
        [TaxId],
        [AddressId],
        [CreatedAt],
        [UpdatedAt]
    )
    SELECT
        [NewCustomerId],
        [CompanyId],
        [Name],
        [FirstName],
        [LastName],
        [Email],
        [Phone],
        [DocumentType],
        [DocumentNumber],
        [TaxId],
        [AddressId],
        [CreatedAt],
        [UpdatedAt]
    FROM #CustomersToClone;
    UPDATE m
    SET m.[TargetCustomerId] = c.[NewCustomerId]
    FROM #CustomerCompanyMap m
    INNER JOIN #CustomersToClone c
        ON c.[OriginalCustomerId] = m.[OriginalCustomerId]
        AND c.[CompanyId] = m.[CompanyId]
    WHERE m.[IsPrimary] = 0;
    UPDATE s
    SET s.[CustomerId] = m.[TargetCustomerId]
    FROM [Sales] s
    INNER JOIN #CustomerCompanyMap m
        ON m.[OriginalCustomerId] = s.[CustomerId]
        AND m.[CompanyId] = s.[CompanyId]
    WHERE s.[CustomerId] IS NOT NULL
        AND s.[CustomerId] <> m.[TargetCustomerId];
    IF EXISTS (SELECT 1 FROM [Customers] WHERE [CompanyId] IS NULL)
        THROW 50002, 'Migration aborted: unresolved customers without CompanyId.', 1;
    DROP TABLE #CustomersToClone;
    DROP TABLE #CustomerCompanyMap;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308233044_AddCustomerCompanyScope'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Customers]') AND [c].[name] = N'CompanyId');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Customers] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [Customers] ALTER COLUMN [CompanyId] uniqueidentifier NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308233044_AddCustomerCompanyScope'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Customers_CompanyId_DocumentType_DocumentNumber] ON [Customers] ([CompanyId], [DocumentType], [DocumentNumber]) WHERE [DocumentType] IS NOT NULL AND [DocumentNumber] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308233044_AddCustomerCompanyScope'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Customers_CompanyId_Email] ON [Customers] ([CompanyId], [Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308233044_AddCustomerCompanyScope'
)
BEGIN
    CREATE INDEX [IX_Customers_CompanyId_Name] ON [Customers] ([CompanyId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308233044_AddCustomerCompanyScope'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Customers_CompanyId_TaxId] ON [Customers] ([CompanyId], [TaxId]) WHERE [TaxId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308233044_AddCustomerCompanyScope'
)
BEGIN
    ALTER TABLE [Customers] ADD CONSTRAINT [FK_Customers_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308233044_AddCustomerCompanyScope'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260308233044_AddCustomerCompanyScope', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260311133545_AddSaleSettlementPaymentsAndTradeIns'
)
BEGIN
    CREATE TABLE [SalePayments] (
        [SaleId] uniqueidentifier NOT NULL,
        [Method] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Reference] nvarchar(120) NULL,
        CONSTRAINT [PK_SalePayments] PRIMARY KEY ([SaleId], [Method]),
        CONSTRAINT [FK_SalePayments_Sales_SaleId] FOREIGN KEY ([SaleId]) REFERENCES [Sales] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260311133545_AddSaleSettlementPaymentsAndTradeIns'
)
BEGIN
    CREATE TABLE [SaleTradeIns] (
        [SaleId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Quantity] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_SaleTradeIns] PRIMARY KEY ([SaleId], [ProductId]),
        CONSTRAINT [FK_SaleTradeIns_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SaleTradeIns_Sales_SaleId] FOREIGN KEY ([SaleId]) REFERENCES [Sales] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260311133545_AddSaleSettlementPaymentsAndTradeIns'
)
BEGIN
    CREATE INDEX [IX_SaleTradeIns_ProductId] ON [SaleTradeIns] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260311133545_AddSaleSettlementPaymentsAndTradeIns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260311133545_AddSaleSettlementPaymentsAndTradeIns', N'8.0.0');
END;
GO

COMMIT;
GO

