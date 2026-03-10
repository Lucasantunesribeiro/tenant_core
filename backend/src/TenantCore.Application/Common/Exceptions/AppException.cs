namespace TenantCore.Application.Common.Exceptions;

public sealed class AppException(
    string type,
    string title,
    int statusCode,
    string detail,
    IDictionary<string, string[]>? errors = null) : Exception(detail)
{
    public string Type { get; } = type;

    public string Title { get; } = title;

    public int StatusCode { get; } = statusCode;

    public IDictionary<string, string[]>? Errors { get; } = errors;
}
