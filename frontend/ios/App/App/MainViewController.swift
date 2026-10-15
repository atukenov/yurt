import Capacitor

/// Replaces Capacitor's default `CAPBridgeViewController` as the storyboard's root view
/// controller (see `Main.storyboard`'s `customClass`) solely to register `ApplePayPlugin`.
///
/// `ApplePayPlugin` is a local plugin (lives directly in this app target, not a separate
/// package), so Capacitor's normal auto-discovery can't find it: that mechanism reads
/// `capacitor.config.json`'s `packageClassList`, which `npx cap sync` regenerates from
/// scratch by scanning plugin *packages* in node_modules — it never looks at this folder,
/// and that file is gitignored besides. Registering explicitly here, in committed Swift
/// code, survives `cap sync` and doesn't depend on an untracked generated file.
/// See docs/apple-pay-setup.md for the full story (this cost real debugging time once).
class MainViewController: CAPBridgeViewController {
    override func capacitorDidLoad() {
        bridge?.registerPluginInstance(ApplePayPlugin())
    }
}
