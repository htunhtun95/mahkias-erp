using Mahkias.Core.Modules.Projects.Data;
using Microsoft.EntityFrameworkCore;

namespace Mahkias.Data
{
    public class DbInitialiser
    {
        public static async Task SeedAsync(MahkiasDbContext context)
        {
            await context.Database.EnsureCreatedAsync();

            if (!await context.ActivityTypes.AnyAsync())
            {
                context.ActivityTypes.AddRange(
                    new ActivityType { Id = 1, Name = "Overhaul", Slug = "overhaul" },
                    new ActivityType { Id = 2, Name = "Exchange", Slug = "exchange" },
                    new ActivityType { Id = 3, Name = "Outright", Slug = "outright" }
                );
            }

            var sourcing = await context.ActivityTypes
                .FirstOrDefaultAsync(type => type.Name == "Sourcing" || type.Slug == "sourcing");
            if (sourcing != null)
            {
                sourcing.Name = "Outright";
                sourcing.Slug = "outright";
            }

            await context.SaveChangesAsync();
        }
    }
}
