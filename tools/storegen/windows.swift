// Lists the on-screen windows a recorder can capture, as JSON.
//
// The desktop game's window is found by owner name, and its bounds are what the
// screen recording is cropped to. Compiled and run by tools/storegen/mac_video.py;
// it needs no accessibility permission because it only reads the window list.
import AppKit
import CoreGraphics
import Foundation

struct Window: Encodable {
    let id: Int
    let owner: String
    let name: String
    let x: Double
    let y: Double
    let width: Double
    let height: Double
}

struct Report: Encodable {
    let displays: [Window]
    let windows: [Window]
}

let options = CGWindowListOption(arrayLiteral: .optionOnScreenOnly, .excludeDesktopElements)

var windows: [Window] = []
if let list = CGWindowListCopyWindowInfo(options, kCGNullWindowID) as? [[String: Any]] {
    for entry in list {
        let bounds = entry[kCGWindowBounds as String] as? [String: Any] ?? [:]
        windows.append(Window(
            id: entry[kCGWindowNumber as String] as? Int ?? 0,
            owner: entry[kCGWindowOwnerName as String] as? String ?? "",
            name: entry[kCGWindowName as String] as? String ?? "",
            x: bounds["X"] as? Double ?? 0,
            y: bounds["Y"] as? Double ?? 0,
            width: bounds["Width"] as? Double ?? 0,
            height: bounds["Height"] as? Double ?? 0))
    }
}

var displays: [Window] = []
for screen in NSScreen.screens {
    displays.append(Window(
        id: 0, owner: screen.localizedName, name: "", x: screen.frame.origin.x,
        y: screen.frame.origin.y, width: screen.frame.width, height: screen.frame.height))
}

let encoder = JSONEncoder()
if let data = try? encoder.encode(Report(displays: displays, windows: windows)),
   let text = String(data: data, encoding: .utf8) {
    print(text)
}
