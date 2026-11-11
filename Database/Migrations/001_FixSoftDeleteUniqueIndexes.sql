-- Migration: 001_FixSoftDeleteUniqueIndexes.sql
-- Target:    EcommerceDb (Local Development)
-- Purpose:   ISSUE-01 — Replace unfiltered unique constraints on dbo.Brands and
--            dbo.Categories with filtered unique indexes (WHERE IsDeleted = 0)
--            so soft-deleted names/slugs can be reused while enforcing active uniqueness.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

-- 1. dbo.Categories: Remove redundant unfiltered UQ_Categories_Slug (UX_Categories_Slug_Active already exists)
IF EXISTS (
    SELECT 1
    FROM sys.key_constraints
    WHERE name = N'UQ_Categories_Slug'
      AND parent_object_id = OBJECT_ID(N'dbo.Categories')
)
BEGIN
    ALTER TABLE dbo.Categories DROP CONSTRAINT UQ_Categories_Slug;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Categories_Slug_Active'
      AND object_id = OBJECT_ID(N'dbo.Categories')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_Categories_Slug_Active
        ON dbo.Categories (Slug)
        WHERE IsDeleted = 0;
END;

-- 2. dbo.Brands: Replace unfiltered UQ_Brands_Name and UQ_Brands_Slug with filtered unique indexes
IF EXISTS (
    SELECT 1
    FROM sys.key_constraints
    WHERE name = N'UQ_Brands_Name'
      AND parent_object_id = OBJECT_ID(N'dbo.Brands')
)
BEGIN
    ALTER TABLE dbo.Brands DROP CONSTRAINT UQ_Brands_Name;
END;

IF EXISTS (
    SELECT 1
    FROM sys.key_constraints
    WHERE name = N'UQ_Brands_Slug'
      AND parent_object_id = OBJECT_ID(N'dbo.Brands')
)
BEGIN
    ALTER TABLE dbo.Brands DROP CONSTRAINT UQ_Brands_Slug;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Brands_Name_Active'
      AND object_id = OBJECT_ID(N'dbo.Brands')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_Brands_Name_Active
        ON dbo.Brands (Name)
        WHERE IsDeleted = 0;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Brands_Slug_Active'
      AND object_id = OBJECT_ID(N'dbo.Brands')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_Brands_Slug_Active
        ON dbo.Brands (Slug)
        WHERE IsDeleted = 0;
END;

COMMIT TRANSACTION;
GO
