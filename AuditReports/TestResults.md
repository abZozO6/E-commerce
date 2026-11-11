# Regression & Verification Test Results Report
**Date:** 2026-11-11  
**Target Environment:** Local `.NET 8` + `EcommerceDb` (Microsoft SQL Server 2022)  
**Important Note:** Tests ran after the final edit to the codebase. All temporary test rows created in `EcommerceDb` were removed afterward; initial DB state (`Brands=1`, `Categories=1`, `sysdiagrams=1`, others `0`) was verified restored.

---

## 1. Restore, Build & DI Verification

| Step | Command / Mechanism | Result |
|---|---|---|
| NuGet Restore | `dotnet restore EcommerceSystem.sln --disable-build-servers -m:1` | ✅ **Succeeded** (`8.0.0` / `8.0.1` packages aligned) |
| Clean Solution Build | `dotnet build EcommerceSystem.sln --no-restore --no-incremental --disable-build-servers -p:UseSharedCompilation=false -m:1` | ✅ **Succeeded — 0 Warning(s), 0 Error(s)** |
| Regression Runner Build | `dotnet build IntegrationAudit.csproj --no-restore` | ✅ **Succeeded — 0 Error(s)** (`MSB3277` / `CS1705` version conflict warnings eliminated) |
| DI Validation | `BuildServiceProvider(ValidateScopes = true, ValidateOnBuild = true)` | ✅ **29 / 29 resolved** (16 DAL + 13 BAL) |

---

## 2. Regression Suite Results (`IntegrationAudit.exe`)

**Total Executed:** 20 checks | **Passed:** 20 | **Failed:** 0

| # | Category | Test Case | Result | Observed Evidence |
|---|---|---|---|---|
| 1 | Architecture | `DalHasNoBalReference` | ✅ PASS | Referenced assemblies contain `Ecommerce.Core`, not `Ecommerce.Application(BAL)` |
| 2 | Architecture | `AllSixRepoNamespacesStandardized` | ✅ PASS | All 6 repos in `Ecommerce.Infrastructure_DAL_.Repositories` |
| 3 | DI | `ResolveAll29ServicesAndRepos` | ✅ PASS | All 16 DAL + 13 BAL registrations resolved under scoped validation |
| 4 | Payment DTO | `OnlyProviderExposedAndValidates` | ✅ PASS | `CreatePaymentDto` exposes only `[Provider]` and passes `Validator` |
| 5 | Payment Security | `NoCustomerDirectStatusUpdate` | ✅ PASS | `IPaymentService` exposes no customer-facing status update method |
| 6 | Middleware | `AllStatusMappingsAndProdLeakGuard` | ✅ PASS | Mapped `400/403/404/409/500`; Production `500` suppressed internal exception message |
| 7 | Brands | `LengthsActiveDupAndSoftDeleteReuse` | ✅ PASS | 100-char name & 1000-char URL accepted; 101 & 1001 rejected; active dup rejected; soft-deleted name/slug reused (`UX_Brands_*_Active`) |
| 8 | Categories | `100Accepted101AndSlug121RejectedActiveDupAndSoftDeleteReuse` | ✅ PASS | 100-char name accepted; 101 name & 121 slug rejected; active dup rejected; soft-deleted slug reused (`UX_Categories_Slug_Active`) |
| 9 | Inventory | `RestockSaleReturnAdjustmentNotesLimitAndNoCancellation` | ✅ PASS | `Restock`, `Sale`, `Return`, `Adjustment` succeeded; 500 notes ok; 501 notes rejected; `"Cancellation"` rejected before SQL |
| 10 | Product Specs | `DuplicateSpecNameRejectedOnUpdateAndSelfUpdateWorks` | ✅ PASS | Case-insensitive duplicate `SpecName` update threw `InvalidOperationException`; self-update returned `true` |
| 11 | Coupons | `ConsistentUpperNormalizationOnGetAndValidate` | ✅ PASS | Mixed-case/padded code normalized to `SAVE20_...` consistently across Create, GetByCode, Validate |
| 12 | Orders Batch | `GetMyOrdersAndGetAllOrdersBatchItems` | ✅ PASS | 3 orders returned with exact grouped items (`1, 2, 1`) in `CreatedAt DESC` order via batch query |
| 13 | Orders & Purchase | `CancelRestoresStockAndHasUserPurchasedQueryWorks` | ✅ PASS | Unpaid cancel restored stock (`15→18`) + logged `Return`; `HasUserPurchasedProductAsync`: `False → True` on `Delivered` |
| 14 | Payment | `CancelledPreservesPendingWithoutCheckError` | ✅ PASS | Callback `Cancelled` kept `Order.PaymentStatus = 'Pending'` without `CK_Orders_PaymentStatus` failure |
| 15 | Payment | `PaidOrderProtectedAgainstCancelledAndFailedDowngrade` | ✅ PASS | On `Paid` order, late `Cancelled` and `Failed` callbacks kept `Order.PaymentStatus = 'Paid'` |
| 16 | Payment | `DuplicateCallbackIdempotentAndConflictingRefBlocked` | ✅ PASS | Identical retry returned `true`; conflicting `ProviderReference` threw `InvalidOperationException` |
| 17 | Payment | `DuplicateProviderReferenceBlockedAndRolledBackAtomically` | ✅ PASS | Reuse of existing `(Provider, ProviderReference)` rejected cleanly; payment remained `Pending`, `PaidAt = null` |
| 18 | Payment | `RefundUpdatesOrderToRefunded` | ✅ PASS | `Succeeded → Refunded` updated `Order.PaymentStatus = 'Refunded'` |
| 19 | Payment | `SuccessfulCallbackBlockedOnCancelledOrder` | ✅ PASS | Late `Succeeded` callback on `Cancelled` order threw `InvalidOperationException`; payment rolled back to `Pending` |
| 20 | Payment | `FailedSetsOrderFailedAndIllegalTransitionRolledBack` | ✅ PASS | `Failed` updated `Order.PaymentStatus = 'Failed'`; invalid `Failed → Succeeded` rejected and rolled back |

