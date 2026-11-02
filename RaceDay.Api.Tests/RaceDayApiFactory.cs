using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaceDay.Api.Data;
using RaceDay.Api.Services;
 
namespace RaceDay.Api.Tests
{
    public class RaceDayApiFactory : WebApplicationFactory<Program>
    {
        // Generated once, when this factory instance is created, not inside
        // the lambda below, which re-runs on every single HTTP request.
        private readonly string _dbName = $"RaceDayTestDb_{Guid.NewGuid()}";
 
        // The stand-in for Azure Blob Storage. Tests can read what was "uploaded" and "deleted".
        public FakeBlobStorageService BlobStorage { get; } = new();
 
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Swap SQL Server for a fresh in-memory database
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);
 
                services.AddDbContext<RaceDayDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_dbName);
                });
 
                // Swap Azure Blob Storage for the fake, so tests never need Azure or secrets
                services.RemoveAll<IBlobStorageService>();
                services.AddSingleton<IBlobStorageService>(BlobStorage);
            });
        }
    }
}