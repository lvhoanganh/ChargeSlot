using System;
using System.Net.Http;
using System.Threading.Tasks;
class Program
{
    static async Task Main()
    {
        var client = new HttpClient();
        var response = await client.PostAsync("https://chargeslot-api-f0b5b-exe2b0ekhp.japaneast-01.azurewebsites.net/api/Wallet/test-pay/33/2", null);
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Status: {(int)response.StatusCode}");
        Console.WriteLine($"Content: {content}");
    }
}
