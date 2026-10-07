using Mahkias.Api.Helpers;
using Mahkias.Api.Modules.Projects;
using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mahkias.Api.Controllers
{
    [ApiController]
    [Route("projects")]
    public class ProjectController : Controller
    {
        private readonly ILogger<ProjectController> _logger;
        private readonly MahkiasDbContext _context;
        private readonly IUow _uow;

        public ProjectController(ILogger<ProjectController> logger, MahkiasDbContext context, IUow uow)
        {
            _logger = logger;
            _context = context;
            _uow = uow;
        }

        [HttpGet("/api/projects")]
        public async Task<IActionResult> List(
            [FromQuery] string search,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string sortBy = "createdAt",
            [FromQuery] string sortDirection = "desc")
        {
            var result = await _uow.Projects.SearchAsync(new SearchProjectArgs
            {
                SearchTerm = search,
                PageNumber = pageNumber,
                PageSize = pageSize,
                SortBy = sortBy,
                SortDirection = sortDirection
            });

            var projects = result.Items.Select(p => new ProjectResponse
            {
                Id = p.Id,
                Name = p.Name,
                Reference = p.Reference,
                Description = p.Description,
                CreatedAt = p.CreatedAt,
                ModifiedAt = p.ModifiedAt
            });

            return Ok(projects);
        }

        [HttpPost("/api/projects")]
        public async Task<IActionResult> Create([FromBody] CreateProjectRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Name is required.");
            }

            var id = await _uow.Projects.AddAsync(new UpsertProjectArgs
            {
                Name = request.Name.Trim(),
                Reference = request.Reference?.Trim(),
                Description = request.Description?.Trim()
            });

            if (id < 0)
            {
                return BadRequest(new { message = "Reference code already exists." });
            }

            return Ok(await ProjectResponseAsync(id));
        }

        [HttpPut("/api/projects/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateProjectRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Name is required.");
            }

            var status = await _uow.Projects.UpdateAsync(id, new UpsertProjectArgs
            {
                Name = request.Name.Trim(),
                Reference = request.Reference?.Trim(),
                Description = request.Description?.Trim()
            });

            if (status < 0)
            {
                return BadRequest(new { message = "Reference code already exists." });
            }

            if (status == 0)
            {
                return NotFound();
            }

            return Ok(await ProjectResponseAsync(id));
        }

        [HttpDelete("/api/projects/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await ProjectExists(id))
            {
                return NotFound();
            }

            var deleted = await _uow.Projects.DeleteAsync(id);
            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }

        [HttpGet("/api/projects/{id}")]
        public async Task<IActionResult> Get(int id, [FromQuery] string sortBy = "createdAt", [FromQuery] string sortDirection = "desc")
        {
            var project = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (project == null)
            {
                return NotFound();
            }

            var activities = await _uow.Activities.GetByProjectAsync(id, sortBy, sortDirection);
            return Ok(new ProjectResponse
            {
                Id = project.Id,
                Name = project.Name,
                Reference = project.Reference,
                Description = project.Description,
                CreatedAt = project.CreatedAt,
                ModifiedAt = project.ModifiedAt,
                Activities = activities.Select(ToResponse).ToList()
            });
        }

        [HttpPost("/api/projects/{id}/activities")]
        public async Task<IActionResult> AddActivity(int id, [FromBody] CreateActivityRequest request)
        {
            if (!await ProjectExists(id))
            {
                return NotFound();
            }

            if (request == null || string.IsNullOrWhiteSpace(request.PartNo))
            {
                return BadRequest("Part no is required.");
            }

            var createdId = await _uow.Activities.CreateAsync(ToArgs(id, request));
            if (createdId <= 0)
            {
                return BadRequest("Part no is required.");
            }

            var created = (await _uow.Activities.GetByProjectAsync(id)).FirstOrDefault(activity => activity.Id == createdId);
            return Ok(created == null ? new ActivityResponse { Id = createdId, ProjectId = id, PartNo = request.PartNo } : ToResponse(created));
        }

        [HttpPost("/api/projects/{id}/activities/bulk")]
        public async Task<IActionResult> BulkActivities(int id, [FromBody] BulkActivitiesRequest request)
        {
            if (!await ProjectExists(id))
            {
                return NotFound();
            }

            var inserted = await _uow.Activities.BulkInsertAsync(id, (request?.Activities ?? new List<CreateActivityRequest>()).Select(activity => ToArgs(id, activity)));
            return Ok(new { inserted });
        }

        [HttpPost("/api/projects/{id}/activities/excel/mapping")]
        public async Task<IActionResult> ExcelMapping(int id, IFormFile file)
        {
            if (!await ProjectExists(id))
            {
                return NotFound();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("Choose an Excel or CSV file.");
            }

            using var stream = file.OpenReadStream();
            var sheet = ActivityExcelReader.Read(stream, file.FileName);
            return Ok(new
            {
                columns = sheet.Columns.Select(column => new { column.Letter, column.Header }),
                rowCount = sheet.Rows.Count,
                suggested = new
                {
                    dsnNo = Suggest(sheet.Columns, "dsnno", "dsn", "dsnnumber"),
                    partNo = Suggest(sheet.Columns, "partno", "partnumber", "part"),
                    description = Suggest(sheet.Columns, "description", "desc"),
                    type = Suggest(sheet.Columns, "type", "activitytype"),
                    quantity = Suggest(sheet.Columns, "quantity", "qty"),
                    budget = Suggest(sheet.Columns, "budget", "price", "cost")
                }
            });
        }

        [HttpPost("/api/projects/{id}/activities/excel/validate")]
        public Task<IActionResult> ExcelValidate(int id, [FromForm] ActivityExcelUploadForm form)
        {
            return ExcelImport(id, form, commit: false);
        }

        [HttpPost("/api/projects/{id}/activities/excel/upload")]
        public Task<IActionResult> ExcelUpload(int id, [FromForm] ActivityExcelUploadForm form)
        {
            return ExcelImport(id, form, commit: true);
        }

        private async Task<IActionResult> ExcelImport(int id, ActivityExcelUploadForm form, bool commit)
        {
            if (!await ProjectExists(id))
            {
                return NotFound();
            }

            if (form?.File == null || form.File.Length == 0)
            {
                return BadRequest("Choose an Excel or CSV file.");
            }

            var missingConfiguration = new List<string>();
            if (string.IsNullOrWhiteSpace(form.DsnNoColumn) && string.IsNullOrWhiteSpace(form.DefaultDsnNo))
            {
                missingConfiguration.Add("DSN No");
            }

            if (string.IsNullOrWhiteSpace(form.PartNoColumn) && string.IsNullOrWhiteSpace(form.DefaultPartNo))
            {
                missingConfiguration.Add("Part No");
            }

            if (string.IsNullOrWhiteSpace(form.TypeColumn) && string.IsNullOrWhiteSpace(form.DefaultTypeId))
            {
                missingConfiguration.Add("Type");
            }

            if (missingConfiguration.Count > 0)
            {
                return BadRequest(new
                {
                    message = "Please select a Column Index or enter a Default Value for this required field.",
                    fields = missingConfiguration
                });
            }

            ActivitySheet sheet;
            using (var stream = form.File.OpenReadStream())
            {
                sheet = ActivityExcelReader.Read(stream, form.File.FileName);
            }

            if (sheet.Rows.Count == 0)
            {
                return BadRequest(new { message = "The file has no data rows." });
            }

            var types = await _context.ActivityTypes.AsNoTracking().ToListAsync();
            var plan = ActivityImportValidator.Plan(sheet, form, types);
            if (plan.Errors.Count == 0)
            {
                plan.Errors.AddRange(await _uow.Activities.ValidateImportAsync(id, plan.Rows));
            }

            if (plan.Errors.Count > 0)
            {
                var payload = new
                {
                    valid = false,
                    ready = 0,
                    rowCount = sheet.Rows.Count,
                    errorCount = plan.Errors.Count,
                    message = "Validation failed.",
                    errors = plan.Errors.Select(issue => new { row = issue.Row, field = issue.Field, message = issue.Message })
                };
                return commit ? BadRequest(payload) : Ok(payload);
            }

            if (!commit)
            {
                return Ok(new
                {
                    valid = true,
                    ready = plan.Rows.Count,
                    rowCount = sheet.Rows.Count,
                    errorCount = 0,
                    errors = Array.Empty<object>()
                });
            }

            var inserted = await _uow.Activities.BulkInsertAsync(id, plan.Rows.Select(row => new CreateActivityArgs
            {
                ProjectId = id,
                PartNo = row.PartNo,
                Budget = row.Budget,
                Description = row.Description,
                DSNNo = row.DSNNo,
                Quantity = row.Quantity,
                TypeId = row.TypeId
            }));
            return Ok(new { inserted });
        }

        private async Task<bool> ProjectExists(int id)
        {
            return await _context.Projects.AnyAsync(project => project.Id == id);
        }

        private static CreateActivityArgs ToArgs(int projectId, CreateActivityRequest request)
        {
            return new CreateActivityArgs
            {
                ProjectId = projectId,
                PartNo = request?.PartNo,
                Budget = request?.Budget,
                Description = request?.Description,
                DSNNo = request?.DSNNo,
                Quantity = request?.Quantity,
                TypeId = request?.TypeId > 0 ? request.TypeId : null
            };
        }

        private static ActivityResponse ToResponse(Mahkias.Core.Modules.Projects.Data.Result.ProjectActivityResult activity)
        {
            return new ActivityResponse
            {
                Id = activity.Id,
                ProjectId = activity.ProjectId,
                PartNo = activity.PartNo,
                MainPartNo = activity.MainPartNo,
                AlternativePartNos = activity.AlternativePartNos ?? new List<string>(),
                Budget = activity.Budget,
                Description = activity.Description,
                DSNNo = activity.DSNNo,
                Quantity = activity.Quantity,
                TypeId = activity.TypeId,
                Type = string.IsNullOrWhiteSpace(activity.Type) ? null : activity.Type,
                CreatedAt = activity.CreatedAt,
                ModifiedAt = activity.ModifiedAt,
                ActivityGroupId = activity.ActivityGroupId,
                ActivityGroupName = string.IsNullOrWhiteSpace(activity.ActivityGroupName) ? null : activity.ActivityGroupName,
                QuoteReceived = activity.QuoteReceived,
                DeliveryProgress = activity.DeliveryProgress
            };
        }

        private async Task<ProjectResponse> ProjectResponseAsync(int id)
        {
            var project = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            return new ProjectResponse
            {
                Id = id,
                Name = project?.Name,
                Reference = project?.Reference,
                Description = project?.Description,
                CreatedAt = project?.CreatedAt,
                ModifiedAt = project?.ModifiedAt
            };
        }

        private static string Suggest(IReadOnlyList<ActivityColumn> columns, params string[] names)
        {
            return columns.FirstOrDefault(column => names.Contains(Normalize(column.Header)))?.Letter;
        }

        private static string Normalize(string value)
        {
            return new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        [HttpGet("/api/lookup/activity-types")]
        public async Task<IActionResult> ActivityTypes()
        {
            var types = await _context.ActivityTypes
                .OrderBy(t => t.Id)
                .Select(t => new ActivityTypeResponse
                {
                    Id = t.Id,
                    Name = t.Name,
                    Slug = t.Slug
                })
                .ToListAsync();

            return Ok(types);
        }
    }
}
