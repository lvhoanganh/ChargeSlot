using ChargeSlot.Api.DTOs.BankAccount;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Helpers;

namespace ChargeSlot.Api.Services.Implementation
{
    public class BankAccountService : IBankAccountService
    {
        private readonly IBankAccountRepository _bankAccountRepo;
        private readonly IWithdrawRequestRepository _withdrawRequestRepo;
        private readonly IUnitOfWork _unitOfWork;

        public BankAccountService(
            IBankAccountRepository bankAccountRepo,
            IWithdrawRequestRepository withdrawRequestRepo,
            IUnitOfWork unitOfWork)
        {
            _bankAccountRepo = bankAccountRepo;
            _withdrawRequestRepo = withdrawRequestRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task<List<BankAccountDto>> GetMyBankAccountsAsync(int userId)
        {
            var list = await _bankAccountRepo.GetByUserIdAsync(userId);
            return list.OrderByDescending(b => b.IsDefault)
                       .ThenByDescending(b => b.CreatedAt)
                       .Select(b => new BankAccountDto
                       {
                           Id = b.Id,
                           BankName = b.BankName,
                           BankAccountNumber = b.BankAccountNumber,
                           BankAccountHolder = b.BankAccountHolder,
                           IsDefault = b.IsDefault,
                           CreatedAt = b.CreatedAt
                       }).ToList();
        }

        public async Task<BankAccountDto> CreateAsync(int userId, CreateBankAccountDto dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                if (dto.IsDefault)
                {
                    var existing = await _bankAccountRepo.GetByUserIdAsync(userId);
                    foreach (var iter in existing.Where(b => b.IsDefault))
                    {
                        iter.IsDefault = false;
                        _bankAccountRepo.Update(iter);
                    }
                }

                var account = new BankAccount
                {
                    UserId = userId,
                    BankName = dto.BankName,
                    BankAccountNumber = dto.BankAccountNumber,
                    BankAccountHolder = dto.BankAccountHolder,
                    IsDefault = dto.IsDefault,
                    CreatedAt = DateTimeHelper.VietnamNow()
                };
                
                _bankAccountRepo.Add(account);
                await _unitOfWork.CompleteAsync();
                await transaction.CommitAsync();

                return new BankAccountDto
                {
                    Id = account.Id,
                    BankName = account.BankName,
                    BankAccountNumber = account.BankAccountNumber,
                    BankAccountHolder = account.BankAccountHolder,
                    IsDefault = account.IsDefault,
                    CreatedAt = account.CreatedAt
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task SetDefaultAsync(int id, int userId)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var account = await _bankAccountRepo.GetByIdAsync(id);
                if (account == null || account.UserId != userId) 
                    throw new KeyNotFoundException("Tài khoản ngân hàng không tồn tại.");

                var all = await _bankAccountRepo.GetByUserIdAsync(userId);
                foreach (var b in all)
                {
                    b.IsDefault = false;
                    _bankAccountRepo.Update(b);
                }
                
                account.IsDefault = true;
                _bankAccountRepo.Update(account);
                
                await _unitOfWork.CompleteAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteAsync(int id, int userId)
        {
            var account = await _bankAccountRepo.GetByIdAsync(id);
            if (account == null || account.UserId != userId) 
                throw new KeyNotFoundException("Tài khoản ngân hàng không tồn tại.");

            var hasPending = await _withdrawRequestRepo.HasPendingRequestsAsync(userId, account.BankAccountNumber, account.BankName);

            if (hasPending)
                throw new InvalidOperationException("Không thể xóa — đang có yêu cầu rút tiền liên quan chưa hoàn tất.");

            _bankAccountRepo.Remove(account);
            await _unitOfWork.CompleteAsync();
        }
    }
}
