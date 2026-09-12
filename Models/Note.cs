using System;

namespace Cajolote.Models;

public class Note
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public bool IsPaid { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? PaidAt { get; set; }

    public decimal Faltante => Amount - PaidAmount;

    public System.Collections.Generic.ICollection<Sale> Sales { get; set; } = new System.Collections.Generic.List<Sale>();
    public System.Collections.Generic.ICollection<ManualDebt> ManualDebts { get; set; } = new System.Collections.Generic.List<ManualDebt>();
    public System.Collections.Generic.ICollection<HistoricalSale> HistoricalSales { get; set; } = new System.Collections.Generic.List<HistoricalSale>();

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public System.Collections.Generic.IEnumerable<object> AllDetails
    {
        get
        {
            var list = new System.Collections.Generic.List<object>();
            if (ManualDebts != null)
            {
                list.AddRange(ManualDebts);
            }
            if (Sales != null)
            {
                list.AddRange(Sales);
            }
            var sorted = new System.Collections.Generic.List<object>(
                System.Linq.Enumerable.OrderByDescending(list, item => {
                    if (item is ManualDebt md) return md.Date;
                    if (item is Sale s) return s.Date;
                    return DateTime.MinValue;
                })
            );
            sorted.Add(new PaymentFormItem(this));
            return sorted;
        }
    }
}

public class PaymentFormItem
{
    public Note Note { get; }
    public PaymentFormItem(Note note)
    {
        Note = note;
    }
}
