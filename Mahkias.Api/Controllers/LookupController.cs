using Mahkias.Api.Security;
using Mahkias.Core.Data;
using Microsoft.AspNetCore.Mvc;

namespace Mahkias.Api.Controllers
{
    [ApiController]
    [RequireApiKey]
    public class LookupController : Controller
    {
        private readonly IUow _uow;

        public LookupController(IUow uow)
        {
            _uow = uow;
        }

        [HttpGet("/api/units")]
        public async Task<IActionResult> Units()
        {
            return Ok(await _uow.Lookups.ListUnitsAsync());
        }

        [HttpGet("/api/conditions")]
        public async Task<IActionResult> Conditions()
        {
            return Ok(await _uow.Lookups.ListConditionsAsync());
        }
    }
}
