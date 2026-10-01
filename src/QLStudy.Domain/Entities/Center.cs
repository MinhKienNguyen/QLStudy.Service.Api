namespace QLStudy.Domain.Entities
{
    public class Center
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? Domain { get; set; }
        public string Status { get; set; } = "Active";
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAccountName { get; set; }
        public string? PaymentQrCode { get; set; }
        public string? PayOSClientId { get; set; }
        public string? PayOSApiKey { get; set; }
        public string? PayOSChecksumKey { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
