// Draws Tempo's icon and launch mark from the one geometry the app draws the mark with
// (Lionheart/Design/ProgressionMark.swift), so the Home Screen, the launch screen and the screens
// inside cannot drift apart. Run through render-brand-assets.sh, which compiles the two files
// together; the output is committed, and running it again reproduces it byte for byte.
//
// The icon is an Icon Composer document: the bars as two SVG layers, filled per appearance by
// icon.json. iOS 26 draws it as Liquid Glass; for iOS 18 the asset compiler renders flat light, dark
// and tinted pictures from the same document, and ignores an AppIcon.appiconset of the same name,
// which is why there is none.
//
// The palette is written out here because these files are made before any asset catalog exists to
// read it from. It is the values Assets.xcassets holds.

import CoreGraphics
import Foundation
import ImageIO
import SwiftUI
import UniformTypeIdentifiers

struct RGB {
    let red: Double, green: Double, blue: Double

    init(hex: UInt32) {
        red = Double((hex >> 16) & 0xFF) / 255
        green = Double((hex >> 8) & 0xFF) / 255
        blue = Double(hex & 0xFF) / 255
    }

    var cgColor: CGColor {
        CGColor(colorSpace: CGColorSpace(name: CGColorSpace.sRGB)!, components: [red, green, blue, 1])!
    }

    var svg: String {
        String(format: "#%02X%02X%02X", Int((red * 255).rounded()), Int((green * 255).rounded()), Int((blue * 255).rounded()))
    }

    /// Icon Composer's spelling of a solid sRGB colour.
    var iconComposer: String {
        String(format: "srgb:%.5f,%.5f,%.5f,1.00000", red, green, blue)
    }
}

let graphite = RGB(hex: 0x14171A)
let chalk = RGB(hex: 0xEFECE4)
let brick = RGB(hex: 0xC8452C)
let brickOnDark = RGB(hex: 0xE0573C)

/// Where the mark sits on the icon: centred, then nudged halfway toward its centre of mass, since
/// the tallest bar is the heaviest and a box-centred mark reads as sitting low and right.
func iconMarkRect(canvas: CGFloat, widthShare: CGFloat) -> CGRect {
    let width = canvas * widthShare
    let height = width / ProgressionMark.aspectRatio
    // The bars' centre of mass sits 4.6 of 84 right of the box's centre and 7.6 of 76 below it.
    let nudgeX = -width * (4.6 / 84) / 2
    let nudgeY = -height * (7.6 / 76) / 2
    return CGRect(x: (canvas - width) / 2 + nudgeX, y: (canvas - height) / 2 + nudgeY, width: width, height: height)
}

