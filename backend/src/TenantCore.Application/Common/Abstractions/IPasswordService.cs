namespace TenantCore.Application.Common.Abstractions;

public interface IPasswordService
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
