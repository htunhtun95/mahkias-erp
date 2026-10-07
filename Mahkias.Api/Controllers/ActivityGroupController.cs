using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data.Result;
using Mahkias.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mahkias.Api.Controllers
{
    [ApiController]
    public class ActivityGroupController : Controller
    {
        private readonly MahkiasDbContext _context;
        private readonly IUow _uow;

        public ActivityGroupController(MahkiasDbContext context, IUow uow)
        {
            _context = context;
            _uow = uow;
        }

        [HttpGet("/api/projects/{projectId}/activity-groups")]
        public async Task<IActionResult> List(int projectId)
        {
            if (!await ProjectExists(projectId))
            {
                return NotFound();
            }

            return Ok(await _uow.ActivityGroups.ListAsync(projectId));
        }

        [HttpPost("/api/projects/{projectId}/activity-groups")]
        public async Task<IActionResult> Create(int projectId, [FromBody] ActivityGroupWrite request)
        {
            if (!await ProjectExists(projectId))
            {
                return NotFound();
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "Group name is required." });
            }

            var id = await _uow.ActivityGroups.CreateAsync(projectId, request.Name, request.Description);
            var created = (await _uow.ActivityGroups.ListAsync(projectId)).FirstOrDefault(group => group.Id == id);
            return Ok(created);
        }

        [HttpPut("/api/projects/{projectId}/activity-groups/{id}")]
        public async Task<IActionResult> Update(int projectId, int id, [FromBody] ActivityGroupWrite request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "Group name is required." });
            }

            var updated = await _uow.ActivityGroups.UpdateAsync(id, projectId, request.Name, request.Description);
            if (!updated)
            {
                return NotFound();
            }

            var group = (await _uow.ActivityGroups.ListAsync(projectId)).FirstOrDefault(item => item.Id == id);
            return Ok(group);
        }

        [HttpDelete("/api/projects/{projectId}/activity-groups/{id}")]
        public async Task<IActionResult> Delete(int projectId, int id)
        {
            var deleted = await _uow.ActivityGroups.DeleteAsync(id, projectId);
            return deleted ? NoContent() : NotFound();
        }

        [HttpPut("/api/projects/{projectId}/activity-groups/assign")]
        public async Task<IActionResult> AssignMany(int projectId, [FromBody] AssignActivitiesRequest request)
        {
            var ids = request?.ActivityIds?.Where(id => id > 0).Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0)
            {
                return BadRequest(new { message = "Select at least one activity." });
            }

            var result = await _uow.ActivityGroups.AssignManyAsync(projectId, ids, request.ActivityGroupId);
            return Assignment(result);
        }

        [HttpPut("/api/projects/{projectId}/activities/{activityId}/group")]
        public async Task<IActionResult> Assign(int projectId, int activityId, [FromBody] AssignActivityGroupRequest request)
        {
            var result = await _uow.ActivityGroups.AssignAsync(projectId, activityId, request?.ActivityGroupId);
            return result.Updated > 0 && string.IsNullOrEmpty(result.Error) ? NoContent() : Assignment(result);
        }

        private IActionResult Assignment(ActivityGroupAssignResult result)
        {
            if (!string.IsNullOrEmpty(result?.Error))
            {
                return BadRequest(new { message = result.Error });
            }

            if (result == null || result.Missing || result.Updated <= 0)
            {
                return NotFound();
            }

            return Ok(new { updated = result.Updated });
        }

        private async Task<bool> ProjectExists(int projectId)
        {
            return await _context.Projects.AsNoTracking().AnyAsync(project => project.Id == projectId);
        }
    }

    public class ActivityGroupWrite
    {
        public string Name { get; set; }

        public string Description { get; set; }
    }

    public class AssignActivityGroupRequest
    {
        public int? ActivityGroupId { get; set; }
    }

    public class AssignActivitiesRequest
    {
        public List<int> ActivityIds { get; set; } = new List<int>();

        public int? ActivityGroupId { get; set; }
    }
}
