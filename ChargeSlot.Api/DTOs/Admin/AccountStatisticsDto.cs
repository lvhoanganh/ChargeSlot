namespace ChargeSlot.Api.DTOs.Admin
{
    public class AccountStatisticsDto
    {
        public int TotalAccounts { get; set; }
        public int ActiveAccounts { get; set; }
        public int BannedAccounts { get; set; }
        public int TotalOwners { get; set; }
        public int TotalDrivers { get; set; }
        public int TotalAdmins { get; set; }
    }
}
