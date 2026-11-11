# Detailed Findings & Resolution Status Report
**Date:** 2026-11-11  
**Project:** EcommerceSystem — ASP.NET Core 8 / Dapper / SQL Server 2022

---

## Finding Status Summary

### Earlier Nov 11 Fixes (Retained & Re-Verified)
| ID | Description | Status | Evidence |
|---|---|---|---|
| **FIX-10** | `ICartItemService` missing from `DI.AddApplication()` | **FIXED & VERIFIED** | DI validation resolved 29/29; cart item add/update tests passed |
| **FIX-11** | Inverted `OrderItemId <= 0` guard in `OrderItemRepository.CreateAsync` | **FIXED & VERIFIED** | Verified with order creation & batch tests |
| **FIX-12** | `CartItemRepository.AddOrIncrementAsync` SQL Error 134 (`@CartItemId`) | **FIXED & VERIFIED** | Multi-item checkout regression tests passed via `AddItemAsync` |
| **FIX-13** | Duplicate `using Ecommerce.Core.Entities;` (`CS0105`) | **FIXED & VERIFIED** | `dotnet build --no-incremental` reported 0 warnings |

### Current Resolution Pass (All 18 Section 4 Items)

| ID | Priority | Title | Status |
|---|---|---|---|
| **BUG-03** | High | Payment → Order status mapping (`CK_Orders_PaymentStatus`) | **FIXED & VERIFIED** |
| **BUG-04** | High | Inventory transaction type alignment (`CK_Inventory_Type`) | **FIXED & VERIFIED** |
| **BUG-05** | High | Category validation vs DB (`Name <= 100`, `Slug <= 120`) | **FIXED & VERIFIED** |
| **ISSUE-01** | Medium | Soft delete & filtered unique indexes (`Brands`, `Categories`) | **FIXED & VERIFIED** |
| **ISSUE-02** | Medium | Remove BAL reference from `Ecommerce.Infrastructure(DAL).csproj` | **FIXED & VERIFIED** |
| **ISSUE-03** | Medium | Max-length checks (`Brand.Name/Slug/LogoUrl`, `Inventory.Notes`, etc.) | **FIXED & VERIFIED** |
| **ISSUE-04** | Medium | Remove unused `OrderId` & `Amount` from `CreatePaymentDto` | **FIXED & VERIFIED** |
| **ISSUE-05** | Medium | Payment callback protection, idempotency & duplicate ref safety | **FIXED & VERIFIED** |
| **ISSUE-06a** | Medium | ASP.NET Core 8 global exception handling (`ProblemDetails`) | **FIXED & VERIFIED** |
| **ISSUE-06b** | Medium | JWT authentication setup | **BLOCKED** |
| **MINOR-01** | Minor | Standardize 6 global-namespace DAL repositories | **FIXED & VERIFIED** |
| **MINOR-02** | Minor | Align `Microsoft.Extensions.*` packages from `10.0.x` to `8.0.x` | **FIXED & VERIFIED** |
| **MINOR-03** | Minor | Remove unused `using` directives | **FIXED & VERIFIED** |
| **MINOR-04** | Minor | Remove dead method `BrandService.ValidateBrandData` | **FIXED & VERIFIED** |
| **MINOR-05** | Minor | Fix N+1 queries in `OrderService` (`GetMyOrdersAsync` / `GetAllOrdersAsync`) | **FIXED & VERIFIED** |
| **MINOR-06** | Minor | Product review purchase verification (`HasUserPurchasedProductAsync`) | **NOT APPLICABLE** |
| **MINOR-07** | Minor | Duplicate `SpecName` validation in `ProductSpecificationService.UpdateAsync` | **FIXED & VERIFIED** |
| **MINOR-08** | Minor | `PaymentService` (`Paid` check) & `CouponService` code normalization | **FIXED & VERIFIED** |

---

## Full Resolution Details & Evidence

### High Priority

