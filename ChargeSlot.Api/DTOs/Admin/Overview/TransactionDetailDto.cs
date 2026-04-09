using System;
using System.Collections.Generic;

namespace ChargeSlot.Api.DTOs.Admin.Overview
{
    public class TransactionDetailDto
    {
        public long Id { get; set; }
        public string ReferenceType { get; set; } = null!;
        public long? ReferenceId { get; set; }
        public string Memo { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        public List<LedgerEntryDetailDto> Entries { get; set; } = new List<LedgerEntryDetailDto>();
    }

    public class LedgerEntryDetailDto
    {
        public int WalletId { get; set; }
        public string WalletType { get; set; } = null!;
        public string OwnerName { get; set; } = null!;
        public string Direction { get; set; } = null!;
        public decimal Amount { get; set; }
    }
}
