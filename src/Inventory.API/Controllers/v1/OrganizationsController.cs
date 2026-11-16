using Inventory.Application.Organizations;
using Inventory.Domain.Identity;
using Inventory.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.API.Controllers.v1;

[ApiController]
[Route("api/v1/organizations")]
[Authorize]
public sealed class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _organizationService;

    public OrganizationsController(IOrganizationService organizationService)
    {
        _organizationService = organizationService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<OrganizationDto>> CreateOrganization(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _organizationService.CreateOrganizationAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("current")]
    [HasPermission(Permissions.Organization.MembersRead)]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganizationDto>> GetCurrentOrganization(CancellationToken cancellationToken)
    {
        var result = await _organizationService.GetCurrentOrganizationAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut("current")]
    [HasPermission(Permissions.Organization.Manage)]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganizationDto>> UpdateCurrentOrganization(
        [FromBody] UpdateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _organizationService.UpdateCurrentOrganizationAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("members")]
    [HasPermission(Permissions.Organization.MembersRead)]
    [ProducesResponseType(typeof(IReadOnlyList<OrganizationMemberDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrganizationMemberDto>>> GetMembers(CancellationToken cancellationToken)
    {
        var result = await _organizationService.GetMembersAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("members/invite")]
    [HasPermission(Permissions.Organization.MembersManage)]
    [ProducesResponseType(typeof(OrganizationMemberDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<OrganizationMemberDto>> InviteMember(
        [FromBody] InviteMemberRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _organizationService.InviteOrAddMemberAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("members/{memberId:guid}/role")]
    [HasPermission(Permissions.Organization.MembersManage)]
    [ProducesResponseType(typeof(OrganizationMemberDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganizationMemberDto>> UpdateMemberRole(
        Guid memberId,
        [FromBody] UpdateMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _organizationService.UpdateMemberRoleAsync(memberId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("members/{memberId:guid}")]
    [HasPermission(Permissions.Organization.MembersManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeactivateMember(Guid memberId, CancellationToken cancellationToken)
    {
        await _organizationService.DeactivateMemberAsync(memberId, cancellationToken);
        return NoContent();
    }

    [HttpGet("roles")]
    [HasPermission(Permissions.Organization.MembersRead)]
    [ProducesResponseType(typeof(IReadOnlyList<OrganizationRoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrganizationRoleDto>>> GetRoles(CancellationToken cancellationToken)
    {
        var result = await _organizationService.GetRolesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("roles")]
    [HasPermission(Permissions.Organization.RolesManage)]
    [ProducesResponseType(typeof(OrganizationRoleDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<OrganizationRoleDto>> CreateRole(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _organizationService.CreateRoleAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("roles/{roleId:guid}")]
    [HasPermission(Permissions.Organization.RolesManage)]
    [ProducesResponseType(typeof(OrganizationRoleDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganizationRoleDto>> UpdateRole(
        Guid roleId,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _organizationService.UpdateRoleAsync(roleId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("permissions")]
    [HasPermission(Permissions.Organization.MembersRead)]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> GetPermissions()
    {
        return Ok(_organizationService.GetAvailablePermissions());
    }
}
