namespace ChargeSlot.Api.Enums
{
    /// <summary>
    /// Trạng thái hợp đồng hợp tác giữa ChargeSlot và Owner.
    /// </summary>
    public enum ContractStatus
    {
        Pending = 0,     // Chờ Owner ký
        Signed = 1,      // Đã ký
        Expired = 2,     // Hết hạn
        Terminated = 3   // Admin chấm dứt
    }
}
