namespace ChargeSlot.Api.DTOs.Dispute
{
    /// <summary>
    /// Thông tin số lượt thua dispute trong tháng hiện tại
    /// và số lượt còn lại trước khi bị ban.
    /// </summary>
    public class DisputeStrikeStatusDto
    {
        /// <summary>Số lượt thua dispute trong tháng hiện tại.</summary>
        public int LoseCountThisMonth { get; set; }

        /// <summary>Ngưỡng thua tối đa trước khi bị ban (Driver: 3, Station: 5).</summary>
        public int BanThreshold { get; set; }

        /// <summary>Số lượt còn lại trước khi bị ban. 0 = sắp/đã bị ban.</summary>
        public int RemainingBeforeBan { get; set; }

        /// <summary>Số lần đã bị ban trước đó (BanCount).</summary>
        public int BanCount { get; set; }

        /// <summary>Đang bị ban hay không.</summary>
        public bool IsBanned { get; set; }

        /// <summary>Ngày hết ban (null = vĩnh viễn hoặc chưa bị ban).</summary>
        public DateTime? BannedUntil { get; set; }
    }
}
