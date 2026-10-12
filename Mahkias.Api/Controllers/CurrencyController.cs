using Mahkias.Core.Data;
using Microsoft.AspNetCore.Mvc;

namespace Mahkias.Api.Controllers
{
    [ApiController]
    public class CurrencyController : Controller
    {
        private readonly IUow _uow;

        public CurrencyController(IUow uow)
        {
            _uow = uow;
        }

        [HttpGet("/api/currencies")]
        public async Task<IActionResult> List()
        {
            return Ok(await _uow.Currencies.ListActiveAsync());
        }
    }
}
