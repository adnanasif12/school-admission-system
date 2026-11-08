using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SchoolAdmission.Api.Data;

/// <summary>Lets `dotnet ef migrations add` build the context without booting the whole app.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<AdmissionDbContext>
{
    public AdmissionDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AdmissionDbContext>()
            .UseNpgsql("Host=localhost;Database=design_time_only")
            .Options);
}
