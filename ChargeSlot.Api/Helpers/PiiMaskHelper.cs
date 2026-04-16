namespace ChargeSlot.Api.Helpers
{
    /// <summary>
    /// Helper che giấu (mask) dữ liệu cá nhân nhạy cảm (PII) trên API response.
    /// </summary>
    public static class PiiMaskHelper
    {
        /// <summary>
        /// Mask số CCCD/CMND: 037203002631 → 0372****2631
        /// </summary>
        public static string? MaskIdCard(string? idCard)
        {
            if (string.IsNullOrEmpty(idCard)) return idCard;
            if (idCard.Length <= 6) return new string('*', idCard.Length);
            return idCard[..4] + new string('*', idCard.Length - 8) + idCard[^4..];
        }

        /// <summary>
        /// Mask mã số thuế: 0312345678 → 031****678
        /// </summary>
        public static string? MaskTaxCode(string? taxCode)
        {
            if (string.IsNullOrEmpty(taxCode)) return taxCode;
            if (taxCode == "N/A") return taxCode;
            if (taxCode.Length <= 5) return new string('*', taxCode.Length);
            return taxCode[..3] + new string('*', taxCode.Length - 6) + taxCode[^3..];
        }
    }
}
