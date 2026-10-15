import Foundation
import Capacitor
import PassKit

// Freedom Pay iOS Merchant SDK — not yet added. Add it via Xcode:
// File > Add Package Dependencies > https://github.com/freedompay-global/merchant-sdk-ios.git
// Then uncomment `import FreedomPaymentSdk` and the TODO blocks below.
// See docs/apple-pay-setup.md for the full prerequisite checklist (issue #49).
// import FreedomPaymentSdk

/// Bridges Apple PassKit + the Freedom Pay SDK to the Angular app. JS side:
/// `frontend/projects/shared-api/src/lib/apple-pay.service.ts` (`registerPlugin('ApplePay')`).
///
/// This is a LOCAL plugin (lives directly in the app target, not a separate package), so
/// Capacitor's bridge does NOT auto-discover it — it must be listed by name in
/// `capacitor.config.json`'s `packageClassList`, which `npx cap sync` regenerates from
/// scratch and will silently drop on next sync. See docs/apple-pay-setup.md's "⚠️" note.
@objc(ApplePayPlugin)
public class ApplePayPlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "ApplePayPlugin"
    public let jsName = "ApplePay"
    public let pluginMethods: [CAPPluginMethod] = [
        CAPPluginMethod(name: "canMakePayments", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "startPayment", returnType: CAPPluginReturnPromise),
    ]

    private static let supportedNetworks: [PKPaymentNetwork] = [.visa, .masterCard]

    // TODO: initialize once FreedomPaymentSdk is added. Merchant id/secret/region are
    // Freedom Pay's own credentials (not Apple's) and belong in native config — e.g. an
    // Info.plist entry or an xcconfig build setting — never fetched from the web layer.
    // private lazy var freedomApi = FreedomAPI(merchantId: "...", secretKey: "...", region: .kz)

    private var pendingCall: CAPPluginCall?
    private var pendingFreedomPaymentId: String?

    @objc func canMakePayments(_ call: CAPPluginCall) {
        let available = PKPaymentAuthorizationController.canMakePayments(usingNetworks: Self.supportedNetworks)
        call.resolve(["available": available])
    }

    @objc func startPayment(_ call: CAPPluginCall) {
        guard let orderId = call.getString("orderId"),
              let amount = call.getDouble("amount"),
              let merchantId = call.getString("merchantId") else {
            call.reject("orderId, amount and merchantId are required.")
            return
        }

        pendingCall = call

        // TODO: uncomment once FreedomPaymentSdk is added. This registers the transaction
        // with Freedom Pay and gets back a paymentId, which confirmApplePayment (below)
        // needs after the user authorizes the PassKit sheet.
        //
        // let paymentRequest = StandardPaymentRequest(
        //     amount: Decimal(amount), currency: .KZT,
        //     description: "Order \(orderId)", userId: "", orderId: orderId)
        // freedomApi.createApplePayment(paymentRequest: paymentRequest) { [weak self] result in
        //     switch result {
        //     case .success(let applePayment):
        //         self?.pendingFreedomPaymentId = applePayment.paymentId
        //         self?.presentApplePaySheet(amount: amount, merchantId: merchantId)
        //     case .error(let error):
        //         self?.pendingCall?.reject("Freedom Pay createApplePayment failed: \(error)")
        //         self?.pendingCall = nil
        //     }
        // }

        call.reject("Freedom Pay SDK not yet integrated — see docs/apple-pay-setup.md.")
    }

    private func presentApplePaySheet(amount: Double, merchantId: String) {
        let request = PKPaymentRequest()
        request.merchantIdentifier = merchantId
        request.supportedNetworks = Self.supportedNetworks
        request.merchantCapabilities = .capability3DS
        request.countryCode = "KZ"
        request.currencyCode = "KZT"
        request.paymentSummaryItems = [
            PKPaymentSummaryItem(label: "Altyncup", amount: NSDecimalNumber(value: amount)),
        ]

        guard let controller = PKPaymentAuthorizationViewController(paymentRequest: request) else {
            pendingCall?.reject("Unable to present the Apple Pay sheet.")
            pendingCall = nil
            return
        }
        controller.delegate = self
        DispatchQueue.main.async { [weak self] in
            self?.bridge?.viewController?.present(controller, animated: true)
        }
    }
}

extension ApplePayPlugin: PKPaymentAuthorizationViewControllerDelegate {
    public func paymentAuthorizationViewController(
        _ controller: PKPaymentAuthorizationViewController,
        didAuthorizePayment payment: PKPayment,
        handler completion: @escaping (PKPaymentAuthorizationResult) -> Void
    ) {
        // TODO: uncomment once FreedomPaymentSdk is added. Finalizes the charge with the
        // encrypted Apple Pay token.
        //
        // guard let paymentId = pendingFreedomPaymentId else {
        //     completion(PKPaymentAuthorizationResult(status: .failure, errors: nil))
        //     return
        // }
        // freedomApi.confirmApplePayment(
        //     paymentId: paymentId, tokenData: payment.token.paymentData
        // ) { [weak self] result in
        //     switch result {
        //     case .success(let paymentResponse):
        //         completion(PKPaymentAuthorizationResult(status: .success, errors: nil))
        //         self?.pendingCall?.resolve(["freedomPaymentId": paymentResponse.paymentId])
        //     case .error(let error):
        //         completion(PKPaymentAuthorizationResult(status: .failure, errors: [error]))
        //         self?.pendingCall?.reject("Freedom Pay confirmApplePayment failed: \(error)")
        //     }
        //     self?.pendingCall = nil
        // }
        completion(PKPaymentAuthorizationResult(status: .failure, errors: nil))
    }

    public func paymentAuthorizationViewControllerDidFinish(_ controller: PKPaymentAuthorizationViewController) {
        DispatchQueue.main.async {
            controller.dismiss(animated: true)
        }
        // Reaches here on user cancel, or after didAuthorizePayment's completion handler
        // runs — if pendingCall is still set, nothing resolved/rejected it yet, so this was
        // a cancel rather than a result.
        if let call = pendingCall {
            call.reject("CANCELLED")
            pendingCall = nil
        }
    }
}
