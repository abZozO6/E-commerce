# Fix History
**Project:** EcommerceSystem — ASP.NET Core 8 / Dapper / SQL Server 2022  
**Last Updated:** 2026-11-11

---

## Prior Session Fixes (Retained)
* **FIX-01..FIX-09** *(Nov 02 – Nov 09)*: `InventoryTransactionRepository` schema columns, `ProductReview.ReviewId` rename, connection `OpenAsync()` before transactions (`ProductRepository`, `ProductImageRepository`), `BrandsController : ControllerBase`, duplicate BAL DI removal, `Orders`/`OrderItems` SELECT columns (`OrderNumber`, `PaymentStatus`, `SKU`), and `ValidateRating(byte)`.

---

## Current Cycle Fixes — Audit & Complete Issue Resolution (Nov 11)

### Initial Audit Fixes (Nov 11 — Verified Retained)
* **FIX-10 (`BUG-01`):** [`DI.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/DI.cs#L32) — Registered `ICartItemService` → `CartItemService`. (**FIXED & VERIFIED**)
* **FIX-11 (`BUG-02`):** [`OrderItemRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Repositories/OrderItemRepository.cs#L61-L74) — Removed inverted `OrderItemId <= 0` guard in `CreateAsync`. (**FIXED & VERIFIED**)
* **FIX-12:** [`CartItemRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Repositories/CartItemRepository.cs#L87-L99) — Scoped Dapper parameters in `AddOrIncrementAsync` to `{ CartId, ProductId, Quantity }` to prevent duplicate `@CartItemId` SQL Error 134. (**FIXED & VERIFIED**)
* **FIX-13:** [`IProductSpecificationRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Core/Interfaces/IProductSpecificationRepository.cs#L1-L7) — Removed duplicate `using Ecommerce.Core.Entities;` (`CS0105`). (**FIXED & VERIFIED**)

### Full Issue Resolution Fixes (Nov 11 — Verified Post-Edit)

| ID | Target Files | Change Applied | Result |
|---|---|---|---|
| **BUG-03 & ISSUE-05** | [`PaymentRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Repositories/PaymentRepository.cs), [`PaymentService.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/Services/PaymentService.cs) | Atomic locked read of Payment+Order (`UPDLOCK, HOLDLOCK`); preserved existing `Order.PaymentStatus` on `Cancelled`; protected `Paid`/`Refunded`/`PartiallyRefunded` orders from `Failed`/`Cancelled` downgrades; blocked `Succeeded` on `Cancelled`/`Refunded` orders; enforced idempotency & duplicate `(Provider, ProviderReference)` checks cleanly; added `ProviderReference <= 200` and safe rollback | **FIXED & VERIFIED** |
| **BUG-04** | [`InventoryTransactionRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Repositories/InventoryTransactionRepository.cs) | Removed `"Cancellation"` from `allowedTypes` matching `CK_Inventory_Type` (`Restock`, `Sale`, `Return`, `Adjustment`) | **FIXED & VERIFIED** |
| **BUG-05** | [`CategoryService.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/Services/CategoryService.cs), [`CategoryRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Repositories/CategoryRepository.cs) | Enforced Category `Name <= 100` and `Slug <= 120` before SQL | **FIXED & VERIFIED** |
| **ISSUE-01** | [`001_FixSoftDeleteUniqueIndexes.sql`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Database/Migrations/001_FixSoftDeleteUniqueIndexes.sql) | Dropped unfiltered `UQ_Categories_Slug`, `UQ_Brands_Name`, `UQ_Brands_Slug`; ensured filtered unique indexes `WHERE IsDeleted = 0` on `EcommerceDb` | **FIXED & VERIFIED** |
| **ISSUE-02** | [`Ecommerce.Infrastructure(DAL).csproj`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Ecommerce.Infrastructure(DAL).csproj) | Removed accidental `<ProjectReference>` to `Ecommerce.Application(BAL)` | **FIXED & VERIFIED** |
| **ISSUE-03** | [`BrandService.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/Services/BrandService.cs), [`BrandRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Repositories/BrandRepository.cs), [`ProductService.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/Services/ProductService.cs), [`ProductRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Repositories/ProductRepository.cs), `InventoryTransactionRepository.cs`, [`UserRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Infrastructure(DAL)/Repositories/UserRepository.cs) | Added max-length validation matching SQL schema (`Brand.Name <= 100`, `Brand.Slug <= 120`, `Brand.LogoUrl <= 1000`, `Notes <= 500`, `Product.Slug <= 220`, `Users.FullName <= 150`, `Email <= 256`, `PasswordHash <= 500`) | **FIXED & VERIFIED** |
| **ISSUE-04** | [`CreatePaymentDto.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/DTOs/Payments/CreatePaymentDto.cs) | Removed unused `OrderId` and `Amount` properties | **FIXED & VERIFIED** |
| **ISSUE-06a** | [`GlobalExceptionHandler.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Api/Middleware/GlobalExceptionHandler.cs), [`Program.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Api/Program.cs) | Added ASP.NET Core 8 `IExceptionHandler` + `AddProblemDetails()` + `UseExceptionHandler()` (400/403/404/409/500 with logging and Production leak guard) | **FIXED & VERIFIED** |
| **ISSUE-06b** | *(API Auth Config)* | JWT secret & auth endpoints not yet configured in project/secrets | **BLOCKED** |
| **MINOR-01** | 6 DAL repository files | Moved `InventoryTransactionRepository`, `OrderItemRepository`, `OrderRepository`, `PaymentRepository`, `ProductRepository`, `UserRepository` into `Ecommerce.Infrastructure_DAL_.Repositories` | **FIXED & VERIFIED** |
| **MINOR-02** | `Ecommerce.Application(BAL).csproj`, `Ecommerce.Infrastructure(DAL).csproj` | Aligned `Microsoft.Extensions.DependencyInjection` → `8.0.1` and `Microsoft.Extensions.Configuration.Abstractions` → `8.0.0` | **FIXED & VERIFIED** |
| **MINOR-03** | `Program.cs`, `Coupon.cs`, repository files | Removed unused/self `using` directives | **FIXED & VERIFIED** |
| **MINOR-04** | `BrandService.cs` | Removed dead method `ValidateBrandData` | **FIXED & VERIFIED** |
| **MINOR-05** | [`IOrderItemRepository.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Core/Interfaces/IOrderItemRepository.cs), `OrderItemRepository.cs`, [`OrderService.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/Services/OrderService.cs) | Eliminated N+1 queries in `GetMyOrdersAsync` & `GetAllOrdersAsync` via chunked batch query `GetByOrderIdsAsync`; also normalized `@SubTotal` → `@Subtotal` in `OrderRepository.cs` | **FIXED & VERIFIED** |
| **MINOR-06** | `ProductReviewService.cs`, `OrderItemRepository.cs` | Unconfirmed business requirement documented without inventing restriction; existing `HasUserPurchasedProductAsync` SQL verified | **NOT APPLICABLE** |
| **MINOR-07** | [`ProductSpecificationService.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/Services/ProductSpecificationService.cs) | Added duplicate `SpecName` check (excluding `specId`) in `UpdateAsync` | **FIXED & VERIFIED** |
| **MINOR-08** | `PaymentService.cs`, [`CouponService.cs`](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/Ecommerce.Application(BAL)/Services/CouponService.cs) | Fixed `Order.PaymentStatus` comparison (`"Paid"`, not `"Succeeded"`); normalized coupon code `.Trim().ToUpperInvariant()` in `GetByCodeAsync` & `ValidateCouponAsync` | **FIXED & VERIFIED** |
