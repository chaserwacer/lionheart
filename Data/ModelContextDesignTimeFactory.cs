using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace lionheart.Data
{
    // Lets `dotnet ef` build the context directly instead of booting the whole
    // web host (which starts SpaProxy/the SPA dev server and never returns).
    public class ModelContextDesignTimeFactory : IDesignTimeDbContextFactory<ModelContext>
    {
        public ModelContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<ModelContext>()
                .UseSqlite("Data Source=./Data/lionheart.db")
                .Options;

            return new ModelContext(options);
        }
    }
}
