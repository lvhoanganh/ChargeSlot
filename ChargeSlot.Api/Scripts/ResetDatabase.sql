SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Xóa data theo thứ tự FK (con trước, cha sau)
DELETE FROM [ChatMessages];
DELETE FROM [ChatConversations];
DELETE FROM [DisputeEvidence];
DELETE FROM [Dispute];
DELETE FROM [LedgerEntry];
DELETE FROM [LedgerTransaction];
DELETE FROM [LoyaltyTransactions];
DELETE FROM [FavoriteStations];
DELETE FROM [Rating];
DELETE FROM [Invoice];
DELETE FROM [ChargingSession];
DELETE FROM [Payment];
DELETE FROM [BookingExtraService];
DELETE FROM [Booking];
DELETE FROM [StationPricing];
DELETE FROM [ExtraService];
DELETE FROM [ChargingSlot];
DELETE FROM [StationOperatingHours];
DELETE FROM [StationUnavailableDates];
DELETE FROM [StationImage];
DELETE FROM [ChargingStation];
DELETE FROM [Contract];
DELETE FROM [WithdrawRequest];
DELETE FROM [BankAccount];
DELETE FROM [Wallet] WHERE [SystemCode] IS NULL; -- ⚠️ GIỮ LẠI ví hệ thống (ESCROW, CLEARING, PLATFORM_REVENUE, TAX_HOLD)
-- Reset balance ví hệ thống về 0
UPDATE [Wallet] SET [AvailableBalance] = 0, [FrozenBalance] = 0 WHERE [SystemCode] IS NOT NULL;
DELETE FROM [Notification];
DELETE FROM [RefreshTokens];
DELETE FROM [UserOtp];
DELETE FROM [Driver];
DELETE FROM [Owner];
DELETE FROM [AspNetUserRoles];
DELETE FROM [AspNetUserClaims];
DELETE FROM [AspNetUserLogins];
DELETE FROM [AspNetUserTokens];
DELETE FROM [AspNetRoleClaims];
DELETE FROM [AspNetUsers];
DELETE FROM [AspNetRoles];
-- KHÔNG xóa SystemConfigs (giữ cấu hình hệ thống)
GO

-- Reset IDENTITY
DBCC CHECKIDENT ('[AspNetUsers]', RESEED, 0);
DBCC CHECKIDENT ('[AspNetRoles]', RESEED, 0);
DBCC CHECKIDENT ('[Booking]', RESEED, 0);
DBCC CHECKIDENT ('[ChargingStation]', RESEED, 0);
DBCC CHECKIDENT ('[ChargingSlot]', RESEED, 0);
DBCC CHECKIDENT ('[Payment]', RESEED, 0);
DBCC CHECKIDENT ('[ChargingSession]', RESEED, 0);
DBCC CHECKIDENT ('[Invoice]', RESEED, 0);
DBCC CHECKIDENT ('[Wallet]', RESEED, 0);
DBCC CHECKIDENT ('[LedgerTransaction]', RESEED, 0);
DBCC CHECKIDENT ('[LedgerEntry]', RESEED, 0);
DBCC CHECKIDENT ('[Notification]', RESEED, 0);
DBCC CHECKIDENT ('[Dispute]', RESEED, 0);
DBCC CHECKIDENT ('[Rating]', RESEED, 0);
DBCC CHECKIDENT ('[WithdrawRequest]', RESEED, 0);
DBCC CHECKIDENT ('[Contract]', RESEED, 0);
DBCC CHECKIDENT ('[ExtraService]', RESEED, 0);
DBCC CHECKIDENT ('[StationPricing]', RESEED, 0);
DBCC CHECKIDENT ('[StationImage]', RESEED, 0);
DBCC CHECKIDENT ('[BankAccount]', RESEED, 0);
GO

PRINT '✅ Database cleared! Restart app to re-seed demo data.';
GO
