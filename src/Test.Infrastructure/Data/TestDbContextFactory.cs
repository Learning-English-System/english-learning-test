using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Test.Infrastructure.Data;

public class TestDbContextFactory : IDesignTimeDbContextFactory<TestDbContext>
{
    public TestDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost,1433;Database=TestDb;User Id=sa;Password=YourStrong@Passw0rd!2026;TrustServerCertificate=True;");

        return new TestDbContext(optionsBuilder.Options);
    }
}
