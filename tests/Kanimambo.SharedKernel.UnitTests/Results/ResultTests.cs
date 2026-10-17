using Kanimambo.SharedKernel.Results;

namespace Kanimambo.SharedKernel.UnitTests.Results;

public sealed class ResultTests
{
    private static readonly Error NotFound = Error.NotFound("catalog.product_not_found", "Product not found.");

    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Failure_exposes_the_error_but_not_a_value()
    {
        Result<int> result = NotFound;

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(NotFound);
        Should.Throw<InvalidOperationException>(() => result.Value).Message.ShouldContain(NotFound.Code);
    }

    [Fact]
    public void Values_convert_implicitly_to_successful_results()
    {
        Result<string> result = "Matola";

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Matola");
    }

    [Fact]
    public void Match_selects_the_branch_for_the_outcome()
    {
        Result<int> success = 42;
        Result<int> failure = NotFound;

        success.Match(v => $"ok {v}", e => e.Code).ShouldBe("ok 42");
        failure.Match(v => $"ok {v}", e => e.Code).ShouldBe(NotFound.Code);
    }

    [Fact]
    public void Validation_error_requires_at_least_one_field_error()
    {
        Should.Throw<ArgumentException>(() => new ValidationError([]));

        var error = Error.Validation(new FieldError("nuit", "NUIT must have 9 digits."));
        error.Type.ShouldBe(ErrorType.Validation);
        error.Code.ShouldBe(ValidationError.ValidationCode);
        error.Errors.ShouldHaveSingleItem().Field.ShouldBe("nuit");
    }
}
