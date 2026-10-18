using System.Text.RegularExpressions;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Application;

public sealed partial class CheckoutValidator : IValidator<CheckoutCart>, IValidator<SubmitDraftOrder>
{
    public IEnumerable<FieldError> Validate(CheckoutCart request) => Validate(request.PaymentMethod, request.Phone);

    public IEnumerable<FieldError> Validate(SubmitDraftOrder request) => Validate(request.PaymentMethod, request.Phone);

    private static IEnumerable<FieldError> Validate(PaymentMethod method, string phone)
    {
        if (method == PaymentMethod.None)
        {
            yield return new FieldError("paymentMethod", "Choose how to pay: MPesa, EMola, MKesh, Card or CashOnDelivery.");
        }

        if (phone is null || !MozambicanPhone().IsMatch(phone.Trim()))
        {
            yield return new FieldError("phone", "Phone numbers use the 258XXXXXXXXX format.");
        }
    }

    [GeneratedRegex(@"^258\d{9}$")]
    private static partial Regex MozambicanPhone();
}
