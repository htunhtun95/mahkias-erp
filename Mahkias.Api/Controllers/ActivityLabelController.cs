using Mahkias.Api.Security;
using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data.Result;
using Mahkias.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mahkias.Api.Controllers
{
    [ApiController]
    [RequireApiKey]
    public class ActivityLabelController : Controller
    {
        private readonly MahkiasDbContext _context;
        private readonly IUow _uow;

        public ActivityLabelController(MahkiasDbContext context, IUow uow)
        {
            _context = context;
            _uow = uow;
        }

        [HttpGet("/api/projects/{projectId}/activity-labels")]
        public async Task<IActionResult> List(int projectId)
        {
            if (!await ProjectExists(projectId))
            {
                return NotFound();
            }

            return Ok(await _uow.ActivityLabels.ListAsync(projectId));
        }

        [HttpPost("/api/projects/{projectId}/activity-labels")]
        public async Task<IActionResult> Create(int projectId, [FromBody] ActivityLabelWrite request)
        {
            if (!await ProjectExists(projectId))
            {
                return NotFound();
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Name) || request.TypeId <= 0)
            {
                return BadRequest(new { message = "Label name and type are required." });
            }

            var created = await _uow.ActivityLabels.CreateAsync(projectId, request.Name, request.TypeId);
            if (!string.IsNullOrEmpty(created.Error))
            {
                return BadRequest(new { message = created.Error });
            }

            if (created.Updated <= 0)
            {
                return BadRequest(new { message = "The label could not be created." });
            }

            return Ok(new ActivityLabelResult
            {
                Id = created.Updated,
                ProjectId = projectId,
                Name = request.Name.Trim(),
                TypeId = request.TypeId,
                CreatedAt = DateTime.Now,
            });
        }

        [HttpDelete("/api/projects/{projectId}/activity-labels/{id}")]
        public async Task<IActionResult> Delete(int projectId, int id)
        {
            var deleted = await _uow.ActivityLabels.DeleteAsync(id, projectId);
            return deleted ? NoContent() : NotFound();
        }

        [HttpDelete("/api/projects/{projectId}/activities/{activityId}/labels/{labelId}")]
        public async Task<IActionResult> RemoveActivityLabel(int projectId, int activityId, int labelId)
        {
            var result = await _uow.ActivityLabels.RemoveActivityAsync(projectId, activityId, labelId);
            return Assignment(result);
        }

        [HttpDelete("/api/projects/{projectId}/activities/{activityId}/labels")]
        public async Task<IActionResult> RemoveActivityLabels(int projectId, int activityId)
        {
            var result = await _uow.ActivityLabels.RemoveActivityAsync(projectId, activityId, null);
            if (result.Missing)
            {
                return NotFound();
            }

            return Ok(new { updated = result.Updated });
        }

        [HttpPut("/api/projects/{projectId}/activity-labels/remove")]
        public async Task<IActionResult> RemoveMembers(int projectId, [FromBody] ActivityLabelMembersRequest request)
        {
            var result = await _uow.ActivityLabels.RemoveMembersAsync(projectId, request?.ActivityIds, request?.ActivityGroupIds, request?.LabelId);
            if (!string.IsNullOrEmpty(result?.Error))
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(new { updated = result?.Updated ?? 0 });
        }

        [HttpPut("/api/projects/{projectId}/activity-labels/activities")]
        public async Task<IActionResult> SetActivities(int projectId, [FromBody] ActivityLabelMembersRequest request)
        {
            var result = await _uow.ActivityLabels.SetActivitiesAsync(projectId, request?.ActivityIds, request?.LabelIds);
            return Assignment(result);
        }

        [HttpPut("/api/projects/{projectId}/activity-labels/groups")]
        public async Task<IActionResult> SetGroups(int projectId, [FromBody] ActivityLabelMembersRequest request)
        {
            var result = await _uow.ActivityLabels.SetGroupsAsync(projectId, request?.ActivityGroupIds, request?.LabelIds);
            return Assignment(result);
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

    public class ActivityLabelWrite
    {
        public string Name { get; set; }

        public int TypeId { get; set; }
    }

    public class ActivityLabelMembersRequest
    {
        public List<int> ActivityIds { get; set; } = new List<int>();

        public List<int> ActivityGroupIds { get; set; } = new List<int>();

        public List<int> LabelIds { get; set; } = new List<int>();

        public int? LabelId { get; set; }
    }
}
