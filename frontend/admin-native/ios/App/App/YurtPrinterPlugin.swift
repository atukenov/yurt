import Capacitor
import CoreBluetooth
import UIKit

/// Services that cheap Bluetooth receipt printers use for "send bytes to print". Same list, same order, as
/// PRINTER_SERVICES in yurt-admin/src/app/core/bluetooth/bluetooth-printing.ts.
private let printerServices = [
    CBUUID(string: "18F0"),
    CBUUID(string: "E7810A71-73AE-499D-8C15-FAA9AEF0C3F2"),
    CBUUID(string: "49535343-FE7D-4AE8-8FA9-9FAFD205E455"),
    CBUUID(string: "6E400001-B5A3-F393-E0A9-E50E24DCCA9E"),
]

/// Bluetooth receipt printers for the admin app on an iPad. The web side (NativeBluetoothConnector in
/// yurt-admin/src/app/core/bluetooth/native-printer.ts) uses it in place of Web Bluetooth, which Safari does not have.
///
/// iPadOS only lets apps reach Bluetooth Low Energy devices (classic Bluetooth needs Apple's MFi programme), so an
/// iPad works with BLE printers only. Everything runs on the main queue.
@objc(YurtPrinterPlugin)
public class YurtPrinterPlugin: CAPPlugin, CAPBridgedPlugin, CBCentralManagerDelegate {
    public let identifier = "YurtPrinterPlugin"
    public let jsName = "YurtPrinter"
    public let pluginMethods: [CAPPluginMethod] = [
        CAPPluginMethod(name: "pick", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "connect", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "write", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "disconnect", returnType: CAPPluginReturnPromise),
    ]

    private static let scanSeconds = 4.0
    private static let connectSeconds = 15.0

    private var central: CBCentralManager?
    private var waitingForPower: [(String?) -> Void] = []
    private var printers: [UUID: BlePrinter] = [:]
    private var seen: [UUID: (CBPeripheral, String)] = [:]
    private var seenOrder: [UUID] = []

    /// Searches for a few seconds, shows the printers found and connects the one staff tap.
    /// Options: title, cancel, none (texts in the app language).
    @objc func pick(_ call: CAPPluginCall) {
        DispatchQueue.main.async {
            self.poweredOn { error in
                if let error = error { return call.reject(error) }
                self.search { found in
                    if found.isEmpty {
                        return call.reject(call.getString("none") ?? "No Bluetooth printers found nearby.", "NO_DEVICES")
                    }
                    self.choose(call, from: found)
                }
            }
        }
    }

    /// Connects again to a printer picked before (deviceId = the id iPadOS gave it), without asking staff.
    @objc func connect(_ call: CAPPluginCall) {
        DispatchQueue.main.async {
            self.poweredOn { error in
                if let error = error { return call.reject(error) }
                guard let id = UUID(uuidString: call.getString("deviceId") ?? ""),
                      let peripheral = self.central?.retrievePeripherals(withIdentifiers: [id]).first
                else { return call.reject("Unknown printer.") }
                self.open(call, peripheral, name: peripheral.name ?? "Bluetooth printer")
            }
        }
    }

    /// Sends ESC/POS bytes (base64 in data) to a connected printer.
    @objc func write(_ call: CAPPluginCall) {
        DispatchQueue.main.async {
            guard let id = UUID(uuidString: call.getString("deviceId") ?? ""), let printer = self.printers[id], printer.ready
            else { return call.reject("The tablet lost the Bluetooth printer.") }
            guard let data = Data(base64Encoded: call.getString("data") ?? "") else {
                return call.reject("The print job is damaged.")
            }
            printer.write(data) { error in
                if let error = error { call.reject(error) } else { call.resolve() }
            }
        }
    }

    @objc func disconnect(_ call: CAPPluginCall) {
        DispatchQueue.main.async {
            if let id = UUID(uuidString: call.getString("deviceId") ?? ""), let printer = self.printers.removeValue(forKey: id) {
                printer.fail("The printer was disconnected.")
                self.central?.cancelPeripheralConnection(printer.peripheral)
            }
            call.resolve()
        }
    }

    // MARK: Finding and connecting

    /// The first use asks staff to allow Bluetooth for the app.
    private func poweredOn(_ done: @escaping (String?) -> Void) {
        if central == nil {
            central = CBCentralManager(delegate: self, queue: nil, options: [CBCentralManagerOptionShowPowerAlertKey: true])
        }
        guard let central = central else { return done("This iPad has no Bluetooth.") }
        if central.state == .unknown || central.state == .resetting {
            waitingForPower.append(done)
            DispatchQueue.main.asyncAfter(deadline: .now() + 10) { [weak self] in
                guard let self = self, !self.waitingForPower.isEmpty else { return }
                let waiting = self.waitingForPower
                self.waitingForPower = []
                waiting.forEach { $0("Bluetooth did not start. Try again.") }
            }
        } else {
            done(Self.problem(central.state))
        }
    }

