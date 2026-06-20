using System.Collections.Generic;
using System.Threading.Tasks;

namespace NetURLScanner.Services
{
    public interface IBankAccountLookupService
    {
        Task<IEnumerable<BankInfo>> GetBanksAsync();
        Task<string?> GetAccountNameAsync(string bankId, string accountNumber);
    }

    public class BankInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Bin { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Logo { get; set; } = string.Empty;
    }
}
