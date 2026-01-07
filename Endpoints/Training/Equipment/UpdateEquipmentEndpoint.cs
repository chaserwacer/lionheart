using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.Training;

namespace lionheart.Endpoints.Training.Equipment
{
    [ValidateModel]
    public class UpdateEquipmentEndpoint : EndpointBaseAsync
        .WithRequest<UpdateEquipmentRequest>
        .WithActionResult<EquipmentDTO>
    {
        private readonly IEquipmentService _equipmentService;
        private readonly UserManager<IdentityUser> _userManager;

        public UpdateEquipmentEndpoint(IEquipmentService equipmentService, UserManager<IdentityUser> userManager)
        {
            _equipmentService = equipmentService;
            _userManager = userManager;
        }

        [HttpPost("api/equipment/update")]
        [EndpointDescription("Update an existing equipment.")]
        [ProducesResponseType<EquipmentDTO>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public override async Task<ActionResult<EquipmentDTO>> HandleAsync([FromBody] UpdateEquipmentRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _equipmentService.UpdateEquipmentAsync(user, request));
        }
    }
}
