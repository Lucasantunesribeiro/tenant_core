using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Projects.Queries;
using TenantCore.Domain.Entities;
using TenantCore.Domain.Enums;
using TenantCore.Infrastructure.Database;
using TenantCore.UnitTests.Common;

namespace TenantCore.UnitTests;

/// <summary>
/// Validates that the pageSize cap (max 100) in query handlers protects against
/// resource exhaustion — callers requesting millions of rows are silently capped.
/// </summary>
public sealed class PageSizeCapTests
{
    private static async Task<(TenantCoreDbContext db, TestCurrentSession session)> BuildContextWithProjectsAsync(
        string dbName,
        int projectCount)
    {
        var tenantId = Guid.NewGuid();
        var session = new TestCurrentSession
        {
            UserId = Guid.NewGuid(),
            TenantId = tenantId,
            Role = UserRole.Admin,
        };

        var db = TestDbContextFactory.Create(dbName, session);

        var owner = new User(tenantId, "manager@test.dev", "Manager User", "hash", UserRole.Manager);
        db.Users.Add(owner);

        for (var i = 0; i < projectCount; i++)
        {
            db.Projects.Add(new Project(
                tenantId,
                null,
                owner.Id,
                $"Project {i:D4}",
                $"PROJ{i:D4}",
                $"Seeded project {i}",
                ProjectStatus.Active,
                null,
                null));
        }

        await db.SaveChangesAsync();

        return (db, session);
    }

    [Fact]
    public async Task GetProjects_WhenPageSizeIsOneMillion_ShouldReturnAtMost100Items()
    {
        var (db, session) = await BuildContextWithProjectsAsync(
            nameof(GetProjects_WhenPageSizeIsOneMillion_ShouldReturnAtMost100Items),
            projectCount: 150);

        var handler = new GetProjectsQueryHandler(db, session);
        var result = await handler.Handle(
            new GetProjectsQuery(Search: null, Status: null, Page: 1, PageSize: 1_000_000),
            CancellationToken.None);

        result.Items.Count.Should().BeLessThanOrEqualTo(100,
            "the handler must cap pageSize to 100 regardless of the requested value");
        result.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task GetProjects_WhenPageSizeIs50_ShouldReturnAtMost50Items()
    {
        var (db, session) = await BuildContextWithProjectsAsync(
            nameof(GetProjects_WhenPageSizeIs50_ShouldReturnAtMost50Items),
            projectCount: 150);

        var handler = new GetProjectsQueryHandler(db, session);
        var result = await handler.Handle(
            new GetProjectsQuery(Search: null, Status: null, Page: 1, PageSize: 50),
            CancellationToken.None);

        result.Items.Count.Should().Be(50);
        result.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task GetProjects_WhenPageSizeIsZero_ShouldReturnZeroItems()
    {
        var (db, session) = await BuildContextWithProjectsAsync(
            nameof(GetProjects_WhenPageSizeIsZero_ShouldReturnZeroItems),
            projectCount: 10);

        var handler = new GetProjectsQueryHandler(db, session);

        // pageSize=0 → Math.Min(0, 100) = 0; Take(0) returns no rows — safe, not a crash
        var result = await handler.Handle(
            new GetProjectsQuery(Search: null, Status: null, Page: 1, PageSize: 0),
            CancellationToken.None);

        result.Items.Should().BeEmpty("taking 0 items must return an empty list, not throw");
        result.PageSize.Should().Be(0);
        result.TotalCount.Should().Be(10, "totalCount is independent of pageSize");
    }

    [Fact]
    public async Task GetProjects_WhenPageSizeIs100Exactly_ShouldNotBeReduced()
    {
        var (db, session) = await BuildContextWithProjectsAsync(
            nameof(GetProjects_WhenPageSizeIs100Exactly_ShouldNotBeReduced),
            projectCount: 120);

        var handler = new GetProjectsQueryHandler(db, session);
        var result = await handler.Handle(
            new GetProjectsQuery(Search: null, Status: null, Page: 1, PageSize: 100),
            CancellationToken.None);

        result.Items.Count.Should().Be(100);
        result.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task GetProjects_WhenPageSizeIs101_ShouldBeReducedTo100()
    {
        var (db, session) = await BuildContextWithProjectsAsync(
            nameof(GetProjects_WhenPageSizeIs101_ShouldBeReducedTo100),
            projectCount: 120);

        var handler = new GetProjectsQueryHandler(db, session);
        var result = await handler.Handle(
            new GetProjectsQuery(Search: null, Status: null, Page: 1, PageSize: 101),
            CancellationToken.None);

        result.Items.Count.Should().Be(100);
        result.PageSize.Should().Be(100);
    }
}
