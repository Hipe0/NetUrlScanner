using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetURLScanner.Services;

namespace NetURLScanner.Controllers;

[Authorize]
[Route("Domain")]
public class DomainVoteController : AppControllerBase
{
    private readonly DomainVoteService _voteService;

    public DomainVoteController(DomainVoteService voteService)
    {
        _voteService = voteService;
    }

    [HttpGet("Stats")]
    [AllowAnonymous]
    public async Task<IActionResult> Stats(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return Json(new { success = false, message = "Thiếu domain." });

        var stats = await _voteService.GetStatsAsync(domain, GetCurrentUserId());
        return Json(new
        {
            success = true,
            data = new
            {
                domain = stats.NormalizedDomain,
                upVotes = stats.UpVotes,
                downVotes = stats.DownVotes,
                netScore = stats.NetScore,
                userVote = stats.CurrentUserVote
            }
        });
    }

    [HttpPost("Vote")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vote(string domain, int vote)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Json(new { success = false, message = "Vui lòng đăng nhập để bình chọn." });

        try
        {
            var stats = await _voteService.VoteAsync(userId.Value, domain, vote);
            return Json(new
            {
                success = true,
                message = "Đã ghi nhận bình chọn.",
                data = new
                {
                    domain = stats.NormalizedDomain,
                    upVotes = stats.UpVotes,
                    downVotes = stats.DownVotes,
                    netScore = stats.NetScore,
                    userVote = stats.CurrentUserVote
                }
            });
        }
        catch (ArgumentException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}
