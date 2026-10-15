using Yurt.Domain.Enums;

namespace Yurt.Application.Common.Interfaces;

public interface IPaymentProviderFactory
{
  IPaymentProvider GetProvider(PaymentProvider provider);
}