/// The mark on a transparent square: the ink pair in `ink` and this week in `thisWeek`.
func png(ink: RGB, thisWeek: RGB, pixels: Int, to url: URL) throws {
    let space = CGColorSpace(name: CGColorSpace.sRGB)!
    let context = CGContext(
        data: nil, width: pixels, height: pixels, bitsPerComponent: 8, bytesPerRow: 0, space: space,
        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    // SwiftUI's paths are y-down; CoreGraphics's bitmap is y-up.
    context.translateBy(x: 0, y: CGFloat(pixels))
    context.scaleBy(x: 1, y: -1)
    let frame = CGRect(x: 0, y: 0, width: pixels, height: pixels)
    context.addPath(ProgressionMark(bars: [.first, .second]).path(in: frame).cgPath)
    context.setFillColor(ink.cgColor)
    context.fillPath()
    context.addPath(ProgressionMark(bars: [.third]).path(in: frame).cgPath)
    context.setFillColor(thisWeek.cgColor)
    context.fillPath()

    let image = context.makeImage()!
    try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
    let destination = CGImageDestinationCreateWithURL(url as CFURL, UTType.png.identifier as CFString, 1, nil)!
    CGImageDestinationAddImage(destination, image, nil)
    guard CGImageDestinationFinalize(destination) else { throw RenderFailure.write(url) }
}

/// The bars as an SVG path, for an Icon Composer layer. Icon Composer fills a layer itself, per
/// appearance, so the file's own fill only shows in a viewer.
func svg(_ bars: [ProgressionMark.Bar], canvas: CGFloat, mark: CGRect, fill: RGB) -> String {
    var data = ""
    ProgressionMark(bars: bars).path(in: mark).forEach { element in
        switch element {
        case .move(let to): data += String(format: "M%.2f %.2f ", to.x, to.y)
        case .line(let to): data += String(format: "L%.2f %.2f ", to.x, to.y)
        case .closeSubpath: data += "Z "
        case .quadCurve, .curve: fatalError("The mark is straight lines only.")
        }
    }
    let size = Int(canvas)
    return """
        <svg xmlns="http://www.w3.org/2000/svg" width="\(size)" height="\(size)" viewBox="0 0 \(size) \(size)">
          <path d="\(data.trimmingCharacters(in: .whitespaces))" fill="\(fill.svg)"/>
        </svg>

        """
}

/// A fill per appearance, in Icon Composer's form. Tinted is read for its luminance.
func fills(_ light: RGB, dark: RGB, tinted: RGB? = nil) -> [[String: Any]] {
    var fills: [[String: Any]] = [
        ["value": ["solid": light.iconComposer]],
        ["appearance": "dark", "value": ["solid": dark.iconComposer]],
    ]
    if let tinted { fills.append(["appearance": "tinted", "value": ["solid": tinted.iconComposer]]) }
    return fills
}

enum RenderFailure: Error {
    case write(URL)
}

let ios = URL(fileURLWithPath: CommandLine.arguments[1], isDirectory: true)

// The Home Screen icon: Chalk ground and Graphite bars, Graphite ground and Chalk bars in dark, this
// week in Brick on both; tinted keeps this week the brightest.
let icon = ios.appending(path: "Lionheart/Resources/AppIcon.icon")
let iconMark = iconMarkRect(canvas: 1024, widthShare: 0.58)
try FileManager.default.createDirectory(at: icon.appending(path: "Assets"), withIntermediateDirectories: true)
try svg([.first, .second], canvas: 1024, mark: iconMark, fill: graphite)
    .write(to: icon.appending(path: "Assets/ink.svg"), atomically: true, encoding: .utf8)
try svg([.third], canvas: 1024, mark: iconMark, fill: brick)
    .write(to: icon.appending(path: "Assets/brick.svg"), atomically: true, encoding: .utf8)

let iconDocument: [String: Any] = [
    "fill-specializations": fills(chalk, dark: graphite),
    "groups": [[
        "layers": [
            ["name": "ink", "image-name": "ink.svg", "glass": true,
             "fill-specializations": fills(graphite, dark: chalk, tinted: RGB(hex: 0x9E9E9E))],
            ["name": "brick", "image-name": "brick.svg", "glass": true,
             "fill-specializations": fills(brick, dark: brickOnDark, tinted: RGB(hex: 0xFFFFFF))],
        ],
        "shadow": ["kind": "neutral", "opacity": 0.5],
        // Off: with it on, the ground shows through the ink and greys the lower half of each bar.
        "translucency": ["enabled": false, "value": 0.5],
    ]],
    "supported-platforms": ["squares": ["iOS"]],
]
try JSONSerialization.data(withJSONObject: iconDocument, options: [.prettyPrinted, .sortedKeys])
    .write(to: icon.appending(path: "icon.json"))

// The launch screen's mark, always on Graphite: a 100-point square with the mark fitted in it, which
// is exactly the frame `OpeningView` first draws it in, so the hand-over has nothing to move.
let launchMark = ios.appending(path: "Lionheart/Resources/Assets.xcassets/LaunchMark.imageset")
for scale in [1, 2, 3] {
    try png(ink: chalk, thisWeek: brickOnDark, pixels: 100 * scale,
            to: launchMark.appending(path: scale == 1 ? "LaunchMark.png" : "LaunchMark@\(scale)x.png"))
}

print("render-brand-assets: icon and launch mark written under \(ios.path)")
