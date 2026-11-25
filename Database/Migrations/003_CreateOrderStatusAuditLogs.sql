SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT 1
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'dbo.OrderStatusAuditLogs')
      AND type = N'U'
)
BEGIN
    CREATE TABLE dbo.OrderStatusAuditLogs
    (
        AuditLogId     BIGINT         IDENTITY(1,1)   NOT NULL,
        OrderId        BIGINT                          NOT NULL,
        AdminUserId    BIGINT                          NULL,
        CustomerUserId BIGINT                          NULL,
        OldStatus      NVARCHAR(60)                    NOT NULL,
        NewStatus      NVARCHAR(60)                    NOT NULL,
        Reason         NVARCHAR(500)                   NOT NULL,
        ChangedAt      DATETIME2(7)                    NOT NULL
            CONSTRAINT DF_OrderStatusAuditLogs_ChangedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_OrderStatusAuditLogs
            PRIMARY KEY CLUSTERED (AuditLogId),

        CONSTRAINT FK_OrderStatusAuditLogs_Orders
            FOREIGN KEY (OrderId)
            REFERENCES dbo.Orders (OrderId),

        CONSTRAINT FK_OrderStatusAuditLogs_Users
            FOREIGN KEY (AdminUserId)
            REFERENCES dbo.Users (UserId),

        CONSTRAINT FK_OrderStatusAuditLogs_CustomerUsers
            FOREIGN KEY (CustomerUserId)
            REFERENCES dbo.Users (UserId),

        CONSTRAINT CK_OrderStatusAuditLogs_Actor
            CHECK (AdminUserId IS NOT NULL OR CustomerUserId IS NOT NULL)
    );

    -- Supports queries like "show me all status changes for order X"
    CREATE NONCLUSTERED INDEX IX_OrderStatusAuditLogs_OrderId
        ON dbo.OrderStatusAuditLogs (OrderId)
        INCLUDE (ChangedAt, OldStatus, NewStatus);

    -- Supports queries like "show me all changes made by admin Y"
    CREATE NONCLUSTERED INDEX IX_OrderStatusAuditLogs_AdminUserId
        ON dbo.OrderStatusAuditLogs (AdminUserId)
        INCLUDE (ChangedAt, OrderId)
        WHERE AdminUserId IS NOT NULL;

    -- Supports queries like "show me all cancellations made by customer Z"
    CREATE NONCLUSTERED INDEX IX_OrderStatusAuditLogs_CustomerUserId
        ON dbo.OrderStatusAuditLogs (CustomerUserId)
        INCLUDE (ChangedAt, OrderId)
        WHERE CustomerUserId IS NOT NULL;

    PRINT 'Table dbo.OrderStatusAuditLogs created successfully with dual Admin/Customer audit support.';
END
ELSE
BEGIN
    -- Safe idempotent upgrade for existing installations
    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.OrderStatusAuditLogs')
          AND name = 'AdminUserId'
          AND is_nullable = 0
    )
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrderStatusAuditLogs_AdminUserId' AND object_id = OBJECT_ID('dbo.OrderStatusAuditLogs'))
            DROP INDEX IX_OrderStatusAuditLogs_AdminUserId ON dbo.OrderStatusAuditLogs;

        ALTER TABLE dbo.OrderStatusAuditLogs ALTER COLUMN AdminUserId BIGINT NULL;

        CREATE NONCLUSTERED INDEX IX_OrderStatusAuditLogs_AdminUserId
            ON dbo.OrderStatusAuditLogs (AdminUserId)
            INCLUDE (ChangedAt, OrderId)
            WHERE AdminUserId IS NOT NULL;
    END

    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.OrderStatusAuditLogs')
          AND name = 'CustomerUserId'
    )
    BEGIN
        ALTER TABLE dbo.OrderStatusAuditLogs ADD CustomerUserId BIGINT NULL
            CONSTRAINT FK_OrderStatusAuditLogs_CustomerUsers REFERENCES dbo.Users (UserId);
    END

    EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_OrderStatusAuditLogs_CustomerUserId'' AND object_id = OBJECT_ID(''dbo.OrderStatusAuditLogs''))
    BEGIN
        CREATE NONCLUSTERED INDEX IX_OrderStatusAuditLogs_CustomerUserId
            ON dbo.OrderStatusAuditLogs (CustomerUserId)
            INCLUDE (ChangedAt, OrderId)
            WHERE CustomerUserId IS NOT NULL;
    END';

    EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = ''CK_OrderStatusAuditLogs_Actor'')
    BEGIN
        ALTER TABLE dbo.OrderStatusAuditLogs ADD CONSTRAINT CK_OrderStatusAuditLogs_Actor
            CHECK (AdminUserId IS NOT NULL OR CustomerUserId IS NOT NULL);
    END';

    PRINT 'Table dbo.OrderStatusAuditLogs upgraded successfully for dual Admin/Customer audit support.';
END

COMMIT TRANSACTION;
