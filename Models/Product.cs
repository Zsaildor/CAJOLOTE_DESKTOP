namespace Cajolote.Models;

public class Product
{
    public int Id { get; set; }
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public bool IsQuickProduct { get; set; } = false;
    public bool IsBulk { get; set; } = false;
    public string? IconKey { get; set; }
    public string CategoryName => Category?.Name ?? "Sin categoría";
}
