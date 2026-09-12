using System;

namespace Cajolote.Models;

public class StoreProfile
{
    public int Id { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Rfc { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public bool IsDirty { get; set; } = false;
}
