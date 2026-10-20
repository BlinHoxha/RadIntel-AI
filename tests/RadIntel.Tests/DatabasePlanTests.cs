using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RadIntel.Infrastructure;

namespace RadIntel.Tests;

public sealed class DatabasePlanTests
{
    [Fact]
    public void PostgreSql_model_matches_migration_snapshot_without_connecting()
    {
        using var db = new DesignTimeFactory().CreateDbContext([]);
        Assert.False(db.Database.HasPendingModelChanges());
    }
    [Fact]
    public void Migration_sql_contains_schema_only_and_no_seed_inserts()
    {
        using var db = new DesignTimeFactory().CreateDbContext([]);
        var sql = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE", sql);
        Assert.Contains("FOREIGN KEY", sql);
        Assert.DoesNotContain("INSERT INTO \"Patients\"", sql);
        Assert.DoesNotContain("INSERT INTO \"Templates\"", sql);
        Assert.DoesNotContain("CREATE DATABASE", sql);
    }
}
