using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Exceptions;

namespace ANpay.Api.Services;

public class KycService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<KycService> _logger;

    public KycService(ApplicationDbContext context, ILogger<KycService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<KycProfile?> GetByUserIdAsync(string userId)
    {
        return await _context.KycProfiles
            .Include(kp => kp.Documents)
            .FirstOrDefaultAsync(kp => kp.UserId == userId);
    }

    public async Task<KycProfile> SubmitAsync(string userId, KycSubmitDto dto)
    {
        var profile = await _context.KycProfiles.FirstOrDefaultAsync(kp => kp.UserId == userId);

        if (profile == null)
        {
            profile = new KycProfile
            {
                UserId = userId,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                DateOfBirth = dto.DateOfBirth,
                Address = dto.Address,
                City = dto.City,
                Country = dto.Country,
                Phone = dto.Phone,
                IdType = dto.IdType,
                IdNumber = dto.IdNumber,
                Status = KycStatus.Submitted,
                Level = KycLevel.Basic,
                SubmittedAt = DateTime.UtcNow
            };
            _context.KycProfiles.Add(profile);
        }
        else
        {
            profile.FirstName = dto.FirstName;
            profile.LastName = dto.LastName;
            profile.DateOfBirth = dto.DateOfBirth;
            profile.Address = dto.Address;
            profile.City = dto.City;
            profile.Country = dto.Country;
            profile.Phone = dto.Phone;
            profile.IdType = dto.IdType;
            profile.IdNumber = dto.IdNumber;
            profile.Status = KycStatus.Submitted;
            profile.SubmittedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("KYC submitted for user {UserId}", userId);
        return profile;
    }

    public async Task<KycProfile> ReviewAsync(Guid profileId, bool approve, string notes)
    {
        var profile = await _context.KycProfiles.FindAsync(profileId)
            ?? throw new NotFoundException("KYC profile not found");

        profile.Status = approve ? KycStatus.Approved : KycStatus.Rejected;
        profile.ReviewedAt = DateTime.UtcNow;
        profile.ReviewNotes = notes;
        if (approve) profile.Level = KycLevel.Full;

        await _context.SaveChangesAsync();
        _logger.LogInformation("KYC {Status} for profile {ProfileId}", profile.Status, profileId);
        return profile;
    }

    public async Task<List<KycProfile>> GetPendingAsync()
    {
        return await _context.KycProfiles
            .Include(kp => kp.User)
            .Where(kp => kp.Status == KycStatus.Submitted)
            .OrderBy(kp => kp.SubmittedAt)
            .ToListAsync();
    }
}

public class KycSubmitDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string IdType { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
}
