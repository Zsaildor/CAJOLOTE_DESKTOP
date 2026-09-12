namespace Cajolote.Models;

public class SaleDetail
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal => Quantity * UnitPrice;
    public string QuantityDisplay => Product != null && Product.IsBulk ? $"{Quantity:0.000} kg" : $"{Quantity:0}";
}
