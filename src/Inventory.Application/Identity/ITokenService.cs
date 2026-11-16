using Inventory.Domain.Identity;

namespace Inventory.Application.Identity;

public interface IPasswordHashingService
{
    string HashPassword(ApplicationUser user, string plainPassword);
    bool VerifyPassword(ApplicationUser user, string passwordHash, string providedPassword);
}

public sealed record GeneratedTokenResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RawRefreshToken,
    string RefreshTokenHash,
    DateTimeOffset RefreshTokenExpiresAtUtc);

public interface ITokenService
{
    GeneratedTokenResult GenerateTokens(
        ApplicationUser user,
        Guid? activeTenantId,
        string? activeRoleName,
        IReadOnlyCollection<string> permissions);

    string HashRefreshToken(string rawRefreshToken);
}
