using FluentValidation;
using Inventory.Application.Common;

namespace Inventory.Application.UnitTests.Common;

public sealed class PaginationAndValidationTests
{
    private sealed record SampleCommand(string Name, int Quantity);

    private sealed class SampleCommandValidator : AbstractValidator<SampleCommand>
    {
        public SampleCommandValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
            RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be positive.");
        }
    }

    [Fact]
    public void PagedRequest_ShouldClampInvalidPageNumberAndExcessivePageSize()
    {
        var request = new PagedRequest
        {
            PageNumber = -4,
            PageSize = 5000
        };

        request.PageNumber.Should().Be(1);
        request.PageSize.Should().Be(PagedRequest.MaxPageSize);
    }

    [Fact]
    public async Task ValidateAndThrowProblemAsync_WithInvalidInput_ShouldThrowRequestValidationException()
    {
        var validator = new SampleCommandValidator();
        var invalidCommand = new SampleCommand("", 0);

        var act = async () => await validator.ValidateAndThrowProblemAsync(invalidCommand);

        var exceptionAssertion = await act.Should().ThrowAsync<RequestValidationException>();
        exceptionAssertion.Which.Errors.Should().ContainKey("Name");
        exceptionAssertion.Which.Errors.Should().ContainKey("Quantity");
    }
}
