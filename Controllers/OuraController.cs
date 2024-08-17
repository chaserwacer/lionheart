using lionheart.Services;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OuraController : ControllerBase
    {
        private readonly IOuraService _ouraService;
        private readonly ILogger<OuraController> _logger;
        public OuraController(IOuraService ouraService, ILogger<OuraController> logger){
            _ouraService = ouraService;
            _logger = logger;
        }

        // [HttpPost("[action]")]
        // public async Task<IActionResult> SyncOuraData([FromBody] DateOnly date )
    }
}