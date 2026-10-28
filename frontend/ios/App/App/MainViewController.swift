import Capacitor

/// The storyboard's root view controller (see `Main.storyboard`'s `customClass`). It used to register a local
/// Apple Pay plugin; online payments now open the payment provider's page (Stripe) in the phone's browser, where
/// Apple Pay works without any native code, so nothing is registered here. Kept as the place to register a local
/// plugin later (`bridge?.registerPluginInstance(...)` in `capacitorDidLoad`).
class MainViewController: CAPBridgeViewController {
}
