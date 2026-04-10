using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Wallet
{
    /// <summary>User báo chưa nhận được tiền rút.</summary>
    public class ReportWithdrawIssueDto
    {
        [Required, MaxLength(2000)]
        public string IssueNote { get; set; } = null!;
    }
}
