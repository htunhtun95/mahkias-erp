using Mahkias.Api.Modules.Projects;
using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data.Args;
using Microsoft.AspNetCore.Mvc;

namespace Mahkias.Api.Controllers
{
    [ApiController]
    [Route("activities")]
    public class ActivityController : Controller
    {
        private readonly IUow _uow;

        public ActivityController(IUow uow)
        {
            _uow = uow;
        }

        [HttpPut("/api/activities/batch")]
        public async Task<IActionResult> UpdateBatch([FromBody] BatchUpdateActivitiesRequest request)
        {
            var activities = request?.Activities ?? new List<BatchActivityUpdate>();
            if (activities.Count == 0)
            {
                return BadRequest(new { message = "At least one activity is required." });
            }

            var errors = new List<object>();
            foreach (var activity in activities)
            {
                if (activity == null || activity.Id <= 0)
                {
                    errors.Add(new { id = activity?.Id ?? 0, field = "Id", message = "Activity is required." });
                    continue;
                }

                if (activity.ProjectId <= 0)
                {
                    errors.Add(new { id = activity.Id, field = "Project", message = "Project is required." });
                }

                if (string.IsNullOrWhiteSpace(activity.DSNNo))
                {
                    errors.Add(new { id = activity.Id, field = "DSN No", message = "Field is missing." });
                }

                if (string.IsNullOrWhiteSpace(activity.PartNo))
                {
                    errors.Add(new { id = activity.Id, field = "Part No", message = "Field is missing." });
                }

                if (activity.TypeId == null || activity.TypeId <= 0)
                {
                    errors.Add(new { id = activity.Id, field = "Type", message = "Field is missing." });
                }

                if (activity.Quantity == null || activity.Quantity < 0)
                {
                    errors.Add(new { id = activity.Id, field = "Quantity", message = "Quantity must be a whole number." });
                }
            }

            if (errors.Count > 0)
            {
                return BadRequest(new { message = "Activity updates failed validation.", errors });
            }

            var updated = await _uow.Activities.UpdateBatchAsync(activities.Select(activity => new ActivityBatchUpdate
            {
                Id = activity.Id,
                Args = new CreateActivityArgs
                {
                    ProjectId = activity.ProjectId,
                    PartNo = activity.PartNo,
                    Budget = activity.Budget,
                    Description = activity.Description,
                    DSNNo = activity.DSNNo,
                    Quantity = activity.Quantity,
                    TypeId = activity.TypeId
                }
            }).ToList());

            if (updated <= 0)
            {
                return BadRequest(new { message = "The activities could not be updated." });
            }

            return Ok(new { updated });
        }

        [HttpPut("/api/activities/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateActivityRequest request)
        {
            if (request == null || request.ProjectId <= 0)
            {
                return BadRequest("Project is required.");
            }

            if (string.IsNullOrWhiteSpace(request.PartNo))
            {
                return BadRequest("Part no is required.");
            }

            if (request.TypeId == null || request.TypeId <= 0)
            {
                return BadRequest(new { message = "Type is required." });
            }

            var current = (await _uow.Activities.GetByProjectAsync(request.ProjectId))
                .FirstOrDefault(item => item.Id == id);
            if (current == null)
            {
                return NotFound();
            }

            if (current.ActivityGroupId != null && current.TypeId != request.TypeId)
            {
                return BadRequest(new { message = "Type cannot be changed while the activity belongs to a group." });
            }

            var updated = await _uow.Activities.UpdateAsync(id, new CreateActivityArgs
            {
                ProjectId = request.ProjectId,
                PartNo = request.PartNo,
                Budget = request.Budget,
                Description = request.Description,
                DSNNo = request.DSNNo,
                Quantity = request.Quantity,
                TypeId = request.TypeId > 0 ? request.TypeId : null
            });

            if (updated <= 0)
            {
                return NotFound();
            }

            var activity = (await _uow.Activities.GetByProjectAsync(request.ProjectId))
                .FirstOrDefault(item => item.Id == id);
            if (activity == null)
            {
                return NotFound();
            }

            return Ok(new ActivityResponse
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
            });
        }

        [HttpDelete("/api/activities/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _uow.Activities.DeleteAsync(id);
            if (deleted <= 0)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
