using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Inventory.Application.Identity;
using Inventory.Domain.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Inventory.Infrastructure.Identity;

public sealed class TokenService : ITokenService
{
    public const string TenantIdClaimType = "tenant_id";
    public const string RoleNameClaimType = "tenant_role";
    public const string PermissionClaimType = "permission";
    public const string PlatformAdminClaimType = "is_platform_admin";

    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public GeneratedTokenResult GenerateTokens(
        ApplicationUser user,
        Guid? activeTenantId,
        string? activeRoleName,
        IReadOnlyCollection<string> permissions)
    {
        var issuer = _configuration["Jwt:Issuer"] ?? "EnterpriseInventorySaaS";
        var audience = _configuration["Jwt:Audience"] ?? "EnterpriseInventorySaaS.Clients";
        var secretKey = _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is not configured.");

        var accessMinutes = _configuration.GetValue<int>("Jwt:AccessTokenExpirationMinutes", 30);
        var refreshDays = _configuration.GetValue<int>("Jwt:RefreshTokenExpirationDays", 14);

        var now = DateTimeOffset.UtcNow;
        var accessExpires = now.AddMinutes(accessMinutes);
        var refreshExpires = now.AddDays(refreshDays);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(PlatformAdminClaimType, user.IsPlatformAdmin ? "true" : "false")
        };

        if (activeTenantId.HasValue && activeTenantId.Value != Guid.Empty)
        {
            claims.Add(new Claim(TenantIdClaimType, activeTenantId.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(activeRoleName))
        {
            claims.Add(new Claim(RoleNameClaimType, activeRoleName));
        }

        foreach (var permission in permissions)
        {
            claims.Add(new Claim(PermissionClaimType, permission));
        }

        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: accessExpires.UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);

        var rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshTokenHash = HashRefreshToken(rawRefreshToken);

        return new GeneratedTokenResult(
            accessToken,
            accessExpires,
            rawRefreshToken,
            refreshTokenHash,
            refreshExpires);
    }

    public string HashRefreshToken(string rawRefreshToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawRefreshToken.Trim());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
