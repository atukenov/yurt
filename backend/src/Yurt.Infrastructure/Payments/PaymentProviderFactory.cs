using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Enums;

namespace Yurt.Infrastructure.Payments;

public class PaymentProviderFactory : IPaymentProviderFactory
{
  private readonly IEnumerable<IPaymentProvider> _providers;

  public PaymentProviderFactory(IEnumerable<IPaymentProvider> providers)
  {
    _providers = providers;
  }

  public IPaymentProvider GetProvider(PaymentProvider provider)
  {
    var paymentProvider = _providers.FirstOrDefault(p => p.Provider == provider);
    if (paymentProvider == null)
      throw new InvalidOperationException($"No payment provider registered for {provider}.");

    return paymentProvider;
  }
}
