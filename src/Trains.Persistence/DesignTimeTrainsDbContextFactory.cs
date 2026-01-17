using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Trains.Persistence;

public sealed class DesignTimeTrainsDbContextFactory : IDesignTimeDbContextFactory<TrainsDbContext> {
    public TrainsDbContext CreateDbContext(string[] args) {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Trains");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("Set env var 'ConnectionStrings__Trains' to run EF Core design-time tooling.");

        var options = new DbContextOptionsBuilder<TrainsDbContext>()
            .UseNpgsql(cs)
            .Options;

        return new TrainsDbContext(options);
    }
}

