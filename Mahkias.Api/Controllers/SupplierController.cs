using Mahkias.Api.Modules.Projects;
using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data.Args;
using Microsoft.AspNetCore.Mvc;

namespace Mahkias.Api.Controllers
{
    [ApiController]
    public class SupplierController : Controller
    {
        private readonly IUow _uow;

        public SupplierController(IUow uow)
        {
            _uow = uow;
        }

        [HttpGet("/api/suppliers")]
        public async Task<IActionResult> List([FromQuery] string search)
        {
            return Ok(await _uow.Suppliers.SearchAsync(search));
        }

        [HttpGet("/api/suppliers/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var supplier = await _uow.Suppliers.GetAsync(id);
            if (supplier == null)
            {
                return NotFound();
            }

            return Ok(supplier);
        }

        [HttpPost("/api/suppliers")]
        public async Task<IActionResult> Create([FromBody] CreateSupplierRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "Supplier name is required." });
            }

            var id = await _uow.Suppliers.CreateAsync(new CreateSupplierArgs
            {
                Name = request.Name.Trim(),
                Reference = request.Reference?.Trim(),
            });
            var created = await _uow.Suppliers.GetAsync(id);
            return Ok(created);
        }

        [HttpPut("/api/suppliers/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateSupplierRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "Supplier name is required." });
            }

            var updated = await _uow.Suppliers.UpdateAsync(id, new CreateSupplierArgs
            {
                Name = request.Name.Trim(),
                Reference = request.Reference?.Trim(),
            });
            if (!updated)
            {
                return NotFound();
            }

            return Ok(await _uow.Suppliers.GetAsync(id));
        }

        [HttpDelete("/api/suppliers/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _uow.Suppliers.DeleteAsync(id);
            if (!result.Found)
            {
                return NotFound();
            }

            if (!result.Deleted)
            {
                return Conflict(new { message = "This supplier cannot be deleted because it has quotations." });
            }

            return NoContent();
        }
    }
}
