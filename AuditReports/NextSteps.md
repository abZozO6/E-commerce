# Next Steps & Remaining Risks
**Date:** 2026-11-11  
**Project:** EcommerceSystem — ASP.NET Core 8 / Dapper / SQL Server 2022

---

## Immediate Actions Required Before Production / New Endpoints

### 1. Resolve `ISSUE-06b` (BLOCKED): Implement JWT Authentication & Authorization
- Provision secret storage (`dotnet user-secrets init` for local development, environment variables / Key Vault for staging/production) for JWT Issuer, Audience, and Signing Key.
- Add `Microsoft.AspNetCore.Authentication.JwtBearer` (`8.0.x`) and configure `AddAuthentication().AddJwtBearer(...)` before `UseAuthorization()`.
- Implement user authentication service & `AuthController` (password hashing/verification, token issuance).
- Protect write endpoints on `BrandsController` / `CategoriesController` and new customer/admin controllers with `[Authorize]` / `[Authorize(Roles = "Admin")]`, deriving `userId` strictly from authenticated `User` claims.

### 2. Implement Payment Webhook Signature Verification (`ISSUE-05` Note)
- `IPaymentService.HandleVerifiedCallbackAsync` is protected at the BAL/DAL transaction layer, but no webhook controller or cryptographic signature verifier (e.g., Stripe/provider HMAC verification) exists yet.
- Never expose `HandleVerifiedCallbackAsync` directly on a customer-callable endpoint.

### 3. Clarify Business Rule `MINOR-06`: Verified-Purchase Product Reviews
- Confirm with the product owner whether `ProductReviewService.CreateAsync` should require `IOrderItemRepository.HasUserPurchasedProductAsync(userId, productId)` (`Orders.Status = 'Delivered'`) or allow reviews from any authenticated user.

---

## Next Development Phase

1. **Review & Commit Uncommitted Workspace Changes:**  
   Stage and commit the WIP Inventory/Payment files, audit fixes, `GlobalExceptionHandler.cs`, `Database/Migrations/001_FixSoftDeleteUniqueIndexes.sql`, and updated `AuditReports/` cleanly in version control.
2. **Implement Remaining API Controllers:**  
   Add controllers for Products, ProductImages, ProductSpecifications, ProductReviews, Addresses, Carts, CartItems, Coupons, Orders, Payments, and InventoryTransactions.
3. **Add Permanent Automated Test Project (`Ecommerce.Tests`):**  
   Convert the verified integration checks from the temporary runner into an xUnit test project targeting an isolated test database, and wire `dotnet test` in CI.
4. **Optional Database Housekeeping:**  
   When convenient, remove duplicate unique indexes on `dbo.ProductImages`, `dbo.ProductReviews`, and `dbo.ProductSpecifications` listed in [DatabaseAudit.md](file:///c:/Users/DRT/Desktop/EcommerceSystem/EcommerceSystem/AuditReports/DatabaseAudit.md).
