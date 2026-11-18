using Mahkias.Api.Security;
using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data.Args;
using Microsoft.AspNetCore.Mvc;

namespace Mahkias.Api.Controllers
{
    [ApiController]
    [RequireApiKey]
    public class DashboardController : Controller
    {
        private readonly IUow _uow;

        public DashboardController(IUow uow)
        {
            _uow = uow;
        }

        [HttpGet("/api/dashboard")]
        public async Task<IActionResult> Get(
            [FromQuery] string dataset,
            [FromQuery] string status,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _uow.Dashboard.SearchAsync(new DashboardSearchArgs
            {
                Dataset = dataset,
                Status = status,
                From = from,
                To = to,
                PageNumber = pageNumber,
                PageSize = pageSize,
            });
            return Ok(result);
        }
    }
}
