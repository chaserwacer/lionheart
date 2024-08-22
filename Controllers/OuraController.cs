using lionheart.Model.Oura;
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
        public OuraController(IOuraService ouraService, ILogger<OuraController> logger)
        {
            _ouraService = ouraService;
            _logger = logger;
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> SyncOuraData() // DateOnly date
        {
            try
            {
                if (User.Identity?.Name is null) { throw new NullReferenceException("username/key was null"); }
                var dateNow = DateOnly.FromDateTime(DateTime.Now);
                await _ouraService.SyncOuraAPI(User.Identity.Name, dateNow, 7);
                return Ok();
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to sync oura data: {e.Message}", e);
                throw;
            }
        }

        [HttpGet("[action]")]
        public async Task<FrontendDailyOuraInfo?> GetDailyOuraInfo(DateOnly date){
            try{
                if (User.Identity?.Name is null) { throw new NullReferenceException("username/key was null"); }
                return await _ouraService.GetDailyOuraInfoAsync(User.Identity.Name, date);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to get oura info: {e.Message}", e);
                throw;
            }
        }
    }
}