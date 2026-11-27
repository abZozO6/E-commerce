-- Migration: 004_HardenPaymentsSchema.sql
-- Target:    EcommerceDb
-- Purpose:   Harden Payments table constraints and indexes;
--            allow NULL AdminUserId in dbo.PaymentStatusAuditLogs for system/automated events (e.g. expiration).

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

-- ============================================================
-- 1. Ensure CK_Payments_Amount (Amount > 0)
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_Payments_Amount'
      AND parent_object_id = OBJECT_ID(N'dbo.Payments')
)
BEGIN
    ALTER TABLE dbo.Payments
        ADD CONSTRAINT CK_Payments_Amount CHECK (Amount > 0);
    PRINT 'Added CK_Payments_Amount.';
END;

-- ============================================================
-- 2. Ensure CK_Payments_Status
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_Payments_Status'
      AND parent_object_id = OBJECT_ID(N'dbo.Payments')
)
BEGIN
    ALTER TABLE dbo.Payments
        ADD CONSTRAINT CK_Payments_Status
        CHECK (Status IN ('Pending', 'Succeeded', 'Failed', 'Cancelled', 'Refunded'));
    PRINT 'Added CK_Payments_Status.';
END;

-- ============================================================
-- 3. Ensure UX_Payments_OneActivePaymentPerOrder (One Pending per Order)
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_Payments_OneActivePaymentPerOrder'
      AND object_id = OBJECT_ID(N'dbo.Payments')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_Payments_OneActivePaymentPerOrder
        ON dbo.Payments (OrderId)
        WHERE Status = 'Pending';
    PRINT 'Created UX_Payments_OneActivePaymentPerOrder.';
END;

-- ============================================================
-- 4. Ensure UX_Payments_Provider_ProviderReference
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_Payments_Provider_ProviderReference'
      AND object_id = OBJECT_ID(N'dbo.Payments')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_Payments_Provider_ProviderReference
        ON dbo.Payments (Provider, ProviderReference)
        WHERE ProviderReference IS NOT NULL;
    PRINT 'Created UX_Payments_Provider_ProviderReference.';
END;

-- ============================================================
-- 5. Allow NULL AdminUserId in dbo.PaymentStatusAuditLogs
--    (for automated system events such as payment expiration)
-- ============================================================
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'PaymentStatusAuditLogs'
      AND COLUMN_NAME = 'AdminUserId'
      AND IS_NULLABLE = 'NO'
)
BEGIN
    IF EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = 'IX_PaymentStatusAuditLogs_AdminUserId'
          AND object_id = OBJECT_ID(N'dbo.PaymentStatusAuditLogs')
    )
    BEGIN
        DROP INDEX IX_PaymentStatusAuditLogs_AdminUserId ON dbo.PaymentStatusAuditLogs;
    END;

    ALTER TABLE dbo.PaymentStatusAuditLogs
        ALTER COLUMN AdminUserId BIGINT NULL;

    CREATE NONCLUSTERED INDEX IX_PaymentStatusAuditLogs_AdminUserId
        ON dbo.PaymentStatusAuditLogs (AdminUserId)
        INCLUDE (ChangedAt, PaymentId)
        WHERE AdminUserId IS NOT NULL;

    PRINT 'dbo.PaymentStatusAuditLogs.AdminUserId updated to nullable.';
END;

COMMIT TRANSACTION;
GO
