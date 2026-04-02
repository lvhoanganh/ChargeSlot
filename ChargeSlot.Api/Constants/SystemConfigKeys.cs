namespace ChargeSlot.Api.Constants
{
    public static class SystemConfigKeys
    {
        public const string RefundPolicy100_Hrs = "RefundPolicy100_Hrs";
        public const string RefundPolicy50_Hrs = "RefundPolicy50_Hrs";
        public const string Payment_Expiry_Minutes = "Payment_Expiry_Minutes";
        public const string CheckIn_Window_Minutes = "CheckIn_Window_Minutes";
        public const string NoShow_Grace_Minutes = "NoShow_Grace_Minutes";
        public const string Slot_Buffer_Minutes = "Slot_Buffer_Minutes";
        
        public const string VAT_Rate = "VAT_Rate";
        public const string Platform_Fee_Rate = "Platform_Fee_Rate";
        public const string Loyalty_Earn_Rate = "Loyalty_Earn_Rate";
        
        public const string Dispute_Limit_Per_Month = "Dispute_Limit_Per_Month";
        public const string Dispute_OwnerEvidence_Hours = "Dispute_OwnerEvidence_Hours";
        public const string Dispute_AdminReview_Hours = "Dispute_AdminReview_Hours";
        
        public const string Ban_Duration_Days_Permanent = "Ban_Duration_Days_Permanent";
        public const string Ban_Duration_Days_FirstOffense = "Ban_Duration_Days_FirstOffense";
        
        public const string OTP_Expiry_Minutes = "OTP_Expiry_Minutes";
    }
}
