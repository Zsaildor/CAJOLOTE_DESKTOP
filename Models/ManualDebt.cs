using System;

namespace Cajolote.Models;

public class ManualDebt
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Details { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Now;

    public int NoteId { get; set; }
    public Note Note { get; set; } = null!;
}
