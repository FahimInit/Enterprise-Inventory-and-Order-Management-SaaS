using FluentValidation;

namespace Inventory.Application.Organizations;

public sealed class CreateOrganizationRequestValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organization name is required.")
            .MinimumLength(2)
            .MaximumLength(150);

        RuleFor(x => x.BaseCurrency)
            .NotEmpty()
            .Length(3).WithMessage("Base currency must be a 3-letter ISO currency code (e.g., USD).");

        RuleFor(x => x.DefaultLowStockThreshold)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.DefaultTaxRatePercent)
            .InclusiveBetween(0m, 100m);
    }
}

public sealed class UpdateOrganizationRequestValidator : AbstractValidator<UpdateOrganizationRequest>
{
    public UpdateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organization name is required.")
            .MinimumLength(2)
            .MaximumLength(150);

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Organization slug is required.")
            .MaximumLength(120);

        RuleFor(x => x.BaseCurrency)
            .NotEmpty()
            .Length(3);

        RuleFor(x => x.DefaultLowStockThreshold)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.DefaultTaxRatePercent)
            .InclusiveBetween(0m, 100m);
    }
}

public sealed class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    public InviteMemberRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(150);

        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("RoleId is required.");
    }
}

public sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(80);

        RuleFor(x => x.Permissions)
            .NotNull()
            .NotEmpty().WithMessage("At least one permission must be assigned to a role.");
    }
}
