import Capacitor
import UIKit

/// The storyboard's root view controller (see `Main.storyboard`'s `customClass`): registers the local Bluetooth
/// printer plugin and keeps the screen on, because the tablet prints only while the app is open on screen.
class MainViewController: CAPBridgeViewController {
    override open func capacitorDidLoad() {
        bridge?.registerPluginInstance(YurtPrinterPlugin())
    }

    override func viewDidAppear(_ animated: Bool) {
        super.viewDidAppear(animated)
        UIApplication.shared.isIdleTimerDisabled = true
    }
}
