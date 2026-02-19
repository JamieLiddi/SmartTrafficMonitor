namespace SmartTrafficMonitor.Models
{
    public class AuthSettings
    {
        public string AdminEmail { get; set; } = "";
        public string AdminPassword { get; set; } = "";
        public string AdminTotpSecretBase32 { get; set; } = "";
    }
}
