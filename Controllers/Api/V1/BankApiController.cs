using Microsoft.AspNetCore.Mvc;
using NetURLScanner.Services;
using System.Threading.Tasks;

namespace NetURLScanner.Controllers.Api.V1
{
    [Route("api/v1/banks")]
    [ApiController]
    public class BankApiController : ControllerBase
    {
        private readonly IBankAccountLookupService _bankService;

        public BankApiController(IBankAccountLookupService bankService)
        {
            _bankService = bankService;
        }

        [HttpGet]
        public async Task<IActionResult> GetBanks()
        {
            var banks = await _bankService.GetBanksAsync();
            return Ok(banks);
        }

        [HttpGet("lookup")]
        public async Task<IActionResult> LookupAccount([FromQuery] string bankId, [FromQuery] string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(bankId) || string.IsNullOrWhiteSpace(accountNumber))
            {
                return BadRequest(new { message = "Thiếu thông tin ngân hàng hoặc số tài khoản." });
            }

            var accountName = await _bankService.GetAccountNameAsync(bankId, accountNumber);
            if (string.IsNullOrEmpty(accountName))
            {
                return NotFound(new { message = "Không tìm thấy thông tin chủ tài khoản." });
            }

            return Ok(new { accountName });
        }
    }
}
