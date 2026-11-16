using Inventory.Domain.Common;

namespace Inventory.Domain.Identity;

public sealed class ApplicationUser : EntityBase
{
    public const int MaxFailedLoginAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsPlatformAdmin { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int FailedLoginCount { get; private set; }
    public DateTimeOffset? LockoutEndUtc { get; private set; }
    public DateTimeOffset? LastLoginAtUtc { get; private set; }

    private ApplicationUser()
    {
    }

    public ApplicationUser(string email, string fullName, string passwordHash, bool isPlatformAdmin = false)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("User.EmailRequired", "User email is required.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("User.FullNameRequired", "Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("User.PasswordHashRequired", "Password hash is required.");
        }

        Email = email.Trim();
        NormalizedEmail = Email.ToUpperInvariant();
        FullName = fullName.Trim();
        PasswordHash = passwordHash;
        IsPlatformAdmin = isPlatformAdmin;
        IsActive = true;
    }

    public bool IsLockedOut(DateTimeOffset nowUtc) =>
        LockoutEndUtc.HasValue && LockoutEndUtc.Value > nowUtc;

    public void EnsureCanAuthenticate(DateTimeOffset nowUtc)
    {
        if (!IsActive)
        {
            throw new ForbiddenAccessException("Auth.UserInactive", "This user account has been deactivated.");
        }

        if (IsLockedOut(nowUtc))
        {
            throw new ForbiddenAccessException("Auth.AccountLocked", "Account is temporarily locked due to multiple failed login attempts.");
        }
    }

    public void RecordLoginSuccess(DateTimeOffset nowUtc)
    {
        FailedLoginCount = 0;
        LockoutEndUtc = null;
        LastLoginAtUtc = nowUtc;
        TouchUpdatedTimestamp();
    }

    public void RecordLoginFailure(DateTimeOffset nowUtc)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= MaxFailedLoginAttempts)
        {
            LockoutEndUtc = nowUtc.Add(LockoutDuration);
        }

        TouchUpdatedTimestamp();
    }

    public void UpdatePasswordHash(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new DomainException("User.PasswordHashRequired", "New password hash cannot be empty.");
        }

        PasswordHash = newPasswordHash;
        FailedLoginCount = 0;
        LockoutEndUtc = null;
        TouchUpdatedTimestamp();
    }

    public void UpdateProfile(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("User.FullNameRequired", "Full name is required.");
        }

        FullName = fullName.Trim();
        TouchUpdatedTimestamp();
    }

    public void Deactivate()
    {
        IsActive = false;
        TouchUpdatedTimestamp();
    }

    public void Activate()
    {
        IsActive = true;
        FailedLoginCount = 0;
        LockoutEndUtc = null;
        TouchUpdatedTimestamp();
    }
}
