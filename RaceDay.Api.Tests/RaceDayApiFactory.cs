using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Api.Tests
{
    public class RaceDayApiFactory : WebApplicationFactory<Program>
    {
        // Generated once, when this factory instance is created — not inside
        // the lambda below, which re-runs on every single HTTP request.
        private readonly string _dbName = $"RaceDayTestDb_{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddDbContext<RaceDayDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_dbName);
                });
            });
        }
    }
}