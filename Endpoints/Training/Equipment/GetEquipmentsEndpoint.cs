using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.Training;

namespace lionheart.Endpoints.Training.Equipment
{
    [ValidateModel]
    public class GetEquipmentsEndpoint : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<List<EquipmentDTO>>
    {
        private readonly IEquipmentService _equipmentService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetEquipmentsEndpoint(IEquipmentService equipmentService, UserManager<IdentityUser> userManager)
        {
            _equipmentService = equipmentService;
            _userManager = userManager;
        }

        [HttpGet("api/equipment/get-all")]
        [EndpointDescription("Get all available equipment.")]
        [ProducesResponseType<List<EquipmentDTO>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<List<EquipmentDTO>>> HandleAsync(CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _equipmentService.GetEquipmentsAsync(user));
        }
    }
}
