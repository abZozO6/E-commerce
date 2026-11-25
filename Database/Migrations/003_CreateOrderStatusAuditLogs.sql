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
        AuditLogId   BIGINT         IDENTITY(1,1)   NOT NULL,
        OrderId      BIGINT                          NOT NULL,
        AdminUserId  BIGINT                          NOT NULL,
        OldStatus    NVARCHAR(60)                    NOT NULL,
        NewStatus    NVARCHAR(60)                    NOT NULL,
        Reason       NVARCHAR(500)                   NOT NULL,
        ChangedAt    DATETIME2(7)                    NOT NULL
            CONSTRAINT DF_OrderStatusAuditLogs_ChangedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_OrderStatusAuditLogs
            PRIMARY KEY CLUSTERED (AuditLogId),

        CONSTRAINT FK_OrderStatusAuditLogs_Orders
            FOREIGN KEY (OrderId)
            REFERENCES dbo.Orders (OrderId),

        CONSTRAINT FK_OrderStatusAuditLogs_Users
            FOREIGN KEY (AdminUserId)
            REFERENCES dbo.Users (UserId)
    );

    -- Supports queries like "show me all status changes for order X"
    CREATE NONCLUSTERED INDEX IX_OrderStatusAuditLogs_OrderId
        ON dbo.OrderStatusAuditLogs (OrderId)
        INCLUDE (ChangedAt, OldStatus, NewStatus);

    -- Supports queries like "show me all changes made by admin Y"
    CREATE NONCLUSTERED INDEX IX_OrderStatusAuditLogs_AdminUserId
        ON dbo.OrderStatusAuditLogs (AdminUserId)
        INCLUDE (ChangedAt, OrderId);

    PRINT 'Table dbo.OrderStatusAuditLogs created successfully.';
END
ELSE
BEGIN
    PRINT 'Table dbo.OrderStatusAuditLogs already exists. Migration skipped.';
END

COMMIT TRANSACTION;
