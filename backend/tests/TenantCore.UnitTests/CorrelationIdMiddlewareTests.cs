using FluentAssertions;
using Microsoft.AspNetCore.Http;
using TenantCore.Api.Middleware;
using TenantCore.Application.Common.Security;

namespace TenantCore.UnitTests;

public sealed class CorrelationIdMiddlewareTests
{
    private static DefaultHttpContext CreateContext(string? correlationIdHeader = null)
    {
        var context = new DefaultHttpContext();

        if (correlationIdHeader is not null)
        {
            context.Request.Headers[HeaderNames.CorrelationId] = correlationIdHeader;
        }

        context.Response.Body = new System.IO.MemoryStream();

        return context;
    }

    [Fact]
    public async Task InvokeAsync_WhenNoHeaderPresent_ShouldGenerateGuidCorrelationId()
    {
        var context = CreateContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var id = context.Items[HeaderNames.CorrelationId] as string;
        id.Should().NotBeNullOrWhiteSpace();
        // Guid.NewGuid().ToString("N") is 32 hex chars — no hyphens
        Guid.TryParseExact(id, "N", out _).Should().BeTrue(
            "generated ID should be a compact GUID (32 hex chars, no hyphens)");
    }

    [Fact]
    public async Task InvokeAsync_WithValidAlphanumericId_ShouldPassItThrough()
    {
        const string safeId = "request-ABC123-abc";
        var context = CreateContext(safeId);
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Items[HeaderNames.CorrelationId].Should().Be(safeId);
    }

    [Fact]
    public async Task InvokeAsync_WithValidGuidWithHyphens_ShouldPassItThrough()
    {
        var safeId = Guid.NewGuid().ToString(); // "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
        var context = CreateContext(safeId);
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Items[HeaderNames.CorrelationId].Should().Be(safeId);
    }

    [Theory]
    [InlineData("bad\nid")]           // newline — log injection
    [InlineData("bad\rid")]           // carriage return
    [InlineData("id|piped")]          // pipe — log injection
    [InlineData("<script>alert(1)</script>")] // XSS tag
    [InlineData("has space")]         // space not allowed
    [InlineData("has.dot")]           // dot not allowed
    public async Task InvokeAsync_WithUnsafeHeader_ShouldReplaceWithNewGuid(string unsafeId)
    {
        var context = CreateContext(unsafeId);
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var resultId = context.Items[HeaderNames.CorrelationId] as string;
        resultId.Should().NotBeNullOrWhiteSpace();
        resultId.Should().NotBe(unsafeId, "unsafe IDs must be rejected and replaced");
        Guid.TryParseExact(resultId, "N", out _).Should().BeTrue(
            "replacement must be a compact GUID");
    }

    [Fact]
    public async Task InvokeAsync_WithIdExceeding64Chars_ShouldReplaceWithNewGuid()
    {
        // 65 valid chars — one over the limit
        var oversizedId = new string('a', 65);
        var context = CreateContext(oversizedId);
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var resultId = context.Items[HeaderNames.CorrelationId] as string;
        resultId.Should().NotBe(oversizedId);
        Guid.TryParseExact(resultId, "N", out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithExactly64ValidChars_ShouldPassItThrough()
    {
        // 64 alphanumeric chars — exactly at the boundary
        var maxLengthId = new string('Z', 64);
        var context = CreateContext(maxLengthId);
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Items[HeaderNames.CorrelationId].Should().Be(maxLengthId);
    }

    [Fact]
    public async Task InvokeAsync_ShouldPropagateCorrelationIdInResponseHeader()
    {
        const string safeId = "trace-ABC-123";
        var context = CreateContext(safeId);
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.Headers[HeaderNames.CorrelationId].ToString()
            .Should().Be(safeId);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoHeader_ShouldAlsoPropagateGeneratedIdInResponseHeader()
    {
        var context = CreateContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var itemId = context.Items[HeaderNames.CorrelationId] as string;
        var responseId = context.Response.Headers[HeaderNames.CorrelationId].ToString();

        responseId.Should().Be(itemId, "response header must echo the same ID stored in context.Items");
    }

    [Fact]
    public async Task InvokeAsync_ShouldCallNextMiddleware()
    {
        var context = CreateContext();
        var nextCalled = false;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue("the middleware must always call the next delegate");
    }
}
