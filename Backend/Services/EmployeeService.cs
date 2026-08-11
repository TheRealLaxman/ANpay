using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Exceptions;

namespace ANpay.Api.Services;

public class EmployeeService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EmployeeService> _logger;

    public EmployeeService(ApplicationDbContext context, ILogger<EmployeeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<EmployeeProfile>> GetByBranchAsync(Guid branchId)
    {
        return await _context.EmployeeProfiles
            .Include(ep => ep.User)
            .Include(ep => ep.Branch)
            .Where(ep => ep.BranchId == branchId && ep.IsActive)
            .OrderBy(ep => ep.User.LastName)
            .ToListAsync();
    }

    public async Task<EmployeeProfile> GetByIdAsync(Guid id)
    {
        var profile = await _context.EmployeeProfiles
            .Include(ep => ep.User)
            .Include(ep => ep.Branch)
            .FirstOrDefaultAsync(ep => ep.Id == id);
        if (profile == null) throw new NotFoundException("Employee profile not found");
        return profile;
    }

    public async Task<EmployeeProfile> CreateAsync(string userId, Guid branchId, EmployeeSubRole subRole, string employeeCode)
    {
        var user = await _context.Users.FindAsync(userId)
            ?? throw new NotFoundException("User not found");

        if (await _context.EmployeeProfiles.AnyAsync(ep => ep.UserId == userId))
            throw new ValidationException("User already has an employee profile");

        if (await _context.EmployeeProfiles.AnyAsync(ep => ep.EmployeeCode == employeeCode))
            throw new ValidationException("Employee code already exists");

        var branch = await _context.Branches.FindAsync(branchId)
            ?? throw new NotFoundException("Branch not found");

        var profile = new EmployeeProfile
        {
            UserId = userId,
            BranchId = branchId,
            SubRole = subRole,
            EmployeeCode = employeeCode,
            IsActive = true
        };

        user.BranchId = branchId;
        user.Role = AppUserRole.Official;

        _context.EmployeeProfiles.Add(profile);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Employee created: {UserId} as {SubRole} in branch {BranchId}", userId, subRole, branchId);
        return profile;
    }

    public async Task<EmployeeProfile> UpdateSubRoleAsync(Guid id, EmployeeSubRole subRole)
    {
        var profile = await _context.EmployeeProfiles.FindAsync(id)
            ?? throw new NotFoundException("Employee profile not found");

        profile.SubRole = subRole;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Employee sub-role updated: {Id} to {SubRole}", id, subRole);
        return profile;
    }

    public async Task DeactivateAsync(Guid id)
    {
        var profile = await _context.EmployeeProfiles.FindAsync(id)
            ?? throw new NotFoundException("Employee profile not found");

        profile.IsActive = false;

        var user = await _context.Users.FindAsync(profile.UserId);
        if (user != null)
        {
            user.BranchId = null;
            user.Role = AppUserRole.Customer;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Employee deactivated: {Id}", id);
    }

    public async Task<bool> HasSubRolePermissionAsync(string userId, EmployeeSubRole requiredSubRole)
    {
        var profile = await _context.EmployeeProfiles
            .FirstOrDefaultAsync(ep => ep.UserId == userId && ep.IsActive);

        if (profile == null) return false;

        return profile.SubRole switch
        {
            EmployeeSubRole.Manager => true,
            EmployeeSubRole.Operations => requiredSubRole != EmployeeSubRole.Manager,
            EmployeeSubRole.CustomerService => requiredSubRole == EmployeeSubRole.CustomerService || requiredSubRole == EmployeeSubRole.Teller,
            EmployeeSubRole.Teller => requiredSubRole == EmployeeSubRole.Teller,
            _ => false
        };
    }
}
