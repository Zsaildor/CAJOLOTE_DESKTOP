using System;
using System.Collections.Generic;

namespace Cajolote.Models;

public class Sale
{
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    public decimal Total { get; set; }
    public bool IsSynced { get; set; } = false;
    public bool IsPaid { get; set; } = true;
    public int? NoteId { get; set; }
    public Note? Note { get; set; }
    public bool IsEdited { get; set; } = false;
    public DateTime? EditedAt { get; set; }

    public ICollection<SaleDetail> Details { get; set; } = new List<SaleDetail>();

    public int TotalProductsCount => Details?.Sum(d => d.Product != null && d.Product.IsBulk ? 1 : (int)d.Quantity) ?? 0;
}
