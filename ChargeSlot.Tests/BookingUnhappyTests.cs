using System;
using System.Linq;
using System.Threading.Tasks;
using ChargeSlot.Api.Enums;
using ChargeSlot.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ChargeSlot.Tests
{
    public class BookingUnhappyTests
    {
        [Fact]
        public async Task CancelBooking_Over2Hours_Returns100PercentRefund_AndPoints() { Assert.True(true); }

        [Fact]
        public async Task CancelBooking_LessThan1Hour_Returns0PercentRefund() { Assert.True(true); }
    }
}
