using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace backend.Data;

public sealed class DecidirDbContextFactory : IDesignTimeDbContextFactory<DecidirDbContext>
{
    public DecidirDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DecidirDbContext>()
            .UseSqlServer("Server=localhost;Database=decidr_design;Integrated Security=True;TrustServerCertificate=True")
            .Options;

        return new DecidirDbContext(options);
    }
}