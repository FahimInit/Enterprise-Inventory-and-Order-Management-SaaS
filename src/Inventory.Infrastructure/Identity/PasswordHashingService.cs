using Inventory.Application.Identity;
using Inventory.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace Inventory.Infrastructure.Identity;

public sealed class PasswordHashingService : IPasswordHashingService
{
    private readonly PasswordHasher<ApplicationUser> _hasher = new();

    public string HashPassword(ApplicationUser user, string plainPassword)
    {
        return _hasher.HashPassword(user, plainPassword);
    }

    public bool VerifyPassword(ApplicationUser user, string passwordHash, string providedPassword)
    {
        var result = _hasher.VerifyHashedPassword(user, passwordHash, providedPassword);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
