using FluentValidation;

namespace Inventory.Application.Catalog;

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MinimumLength(2)
            .MaximumLength(120);

        RuleFor(x => x.Description)
            .MaximumLength(500);
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MinimumLength(2)
            .MaximumLength(120);

        RuleFor(x => x.Description)
            .MaximumLength(500);
    }
}

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("CategoryId is required.");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("Product SKU is required.")
            .MaximumLength(64);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200);

        RuleFor(x => x.UnitOfMeasure)
            .NotEmpty().WithMessage("Unit of measurement is required.")
            .MaximumLength(24);

        RuleFor(x => x.StandardCost)
            .GreaterThanOrEqualTo(0m).WithMessage("Standard cost cannot be negative.");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0m).WithMessage("Unit price cannot be negative.");

        RuleFor(x => x.ReorderPoint)
            .GreaterThanOrEqualTo(0).WithMessage("Reorder point cannot be negative.");

        RuleFor(x => x.TargetStockLevel)
            .GreaterThanOrEqualTo(x => x.ReorderPoint)
            .WithMessage("Target stock level must be greater than or equal to the reorder point.");
    }
}

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("CategoryId is required.");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("Product SKU is required.")
            .MaximumLength(64);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200);

        RuleFor(x => x.UnitOfMeasure)
            .NotEmpty().WithMessage("Unit of measurement is required.")
            .MaximumLength(24);

        RuleFor(x => x.StandardCost)
            .GreaterThanOrEqualTo(0m);

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0m);

        RuleFor(x => x.ReorderPoint)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.TargetStockLevel)
            .GreaterThanOrEqualTo(x => x.ReorderPoint)
            .WithMessage("Target stock level must be greater than or equal to the reorder point.");
    }
}
