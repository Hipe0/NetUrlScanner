using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Helpers;
using NetURLScanner.Models;

namespace NetURLScanner.Services;

public class DomainVoteStats
{
    public string NormalizedDomain { get; set; } = string.Empty;
    public int UpVotes { get; set; }
    public int DownVotes { get; set; }
    public int NetScore => UpVotes - DownVotes;
    public int? CurrentUserVote { get; set; }
}

public class DomainVoteService
{
    private readonly ApplicationDbContext _context;

    public DomainVoteService(ApplicationDbContext context)
    {
        _context = context;
    }

    public string? ResolveDomain(string urlOrDomain) =>
        DomainHelper.NormalizeDomain(urlOrDomain);

    public async Task<DomainVoteStats> GetStatsAsync(string urlOrDomain, int? userId = null)
    {
        var domain = ResolveDomain(urlOrDomain) ?? urlOrDomain.Trim().ToLowerInvariant();
        var votes = await _context.DomainVotes
            .AsNoTracking()
            .Where(x => x.NormalizedDomain == domain)
            .ToListAsync();

        int? userVote = null;
        if (userId != null)
            userVote = votes.FirstOrDefault(x => x.UserId == userId)?.Vote;

        return new DomainVoteStats
        {
            NormalizedDomain = domain,
            UpVotes = votes.Count(x => x.Vote > 0),
            DownVotes = votes.Count(x => x.Vote < 0),
            CurrentUserVote = userVote
        };
    }

    public async Task<DomainVoteStats> VoteAsync(int userId, string urlOrDomain, int vote)
    {
        if (vote is not (1 or -1))
            throw new ArgumentException("Vote phải là +1 hoặc -1.");

        var domain = ResolveDomain(urlOrDomain)
            ?? throw new ArgumentException("Domain không hợp lệ.");

        var existing = await _context.DomainVotes
            .FirstOrDefaultAsync(x => x.UserId == userId && x.NormalizedDomain == domain);

        if (existing == null)
        {
            _context.DomainVotes.Add(new DomainVote
            {
                UserId = userId,
                NormalizedDomain = domain,
                Vote = vote,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            });
        }
        else if (existing.Vote == vote)
        {
            _context.DomainVotes.Remove(existing);
        }
        else
        {
            existing.Vote = vote;
            existing.UpdatedAt = DateTime.Now;
        }

        await _context.SaveChangesAsync();
        return await GetStatsAsync(domain, userId);
    }
}