    private static func problem(_ state: CBManagerState) -> String? {
        switch state {
        case .poweredOn: return nil
        case .poweredOff: return "Bluetooth is off. Turn it on in Settings."
        case .unauthorized: return "The app may not use Bluetooth. Allow Bluetooth for it in Settings."
        case .unsupported: return "This iPad has no Bluetooth."
        default: return "Bluetooth is not ready. Try again."
        }
    }

    private func search(_ done: @escaping ([(CBPeripheral, String)]) -> Void) {
        guard let central = central else { return done([]) }
        seen = [:]
        seenOrder = []
        // Printers already connected to the iPad (by another app) do not advertise, so ask for them too.
        for p in central.retrieveConnectedPeripherals(withServices: printerServices) {
            remember(p, name: p.name)
        }
        central.scanForPeripherals(withServices: nil, options: nil)
        DispatchQueue.main.asyncAfter(deadline: .now() + Self.scanSeconds) {
            central.stopScan()
            done(self.seenOrder.compactMap { self.seen[$0] })
        }
    }

    private func remember(_ peripheral: CBPeripheral, name: String?) {
        guard let name = name, !name.isEmpty else { return } // Nameless devices are headphones, watches and so on.
        if seen[peripheral.identifier] == nil { seenOrder.append(peripheral.identifier) }
        seen[peripheral.identifier] = (peripheral, name)
    }

    private func choose(_ call: CAPPluginCall, from found: [(CBPeripheral, String)]) {
        guard let host = bridge?.viewController else { return call.reject("The app is not ready. Try again.") }
        let sheet = UIAlertController(title: call.getString("title") ?? "Choose the printer", message: nil, preferredStyle: .actionSheet)
        for (peripheral, name) in found {
            sheet.addAction(UIAlertAction(title: name, style: .default) { _ in self.open(call, peripheral, name: name) })
        }
        // On iPad a tap outside the list also runs this.
        sheet.addAction(UIAlertAction(title: call.getString("cancel") ?? "Cancel", style: .cancel) { _ in
            call.reject("Closed without picking a printer.", "CANCELLED")
        })
        if let popover = sheet.popoverPresentationController {
            popover.sourceView = host.view
            popover.sourceRect = CGRect(x: host.view.bounds.midX, y: host.view.bounds.midY, width: 0, height: 0)
            popover.permittedArrowDirections = []
        }
        host.present(sheet, animated: true)
    }

    private func open(_ call: CAPPluginCall, _ peripheral: CBPeripheral, name: String) {
        guard let central = central else { return call.reject("This iPad has no Bluetooth.") }
        let id = peripheral.identifier
        if let old = printers.removeValue(forKey: id) {
            old.fail("The printer was connected again.")
            central.cancelPeripheralConnection(old.peripheral)
        }
        let printer = BlePrinter(peripheral, name: name)
        printers[id] = printer
        printer.whenReady { [weak self] error in
            if let error = error {
                if self?.printers[id] === printer { self?.printers[id] = nil }
                central.cancelPeripheralConnection(peripheral)
                call.reject(error)
            } else {
                call.resolve(["deviceId": id.uuidString, "name": name])
            }
        }
        central.connect(peripheral, options: nil)
        DispatchQueue.main.asyncAfter(deadline: .now() + Self.connectSeconds) {
            if !printer.ready { printer.fail("Could not connect to the printer. Is it on and close to the iPad?") }
        }
    }

    // MARK: CBCentralManagerDelegate

    public func centralManagerDidUpdateState(_ central: CBCentralManager) {
        if central.state == .unknown || central.state == .resetting { return }
        let waiting = waitingForPower
        waitingForPower = []
        waiting.forEach { $0(Self.problem(central.state)) }
        if central.state != .poweredOn {
            // Bluetooth went off: every printer is gone.
            for id in Array(printers.keys) { lost(id) }
        }
    }

    public func centralManager(_ central: CBCentralManager, didDiscover peripheral: CBPeripheral,
                               advertisementData: [String: Any], rssi RSSI: NSNumber) {
        remember(peripheral, name: peripheral.name ?? advertisementData[CBAdvertisementDataLocalNameKey] as? String)
    }

    public func centralManager(_ central: CBCentralManager, didConnect peripheral: CBPeripheral) {
        printers[peripheral.identifier]?.didConnect()
    }

    public func centralManager(_ central: CBCentralManager, didFailToConnect peripheral: CBPeripheral, error: Error?) {
        printers[peripheral.identifier]?.fail("Could not connect to the printer. Is it on and close to the iPad?")
    }

    public func centralManager(_ central: CBCentralManager, didDisconnectPeripheral peripheral: CBPeripheral, error: Error?) {
        guard let printer = printers[peripheral.identifier] else { return } // Closed by the app.
        if printer.ready { lost(peripheral.identifier) } else { printer.fail("Could not connect to the printer.") }
    }