---

## 3. Live API HTTP Endpoint & Middleware Probes (`Ecommerce.Api.exe`)

Executed post-final-edit against live Kestrel HTTP server bound on loopback with real `EcommerceDb` (Development) and simulated unreachable DB (Production):

| Mode | Request | Observed Status & Content-Type | Observed Body Verification |
|---|---|---|---|
| Development | `GET /swagger/index.html` | `200 OK` (`text/html`) | Swagger UI loaded cleanly ✅ |
| Development | `GET /api/Brands` | `200 OK` (`application/json`) | Returned existing brand record ✅ |
| Development | `GET /api/Brands/-1` | `400 Bad Request` (`application/problem+json`) | `{"title":"Bad Request","status":400,"detail":"Invalid brand ID.","instance":"/api/Brands/-1"}` ✅ |
| Development | `GET /api/Categories/1` | `200 OK` (`application/json`) | Returned existing `Electronics` category ✅ |
| Development | `GET /api/Categories/999999/subcategories` | `404 Not Found` (`application/problem+json`) | `{"title":"Not Found","status":404,"detail":"Parent category was not found.",...}` ✅ |
| Development | `POST /api/Categories` `{"name":"Electronics"}` | `409 Conflict` (`application/problem+json`) | `{"title":"Conflict","status":409,"detail":"A category with the same slug already exists.",...}` ✅ |
| Production | `GET /api/Brands` *(unreachable SQL server)* | `500 Internal Server Error` (`application/problem+json`) | Logged `SqlException 258` internally; returned `{"title":"Internal Server Error","status":500,"detail":"An unexpected error occurred.","instance":"/api/Brands"}` with **no stack trace or connection detail** ✅ |

*Note on JWT probes:* **NOT RUN / BLOCKED** because JWT configuration & signing secret are not available (`ISSUE-06b`).

---

## 4. SQL Server Constraint & Cleanup Verification

Post-regression query against `EcommerceDb` confirmed:
- `Brands`: `UX_Brands_Name_Active` & `UX_Brands_Slug_Active` (`WHERE IsDeleted = 0`), row count = `1`
- `Categories`: `UX_Categories_Slug_Active` (`WHERE IsDeleted = 0`), row count = `1`
- All 13 other user tables: row count = `0` (`sysdiagrams = 1`)
