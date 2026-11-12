using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data.Args;
using Microsoft.AspNetCore.Mvc;

namespace Mahkias.Api.Helpers
{
    public static class ProjectHistoryLog
    {
        public static async Task Write(this ControllerBase controller, IUow uow, int projectId, string action, string summary)
        {
            if (controller == null || uow == null || projectId <= 0 || string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(summary))
            {
                return;
            }

            var name = action.Trim();
            if (name.Length > 40)
            {
                name = name.Substring(0, 40);
            }

            await uow.ProjectHistory.AddAsync(new ProjectHistoryWrite
            {
                ProjectId = projectId,
                Action = name,
                Summary = summary.Trim(),
                ActorName = controller.ActorName(),
                ActorEmail = controller.ActorEmail(),
            });
        }

        public static async Task WriteAll(this ControllerBase controller, IUow uow, IEnumerable<int> projectIds, string action, string summary)
        {
            if (projectIds == null)
            {
                return;
            }

            foreach (var projectId in projectIds.Where(id => id > 0).Distinct())
            {
                await controller.Write(uow, projectId, action, summary);
            }
        }
    }
}
