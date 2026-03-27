namespace ChargeSlot.Api.Services.Interfaces
{
    public interface ISmsService
    {
        Task SendSmsAsync(string phoneNumber, string content);
    }
}
