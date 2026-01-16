interface PaymentMethodSelectorProps {
  paymentMethod: "kaspi" | "applepay";
  onPaymentMethodChange: (method: "kaspi" | "applepay") => void;
  t: any;
}

export function PaymentMethodSelector({
  paymentMethod,
  onPaymentMethodChange,
  t,
}: PaymentMethodSelectorProps) {
  return (
    <div>
      <h2 className="text-xl font-bold text-gray-900 mb-4">
        {t.paymentMethodTitle}
      </h2>
      <div className="space-y-3">
        <label className="flex items-center">
          <input
            type="radio"
            name="payment"
            value="kaspi"
            checked={paymentMethod === "kaspi"}
            onChange={(e) =>
              onPaymentMethodChange(e.target.value as "kaspi" | "applepay")
            }
            className="w-4 h-4"
          />
          <span className="ml-3 text-gray-700">{t.payWithKaspi}</span>
        </label>
        <label className="flex items-center">
          <input
            type="radio"
            name="payment"
            value="applepay"
            checked={paymentMethod === "applepay"}
            onChange={(e) =>
              onPaymentMethodChange(e.target.value as "kaspi" | "applepay")
            }
            className="w-4 h-4"
          />
          <span className="ml-3 text-gray-700">{t.payWithApplePay}</span>
        </label>
      </div>
    </div>
  );
}
