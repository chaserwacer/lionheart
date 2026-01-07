using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.Training;

namespace lionheart.Endpoints.Training.Equipment
{
    [ValidateModel]
    public class CreateEquipmentEndpoint : EndpointBaseAsync
        .WithRequest<CreateEquipmentRequest>
        .WithActionResult<EquipmentDTO>
    {
        private readonly IEquipmentService _equipmentService;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateEquipmentEndpoint(IEquipmentService equipmentService, UserManager<IdentityUser> userManager)
        {
            _equipmentService = equipmentService;
            _userManager = userManager;
        }

        [HttpPost("api/equipment/create")]
        [EndpointDescription("Create a new equipment.")]
        [ProducesResponseType<EquipmentDTO>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public override async Task<ActionResult<EquipmentDTO>> HandleAsync([FromBody] CreateEquipmentRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _equipmentService.CreateEquipmentAsync(user, request));
        }
    }
}
