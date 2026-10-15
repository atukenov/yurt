using Yurt.Application.Features.Payments.DTOs;
using Yurt.Domain.Enums;

namespace Yurt.Application.Common.Interfaces;

public interface IPaymentProvider
{
  PaymentProvider Provider { get; }
  Task<PaymentInvoiceResponse> CreateInvoiceAsync(CreatePaymentInvoiceRequest request, CancellationToken cancellationToken = default);
  Task<PaymentStatusResponse> GetPaymentStatusAsync(string invoiceId, CancellationToken cancellationToken = default);
}