    private func lost(_ id: UUID) {
        guard let printer = printers.removeValue(forKey: id) else { return }
        let wasReady = printer.ready
        printer.fail("The tablet lost the Bluetooth printer.")
        if wasReady { notifyListeners("disconnected", data: ["deviceId": id.uuidString]) }
    }
}

/// One connected BLE printer: finds the characteristic that takes print data and sends jobs in small parts.
final class BlePrinter: NSObject, CBPeripheralDelegate {
    /// Pause between writes when the printer does not confirm each one, so its buffer does not overflow.
    private static let unconfirmedPause = 0.02

    let peripheral: CBPeripheral
    let name: String
    private(set) var ready = false
    private var onReady: ((String?) -> Void)?
    private var characteristic: CBCharacteristic?
    private var writeType: CBCharacteristicWriteType = .withResponse
    private var servicesLeft = 0
    private var parts: [Data] = []
    private var onWritten: ((String?) -> Void)?

    init(_ peripheral: CBPeripheral, name: String) {
        self.peripheral = peripheral
        self.name = name
        super.init()
        peripheral.delegate = self
    }

    func whenReady(_ done: @escaping (String?) -> Void) {
        onReady = done
    }

    func didConnect() {
        peripheral.discoverServices(printerServices)
    }

    /// Ends a connect or a write that is still going on with this error.
    func fail(_ message: String) {
        ready = false
        finishConnect(message)
        finishWrite(message)
    }

    func write(_ data: Data, done: @escaping (String?) -> Void) {
        guard onWritten == nil else { return done("The printer is still busy with the last job.") }
        let size = max(20, min(peripheral.maximumWriteValueLength(for: writeType), 512))
        parts = stride(from: 0, to: data.count, by: size).map { data.subdata(in: $0..<min($0 + size, data.count)) }
        onWritten = done
        sendNext()
    }

    private func sendNext() {
        guard onWritten != nil, let characteristic = characteristic else { return }
        guard let part = parts.first else { return finishWrite(nil) }
        if writeType == .withResponse {
            parts.removeFirst()
            peripheral.writeValue(part, for: characteristic, type: .withResponse) // Next part after didWriteValueFor.
        } else if peripheral.canSendWriteWithoutResponse {
            parts.removeFirst()
            peripheral.writeValue(part, for: characteristic, type: .withoutResponse)
            DispatchQueue.main.asyncAfter(deadline: .now() + Self.unconfirmedPause) { self.sendNext() }
        } // Otherwise peripheralIsReady(toSendWriteWithoutResponse:) calls sendNext.
    }

    private func finishConnect(_ error: String?) {
        guard let done = onReady else { return }
        onReady = nil
        ready = error == nil
        done(error)
    }

    private func finishWrite(_ error: String?) {
        guard let done = onWritten else { return }
        onWritten = nil
        parts = []
        done(error)
    }

    // MARK: CBPeripheralDelegate

    func peripheral(_ peripheral: CBPeripheral, didDiscoverServices error: Error?) {
        let services = known(peripheral)
        if error != nil || services.isEmpty { return finishConnect("This device does not look like a Bluetooth printer.") }
        servicesLeft = services.count
        services.forEach { peripheral.discoverCharacteristics(nil, for: $0) }
    }

    func peripheral(_ peripheral: CBPeripheral, didDiscoverCharacteristicsFor service: CBService, error: Error?) {
        servicesLeft -= 1
        guard servicesLeft == 0, onReady != nil else { return }
        // Like the Chrome version: the first known service with a writable characteristic, confirmed writes first.
        for service in known(peripheral) {
            let all = service.characteristics ?? []
            if let c = all.first(where: { $0.properties.contains(.write) }) {
                characteristic = c
                writeType = .withResponse
                break
            }
            if let c = all.first(where: { $0.properties.contains(.writeWithoutResponse) }) {
                characteristic = c
                writeType = .withoutResponse
                break
            }
        }
        finishConnect(characteristic == nil ? "This device does not look like a Bluetooth printer." : nil)
    }

    func peripheral(_ peripheral: CBPeripheral, didWriteValueFor characteristic: CBCharacteristic, error: Error?) {
        if error != nil { finishWrite("The Bluetooth printer refused the data.") } else { sendNext() }
    }

    func peripheralIsReady(toSendWriteWithoutResponse peripheral: CBPeripheral) {
        sendNext()
    }

    /// The printer's known services, in the order of printerServices.
    private func known(_ peripheral: CBPeripheral) -> [CBService] {
        (peripheral.services ?? [])
            .filter { printerServices.contains($0.uuid) }
            .sorted { (printerServices.firstIndex(of: $0.uuid) ?? 0) < (printerServices.firstIndex(of: $1.uuid) ?? 0) }
    }
}