#### BUG-03 — Payment Status Mapping (`FIXED & VERIFIED`)
* **Files Modified:** [`PaymentRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Repositories/PaymentRepository.cs#L157-L347)
* **Change:** Replaced `MapToOrderPaymentStatus` with `ResolveOrderPaymentStatus(newPaymentStatus, currentOrderPaymentStatus)` inside a locked transaction reading both `dbo.Payments` and `dbo.Orders` (`UPDLOCK, HOLDLOCK`). `Cancelled` preserves the existing `Orders.PaymentStatus`; `Succeeded` sets `Paid` (blocked on `Cancelled`/`Refunded`/`PartiallyRefunded` orders); `Refunded` sets `Refunded`; `Failed` sets `Failed` only if not already `Paid`/`Refunded`/`PartiallyRefunded`.
* **Evidence:** Regression tests `CancelledPreservesPendingWithoutCheckError`, `PaidOrderProtectedAgainstCancelledAndFailedDowngrade`, `RefundUpdatesOrderToRefunded`, and `FailedSetsOrderFailedAndIllegalTransitionRolledBack` all passed against `EcommerceDb`.

#### BUG-04 — Inventory Transaction Types (`FIXED & VERIFIED`)
* **Files Modified:** [`InventoryTransactionRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Repositories/InventoryTransactionRepository.cs#L100-L127)
* **Change:** Removed `"Cancellation"` from `allowedTypes`, leaving only `Restock`, `Sale`, `Return`, and `Adjustment` (matching `CK_Inventory_Type`). Confirmed `OrderRepository.CancelOrderWithTransactionAsync` continues recording `'Return'`.
* **Evidence:** Regression test `InventoryRegression :: RestockSaleReturnAdjustmentNotesLimitAndNoCancellation` verified all 4 valid types persist and `"Cancellation"` throws `ArgumentException` before SQL.

#### BUG-05 — Category Validation (`FIXED & VERIFIED`)
* **Files Modified:** [`CategoryService.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/Services/CategoryService.cs#L80-L89), [`CategoryRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Repositories/CategoryRepository.cs#L154-L220)
* **Change:** Changed `ValidateAndNormalizeName` limit from `200` to `100` (`nvarchar(100)`), added slug length `<= 120` guard (`nvarchar(120)`) in `CategoryService`, and added `NormalizeAndValidate` in `CategoryRepository`.
* **Evidence:** Regression test `CategoryRegression` verified 100-char name passes, 101-char name throws `ArgumentException`, and 121-char slug on repository throws `ArgumentException` before SQL.

---

### Medium Priority

#### ISSUE-01 — Soft Delete & Filtered Unique Indexes (`FIXED & VERIFIED`)
* **SQL Migration:** [`001_FixSoftDeleteUniqueIndexes.sql`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Database/Migrations/001_FixSoftDeleteUniqueIndexes.sql)
* **Change:** Executed safe transactional DDL dropping unfiltered `UQ_Categories_Slug`, `UQ_Brands_Name`, and `UQ_Brands_Slug`, and ensuring filtered unique indexes `UX_Categories_Slug_Active`, `UX_Brands_Name_Active`, and `UX_Brands_Slug_Active` exist with `WHERE IsDeleted = 0`.
* **Evidence:** `sys.indexes` confirmed filtered indexes active; regression tests confirmed soft-deleted Brand and Category names/slugs are reusable while active duplicates throw `InvalidOperationException`.

#### ISSUE-02 — Remove BAL Reference from DAL (`FIXED & VERIFIED`)
* **File Modified:** [`Ecommerce.Infrastructure(DAL).csproj`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Ecommerce.Infrastructure(DAL).csproj#L10-L18)
* **Change:** Removed `<ProjectReference Include="..\Ecommerce.Application(BAL)\Ecommerce.Application(BAL).csproj" />`.
* **Evidence:** Assembly reflection test `DalHasNoBalReference` confirmed `Ecommerce.Application(BAL)` is absent from DAL referenced assemblies; solution built with 0 errors.

#### ISSUE-03 — Missing Maximum Length Validation (`FIXED & VERIFIED`)
* **Files Modified:** [`BrandService.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/Services/BrandService.cs#L52-L162), [`BrandRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Repositories/BrandRepository.cs#L127-L185), [`ProductService.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/Services/ProductService.cs#L94-L235), [`ProductRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Repositories/ProductRepository.cs#L338-L355), [`InventoryTransactionRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Repositories/InventoryTransactionRepository.cs#L115-L127)
* **Related Length Hardening (Section 5):** Added `ProviderReference <= 200` / trimmed `Provider <= 50` in `PaymentRepository`, slug `<= 220` in `ProductService`, and `FullName <= 150` / `Email <= 256` / `PasswordHash <= 500` in [`UserRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Repositories/UserRepository.cs#L113-L195).
* **Evidence:** Regression tests verified Brand 100/1000 accepted and 101/1001 rejected; Inventory `Notes` 500 accepted and 501 rejected.

#### ISSUE-04 — `CreatePaymentDto` (`FIXED & VERIFIED`)
* **File Modified:** [`CreatePaymentDto.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/DTOs/Payments/CreatePaymentDto.cs)
* **Change:** Removed unused `OrderId` and `Amount` properties; retained `[Required] [MaxLength(50)] Provider`.
* **Evidence:** Reflection & DataAnnotations test `OnlyProviderExposedAndValidates` passed with `{ Provider = "Stripe" }`.

#### ISSUE-05 — Payment Callback Protection (`FIXED & VERIFIED`)
* **Files Modified:** [`PaymentRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Repositories/PaymentRepository.cs#L157-L347), [`PaymentService.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/Services/PaymentService.cs#L120-L125)
* **Change:** Atomic transaction locks both `Payments` and `Orders`; rejects `Succeeded` on `Cancelled`/`Refunded`/`PartiallyRefunded` orders; detects conflicting `ProviderReference` on duplicate callbacks; blocks duplicate `(Provider, ProviderReference)` across different payments cleanly (locked check + `SqlException 2601/2627` mapping); preserves `Paid`/`Refunded` against `Failed`/`Cancelled` callbacks; wraps rollback safely. Clarified XML docs that cryptographic webhook signature verification is not yet implemented in API.
* **Evidence:** All 7 payment regression suites + customer-method-absence reflection check passed.

#### ISSUE-06a — Global Exception Handling (`FIXED & VERIFIED`)
* **Files Added/Modified:** [`GlobalExceptionHandler.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Api/Middleware/GlobalExceptionHandler.cs), [`Program.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Api/Program.cs#L1-L34)
* **Change:** Implemented `IExceptionHandler` with `AddProblemDetails()` + early `app.UseExceptionHandler()`, mapping `ArgumentException` → 400, `UnauthorizedAccessException` → 403, `KeyNotFoundException` → 404, `InvalidOperationException` → 409, and unhandled → 500 (`_logger.LogError`, generic detail in Production).
* **Evidence:** Unit test verified all 5 status codes & Production leak guard; live HTTP probes verified `400` (`/api/Brands/-1`), `404` (`/api/Categories/999999/subcategories`), `409` (`POST /api/Categories` duplicate), and `500` in Production (`{"detail":"An unexpected error occurred."}`).

#### ISSUE-06b — JWT Authentication (`BLOCKED`)
* **Exact Reason:** No JWT configuration (`Issuer`, `Audience`, signing key), `UserSecretsId`, environment secret, or login/token endpoint (`AuthController`/`IUserService`) exists in the project. Per safety rule, no secret or credential was hardcoded or fabricated.
* **Action Required:** Configure secret management (`UserSecrets` / Key Vault / env vars), register `AddAuthentication().AddJwtBearer(...)` before `UseAuthorization()`, implement auth endpoints, and apply `[Authorize]` attributes to protected routes.

---

### Minor & Code Quality Issues

* **MINOR-01 — Repository Namespaces (`FIXED & VERIFIED`):** Added `namespace Ecommerce.Infrastructure_DAL_.Repositories;` to `InventoryTransactionRepository`, `OrderItemRepository`, `OrderRepository`, `PaymentRepository`, `ProductRepository`, and `UserRepository` (and removed redundant self-`using`s). Verified via reflection + DI.
* **MINOR-02 — Package Version Alignment (`FIXED & VERIFIED`):** Downgraded `Microsoft.Extensions.DependencyInjection` (`10.0.14` → `8.0.1`) in BAL/DAL and `Microsoft.Extensions.Configuration.Abstractions` (`10.0.13` → `8.0.0`) in DAL. Verified clean `net8.0` restore and `IntegrationAudit` build without `CS1705`/`MSB3277`.
* **MINOR-03 — Unused Using Directives (`FIXED & VERIFIED`):** Removed unused `using`s from `Program.cs` (3 directives), `Coupon.cs` (`System.Net.Sockets`), and repository self-imports.
* **MINOR-04 — Dead Code (`FIXED & VERIFIED`):** Removed unreachable `BrandService.ValidateBrandData`.
* **MINOR-05 — `OrderService` N+1 Queries (`FIXED & VERIFIED`):** Added [`IOrderItemRepository.GetByOrderIdsAsync`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Core/Interfaces/IOrderItemRepository.cs#L13) and implemented chunked `WHERE OrderId IN @OrderIds` in [`OrderItemRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Repositories/OrderItemRepository.cs#L61-L97); updated `GetMyOrdersAsync` and `GetAllOrdersAsync` in [`OrderService.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/Services/OrderService.cs#L52-L81) to batch-load items while preserving DTO shape and order sorting. Verified with multi-order regression test.
* **MINOR-06 — `HasUserPurchasedProductAsync` Ambiguity (`NOT APPLICABLE`):** Repo search confirmed no requirements doc or `ProductReviewService` dependency establishes whether reviews require a delivered purchase or are open to any user. Did not invent a restriction. Existing SQL query in `OrderItemRepository` was tested directly against `EcommerceDb` and verified (`false` before `Delivered`, `true` after `Delivered`).
* **MINOR-07 — Spec Update Duplicate Validation (`FIXED & VERIFIED`):** Added case-insensitive `SpecName` uniqueness check (excluding current `specId`) in [`ProductSpecificationService.UpdateAsync`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/Services/ProductSpecificationService.cs#L102-L120). Verified live: self-update succeeds, colliding rename throws `InvalidOperationException`.
* **MINOR-08 — Payment & Coupon Consistency (`FIXED & VERIFIED`):** Updated [`PaymentService.InitiatePaymentAsync`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/Services/PaymentService.cs#L85-L98) to compare `Orders.PaymentStatus` against `"Paid"`, `"Refunded"`, `"PartiallyRefunded"` (no `"Succeeded"` on order); updated [`CouponService.GetByCodeAsync`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/Services/CouponService.cs#L34-L43) and [`ValidateCouponAsync`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/Services/CouponService.cs#L160-L162) to normalize `.Trim().ToUpperInvariant()`. Also aligned `@SubTotal` → `@Subtotal` in [`OrderRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Repositories/OrderRepository.cs#L105).
