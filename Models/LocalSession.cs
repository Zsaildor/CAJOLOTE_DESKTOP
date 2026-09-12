using System;

namespace Cajolote.Models
{
    public class LocalSession
    {
        public string LocalId { get; set; } = string.Empty; // UID de Firebase
        public string Email { get; set; } = string.Empty;
        public string IdToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpirationTime { get; set; }
    }
}
