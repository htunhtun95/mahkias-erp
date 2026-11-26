using Mahkias.Api.Security;
using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects;
using Microsoft.AspNetCore.Mvc;

namespace Mahkias.Api.Controllers
{
    [ApiController]
    [RequireApiKey]
    public class PartController : Controller
    {
        private readonly IUow _uow;

        public PartController(IUow uow)
        {
            _uow = uow;
        }

        [HttpGet("/api/parts/suggestions")]
        public async Task<IActionResult> Suggestions([FromQuery] string q)
        {
            var matches = await _uow.Parts.SearchAsync(q);
            return Ok(matches.Select(match => new
            {
                partId = match.PartId,
                partNo = match.PartNo,
                description = match.Description
            }));
        }

        [HttpGet("/api/parts")]
        public async Task<IActionResult> List([FromQuery] string search)
        {
            var parts = await _uow.Parts.ListAsync(search);
            return Ok(parts.Select(ToResponse));
        }

        [HttpGet("/api/parts/{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var part = await _uow.Parts.GetAsync(id);
            if (part == null)
            {
                return NotFound();
            }

            return Ok(ToResponse(part));
        }

        [HttpPost("/api/parts")]
        public async Task<IActionResult> Create([FromBody] PartWriteRequest request)
        {
            var saved = await _uow.Parts.CreateAsync(request?.PartNo, ToWrites(request));
            if (!string.IsNullOrWhiteSpace(saved.Error))
            {
                return BadRequest(new { message = saved.Error });
            }

            return Ok(ToResponse(saved.Part));
        }

        [HttpPut("/api/parts/{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] PartWriteRequest request)
        {
            var saved = await _uow.Parts.UpdateAsync(id, request?.PartNo, ToWrites(request));
            if (saved.Part == null && saved.Error == "Part was not found.")
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(saved.Error))
            {
                return BadRequest(new { message = saved.Error });
            }

            return Ok(ToResponse(saved.Part));
        }

        [HttpGet("/api/parts/{id:int}/alternates")]
        public async Task<IActionResult> Alternates(int id)
        {
            var part = await _uow.Parts.GetAsync(id);
            if (part == null)
            {
                return NotFound();
            }

            var alternates = await _uow.Parts.ListAlternatesAsync(id);
            return Ok(alternates.Select(ToAlternate));
        }

        [HttpPost("/api/parts/{id:int}/alternates")]
        public async Task<IActionResult> AddAlternate(int id, [FromBody] PartAlternateRequest request)
        {
            var saved = await _uow.Parts.SaveAlternateAsync(id, null, request?.PartNo, request?.Description, request?.PartId);
            if (!string.IsNullOrWhiteSpace(saved.Error))
            {
                return saved.Error == "Part was not found." ? NotFound() : BadRequest(new { message = saved.Error });
            }

            return Ok(saved.Alternates.Select(ToAlternate));
        }

        [HttpPut("/api/parts/{id:int}/alternates/{linkId:int}")]
        public async Task<IActionResult> UpdateAlternate(int id, int linkId, [FromBody] PartAlternateRequest request)
        {
            var saved = await _uow.Parts.SaveAlternateAsync(id, linkId, request?.PartNo, request?.Description, request?.PartId);
            if (!string.IsNullOrWhiteSpace(saved.Error))
            {
                return saved.Error == "Part was not found." || saved.Error == "Alternate part was not found."
                    ? NotFound()
                    : BadRequest(new { message = saved.Error });
            }

            return Ok(saved.Alternates.Select(ToAlternate));
        }

        [HttpDelete("/api/parts/{id:int}/alternates/{linkId:int}")]
        public async Task<IActionResult> RemoveAlternate(int id, int linkId)
        {
            var removed = await _uow.Parts.RemoveAlternateAsync(id, linkId);
            if (!removed)
            {
                return NotFound();
            }

            return Ok();
        }

        [HttpGet("/api/parts/{id:int}/quotations")]
        public async Task<IActionResult> Quotations(int id)
        {
            var part = await _uow.Parts.GetAsync(id);
            if (part == null)
            {
                return NotFound();
            }

            var lines = await _uow.Parts.ListQuotationsAsync(id);
            return Ok(lines.Select(line => new
            {
                quotationItemId = line.QuotationItemId,
                quotationId = line.QuotationId,
                code = line.Code,
                supplierId = line.SupplierId,
                supplierName = line.SupplierName,
                supplierReference = line.SupplierReference,
                partNo = line.PartNo,
                quantity = line.Quantity,
                unit = line.Unit,
                condition = line.Condition,
                unitPrice = line.UnitPrice,
                totalPrice = line.TotalPrice,
                currencyCode = line.CurrencyCode,
                currencySymbol = line.CurrencySymbol,
                quotedDate = line.QuotedDate,
                validUntil = line.ValidUntil,
                match = line.Match,
                projects = line.Projects.Select(project => new
                {
                    id = project.Id,
                    label = project.Label,
                }),
            }));
        }

        [HttpDelete("/api/parts/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _uow.Parts.DeleteAsync(id);
            if (!deleted)
            {
                return NotFound();
            }

            return Ok();
        }

        private static List<PartDescriptionWrite> ToWrites(PartWriteRequest request)
        {
            return (request?.Descriptions ?? new List<PartDescriptionRequest>())
                .Where(item => item != null)
                .Select(item => new PartDescriptionWrite
                {
                    Id = item.Id > 0 ? item.Id : null,
                    Description = item.Description,
                })
                .ToList();
        }

        private static object ToAlternate(PartAlternate alternate)
        {
            return new
            {
                id = alternate.Id,
                partId = alternate.PartId,
                partNo = alternate.PartNo,
                descriptions = alternate.Descriptions.Select(item => new
                {
                    id = item.Id,
                    description = item.Description,
                }),
            };
        }

        private static object ToResponse(PartDetail part)
        {
            return new
            {
                id = part.Id,
                partNo = part.PartNo,
                createdAt = part.CreatedAt,
                descriptions = part.Descriptions.Select(item => new
                {
                    id = item.Id,
                    description = item.Description,
                }),
            };
        }

        public class PartWriteRequest
        {
            public string PartNo { get; set; }
            public List<PartDescriptionRequest> Descriptions { get; set; } = new();
        }

        public class PartDescriptionRequest
        {
            public int? Id { get; set; }
            public string Description { get; set; }
        }

        public class PartAlternateRequest
        {
            public string PartNo { get; set; }
            public string Description { get; set; }
            public int? PartId { get; set; }
        }
    }
}