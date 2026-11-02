namespace Ecommerce.Application_BAL_.DTOs.Brands;

public class BrandDto
{
    public long BrandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; }
}