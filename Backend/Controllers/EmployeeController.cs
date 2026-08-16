using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ANpay.Api.Services;
using ANpay.Api.Models;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmployeeController : ControllerBase
{
    private readonly EmployeeService _employeeService;

    public EmployeeController(EmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    [HttpGet("branch/{branchId}")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> GetByBranch(Guid branchId)
    {
        var employees = await _employeeService.GetByBranchAsync(branchId);
        return Ok(employees);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var employee = await _employeeService.GetByIdAsync(id);
        if (employee == null) return NotFound();
        return Ok(employee);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeDto dto)
    {
        var employee = await _employeeService.CreateAsync(dto.UserId, dto.BranchId, dto.SubRole, dto.EmployeeCode);
        return Ok(employee);
    }

    [HttpPut("{id}/subrole")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> UpdateSubRole(Guid id, [FromBody] UpdateSubRoleDto dto)
    {
        var employee = await _employeeService.UpdateSubRoleAsync(id, dto.SubRole);
        return Ok(employee);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await _employeeService.DeactivateAsync(id);
        return NoContent();
    }
}

public class CreateEmployeeDto
{
    public string UserId { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public EmployeeSubRole SubRole { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
}

public class UpdateSubRoleDto
{
    public EmployeeSubRole SubRole { get; set; }
}
