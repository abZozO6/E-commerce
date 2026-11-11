using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;
using System.Data;

namespace Ecommerce.Infrastructure_DAL_.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    private static readonly HashSet<string> AllowedTransactionTypes =
    [
        "Restock",
        "Sale",
        "Return",
        "Adjustment"
    ];

    public ProductRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Product?> GetByIdAsync(long productId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT ProductId, CategoryId, BrandId, Name, Slug, SKU, Description,
                   Price, StockQuantity, WarrantyMonths, IsActive, CreatedAt, RowVersion
            FROM dbo.Products
            WHERE ProductId = @ProductId";

        return await connection.QuerySingleOrDefaultAsync<Product>(
            sql,
            new { ProductId = productId });
    }

    public async Task<Product?> GetBySlugAsync(string slug)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT ProductId, CategoryId, BrandId, Name, Slug, SKU, Description,
                   Price, StockQuantity, WarrantyMonths, IsActive, CreatedAt, RowVersion
            FROM dbo.Products
            WHERE Slug = @Slug AND IsActive = 1";

        return await connection.QuerySingleOrDefaultAsync<Product>(
            sql,
            new { Slug = slug });
    }

    /// <summary>
    /// Retrieves all products, ordered from newest to oldest.
    /// </summary>
    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT ProductId, CategoryId, BrandId, Name, Slug, SKU, Description,
                   Price, StockQuantity, WarrantyMonths, IsActive, CreatedAt, RowVersion
            FROM dbo.Products
            ORDER BY CreatedAt DESC";

        return await connection.QueryAsync<Product>(sql);
    }

    /// <summary>
    /// Retrieves active products matching the supplied optional filters.
    /// </summary>
    public async Task<IEnumerable<Product>> GetActiveFilteredAsync(
        long? categoryId = null,
        long? brandId = null,
        string? searchTerm = null,
        decimal? minPrice = null,
        decimal? maxPrice = null)
    {
        if (minPrice < 0 || maxPrice < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minPrice),
                "Price filters cannot be negative.");
        }

        if (minPrice.HasValue &&
            maxPrice.HasValue &&
            minPrice > maxPrice)
        {
            throw new ArgumentException(
                "Minimum price cannot exceed maximum price.");
        }

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT ProductId, CategoryId, BrandId, Name, Slug, SKU, Description,
                   Price, StockQuantity, WarrantyMonths, IsActive, CreatedAt, RowVersion
            FROM dbo.Products
            WHERE IsActive = 1
              AND (@CategoryId IS NULL OR CategoryId = @CategoryId)
              AND (@BrandId IS NULL OR BrandId = @BrandId)
              AND (
                    @SearchTerm IS NULL
                    OR Name LIKE '%' + @SearchTerm + '%'
                    OR SKU = @SearchTerm
                  )
              AND (@MinPrice IS NULL OR Price >= @MinPrice)
              AND (@MaxPrice IS NULL OR Price <= @MaxPrice)
            ORDER BY CreatedAt DESC";

        return await connection.QueryAsync<Product>(sql, new
        {
            CategoryId = categoryId,
            BrandId = brandId,
            SearchTerm = string.IsNullOrWhiteSpace(searchTerm)
                ? null
                : searchTerm.Trim(),
            MinPrice = minPrice,
            MaxPrice = maxPrice
        });
    }

    /// <summary>
    /// Checks whether another product already uses the supplied slug.
    /// </summary>
    public async Task<bool> ExistsBySlugAsync(string slug,long? excludeProductId = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM dbo.Products
                WHERE Slug = @Slug
                  AND (@ExcludeId IS NULL OR ProductId <> @ExcludeId)
            ) THEN 1 ELSE 0 END";

        return await connection.ExecuteScalarAsync<bool>(sql, new
        {
            Slug = slug.Trim(),
            ExcludeId = excludeProductId
        });
    }

    /// <summary>
    /// Creates a product, generates its SKU, and records its initial stock.
    /// Both database operations are committed atomically.
    /// </summary>
    public async Task<long> CreateAsync(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (product.StockQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(product.StockQuantity),
                "Stock quantity cannot be negative.");
        }

        using var connection = _connectionFactory.CreateConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Generate a unique SKU using SQL Server Sequence.
            const string getNextSkuSql = @"
                SELECT NEXT VALUE FOR dbo.ProductSkuSequence;";

            long skuNumber = await connection.ExecuteScalarAsync<long>(
                getNextSkuSql,
                transaction: transaction);

            product.SKU = $"PRD-{skuNumber:D6}";

            const string insertProductSql = @"
                INSERT INTO dbo.Products (
                    CategoryId,
                    BrandId,
                    Name,
                    Slug,
                    SKU,
                    Description,
                    Price,
                    StockQuantity,
                    WarrantyMonths,
                    IsActive
                )
                VALUES (
                    @CategoryId,
                    @BrandId,
                    @Name,
                    @Slug,
                    @SKU,
                    @Description,
                    @Price,
                    @StockQuantity,
                    @WarrantyMonths,
                    @IsActive
                );

                SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

            long productId = await connection.ExecuteScalarAsync<long>(
                insertProductSql,
                product,
                transaction);

            // Record the initial stock in the inventory history.
            if (product.StockQuantity > 0)
            {
                const string insertInventorySql = @"
                    INSERT INTO dbo.InventoryTransactions (
                        ProductId,
                        QuantityChange,
                        TransactionType,
                        ReferenceOrderId,
                        Notes
                    )
                    VALUES (
                        @ProductId,
                        @QuantityChange,
                        'Restock',
                        NULL,
                        @Notes
                    );";

                await connection.ExecuteAsync(insertInventorySql, new
                {
                    ProductId = productId,
                    QuantityChange = product.StockQuantity,
                    Notes = "Opening stock when creating the product"
                }, transaction);
            }

            transaction.Commit();

            return productId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Updates product details only when the supplied RowVersion matches
    /// the current database version.
    /// SKU is immutable and is not updated.
    /// </summary>
    public async Task<bool> UpdateWithRowVersionAsync(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (product.RowVersion is null || product.RowVersion.Length != 8)
        {
            throw new ArgumentException(
                "A valid RowVersion is required.",
                nameof(product));
        }

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE dbo.Products
            SET CategoryId = @CategoryId,
                BrandId = @BrandId,
                Name = @Name,
                Slug = @Slug,
                Description = @Description,
                Price = @Price,
                WarrantyMonths = @WarrantyMonths,
                IsActive = @IsActive
            WHERE ProductId = @ProductId
              AND RowVersion = @RowVersion";

        int rowsAffected = await connection.ExecuteAsync(sql, product);

        return rowsAffected > 0;
    }

    /// <summary>
    /// Adjusts product stock and records the corresponding inventory
    /// transaction atomically.
    /// </summary>
    public async Task<bool> AdjustStockWithTransactionAsync(
        long productId,
        int quantityChange,
        string transactionType,
        string? notes,
        byte[] rowVersion)
    {
        if (quantityChange == 0)
        {
            throw new ArgumentException(
                "Quantity change cannot be zero.",
                nameof(quantityChange));
        }

        if (rowVersion is null || rowVersion.Length != 8)
        {
            throw new ArgumentException(
                "A valid RowVersion is required.",
                nameof(rowVersion));
        }

        if (!AllowedTransactionTypes.Contains(transactionType))
        {
            throw new ArgumentException(
                "Invalid inventory transaction type.",
                nameof(transactionType));
        }

        if (transactionType == "Restock" && quantityChange < 0)
        {
            throw new ArgumentException(
                "Restock transactions must increase stock.",
                nameof(quantityChange));
        }

        if (transactionType == "Sale" && quantityChange > 0)
        {
            throw new ArgumentException(
                "Sale transactions must decrease stock.",
                nameof(quantityChange));
        }

        if (transactionType == "Return" && quantityChange < 0)
        {
            throw new ArgumentException(
                "Return transactions must increase stock.",
                nameof(quantityChange));
        }

        notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        if (notes?.Length > 500)
        {
            throw new ArgumentException(
                "Notes cannot exceed 500 characters.",
                nameof(notes));
        }

        using var connection = _connectionFactory.CreateConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string updateStockSql = @"
                UPDATE dbo.Products
                SET StockQuantity = StockQuantity + @QuantityChange
                WHERE ProductId = @ProductId
                  AND RowVersion = @RowVersion
                  AND (StockQuantity + @QuantityChange) >= 0;";

            int updatedRows = await connection.ExecuteAsync(
                updateStockSql,
                new
                {
                    ProductId = productId,
                    QuantityChange = quantityChange,
                    RowVersion = rowVersion
                },
                transaction);

            if (updatedRows == 0)
            {
                transaction.Rollback();
                return false;
            }

            const string insertInventoryTxSql = @"
                INSERT INTO dbo.InventoryTransactions (
                    ProductId,
                    QuantityChange,
                    TransactionType,
                    ReferenceOrderId,
                    Notes
                )
                VALUES (
                    @ProductId,
                    @QuantityChange,
                    @TransactionType,
                    NULL,
                    @Notes
                );";

            await connection.ExecuteAsync(insertInventoryTxSql, new
            {
                ProductId = productId,
                QuantityChange = quantityChange,
                TransactionType = transactionType,
                Notes = notes
            }, transaction);

            transaction.Commit();

            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Activates or deactivates a product.
    /// </summary>
    public async Task<bool> SetActiveStatusAsync(long productId,bool isActive)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE dbo.Products
            SET IsActive = @IsActive
            WHERE ProductId = @ProductId";

        int rowsAffected = await connection.ExecuteAsync(sql, new
        {
            ProductId = productId,
            IsActive = isActive
        });

        return rowsAffected > 0;
    }


    public async Task<ProductDetails?> GetDetailsByIdAsync(long productId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
        SELECT
            p.ProductId,
            p.BrandId,
            p.CategoryId,
            p.Name,
            p.Slug,
            p.SKU,
            p.Description,
            p.Price,
            p.StockQuantity,
            p.WarrantyMonths,
            p.IsActive,
            p.CreatedAt,
            p.RowVersion,

            c.Name AS CategoryName,
            b.Name AS BrandName,
            pi.ImageUrl

        FROM dbo.Products p

        INNER JOIN dbo.Categories c
            ON p.CategoryId = c.CategoryId

        LEFT JOIN dbo.Brands b
            ON p.BrandId = b.BrandId

        LEFT JOIN dbo.ProductImages pi
            ON p.ProductId = pi.ProductId
            AND pi.IsPrimary = 1

        WHERE p.ProductId = @ProductId;
        """;

        return await connection.QuerySingleOrDefaultAsync<ProductDetails>(
            sql,
            new { ProductId = productId });
    }
}