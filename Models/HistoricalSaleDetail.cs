using System.ComponentModel.DataAnnotations.Schema;

namespace Cajolote.Models;

public class HistoricalSaleDetail
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }
    
    public int HistoricalSaleId { get; set; }
    public HistoricalSale HistoricalSale { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal => Quantity * UnitPrice;
    public string QuantityDisplay => Product != null && Product.IsBulk ? $"{Quantity:0.000} kg" : $"{Quantity:0}";
}
