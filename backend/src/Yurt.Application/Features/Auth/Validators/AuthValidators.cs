using FluentValidation;
using Yurt.Application.Features.Auth.DTOs;

namespace Yurt.Application.Features.Auth.Validators;

public class CustomerRegisterValidator : AbstractValidator<CustomerRegisterDto>
{
    public CustomerRegisterValidator()
    {
        RuleFor(x => x.MobileNumber)
            .NotEmpty()
            .Matches(@"^\+?[1-9]\d{6,14}$")
            .WithMessage("Invalid mobile number format.");

        RuleFor(x => x.Pin4)
            .NotEmpty()
            .Length(4)
            .Matches(@"^\d{4}$")
            .WithMessage("PIN must be exactly 4 digits.");
    }
}

public class RegisterVerifyValidator : AbstractValidator<RegisterVerifyDto>
{
    public RegisterVerifyValidator()
    {
        RuleFor(x => x.MobileNumber)
            .NotEmpty()
            .Matches(@"^\+?[1-9]\d{6,14}$")
            .WithMessage("Invalid mobile number format.");

        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches(@"^\d{4}$")
            .WithMessage("Code must be exactly 4 digits.");
    }
}

public class CustomerLoginValidator : AbstractValidator<CustomerLoginDto>
{
    public CustomerLoginValidator()
    {
        RuleFor(x => x.MobileNumber).NotEmpty();
        RuleFor(x => x.Pin4)
            .NotEmpty()
            .Length(4)
            .Matches(@"^\d{4}$")
            .WithMessage("PIN must be exactly 4 digits.");
    }
}

public class ChangeMobileNumberValidator : AbstractValidator<ChangeMobileNumberDto>
{
    public ChangeMobileNumberValidator()
    {
        RuleFor(x => x.MobileNumber)
            .NotEmpty()
            .Matches(@"^\+?[1-9]\d{6,14}$")
            .WithMessage("Invalid mobile number format.");
    }
}

public class AdminLoginValidator : AbstractValidator<AdminLoginDto>
{
    public AdminLoginValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
