using System.Collections.Generic;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Payments.DTOs;

namespace Yurt.Application.Common.Interfaces;

public interface IPaymentWebhookValidator
{
  Task<Result<ValidatedPaymentWebhook>> ValidateAsync(string rawBody, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default);
}
