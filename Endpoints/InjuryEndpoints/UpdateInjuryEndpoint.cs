using Ardalis.ApiEndpoints;
using lionheart.Model.DTOs;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.InjuryEndpoints;

[ValidateModel]
public class UpdateInjuryEndpoint : EndpointBaseAsync
	.WithRequest<UpdateInjuryRequest>
	.WithActionResult<InjuryDTO>
{
	private readonly IInjuryService _injuryService;
	private readonly UserManager<IdentityUser> _userManager;

	public UpdateInjuryEndpoint(IInjuryService injuryService, UserManager<IdentityUser> userManager)
	{
		_injuryService = injuryService;
		_userManager = userManager;
	}

	[HttpPut("api/injury/update")]
	[ProducesResponseType(typeof(InjuryDTO), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public override async Task<ActionResult<InjuryDTO>> HandleAsync(
		[FromBody] UpdateInjuryRequest request,
		CancellationToken cancellationToken = default)
	{
		var user = await _userManager.GetUserAsync(User);
		if (user is null) return Unauthorized();

		var result = await _injuryService.UpdateInjuryAsync(user, request);
		return this.ToActionResult(result);
	}
}
