-- Migration: 002_CreatePaymentStatusAuditLogs.sql
-- Target:    EcommerceDb (Local Development)
-- Purpose:   Create dbo.PaymentStatusAuditLogs to record every Admin-initiated
--            manual payment status change, with appropriate FK constraints and indexes.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

-- ============================================================
-- 1. Create dbo.PaymentStatusAuditLogs
-- ============================================================
IF NOT EXISTS (
    SELECT 1
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'dbo.PaymentStatusAuditLogs')
      AND type = N'U'
)
BEGIN
    CREATE TABLE dbo.PaymentStatusAuditLogs (
        AuditLogId          BIGINT          IDENTITY(1,1)   NOT NULL,
        PaymentId           BIGINT                          NOT NULL,
        AdminUserId         BIGINT                          NOT NULL,
        OldStatus           NVARCHAR(30)                    NOT NULL,
        NewStatus           NVARCHAR(30)                    NOT NULL,
        TransactionReference NVARCHAR(200)                  NULL,
        Reason              NVARCHAR(500)                   NOT NULL,
        ChangedAt           DATETIME2(7)    NOT NULL        DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_PaymentStatusAuditLogs
            PRIMARY KEY CLUSTERED (AuditLogId),

        CONSTRAINT FK_PaymentStatusAuditLogs_Payments
            FOREIGN KEY (PaymentId)
            REFERENCES dbo.Payments (PaymentId),

        CONSTRAINT FK_PaymentStatusAuditLogs_Users
            FOREIGN KEY (AdminUserId)
            REFERENCES dbo.Users (UserId)
    );

    -- Index: fast lookup of audit history for a specific payment (most common query)
    CREATE NONCLUSTERED INDEX IX_PaymentStatusAuditLogs_PaymentId
        ON dbo.PaymentStatusAuditLogs (PaymentId)
        INCLUDE (ChangedAt, OldStatus, NewStatus, AdminUserId);

    -- Index: lookup of all actions performed by a specific admin user
    CREATE NONCLUSTERED INDEX IX_PaymentStatusAuditLogs_AdminUserId
        ON dbo.PaymentStatusAuditLogs (AdminUserId)
        INCLUDE (ChangedAt, PaymentId);
END;

COMMIT TRANSACTION;
GO
