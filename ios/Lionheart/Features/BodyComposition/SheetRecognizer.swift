import CoreGraphics
import Foundation
import ImageIO
import PDFKit
import UIKit
import Vision

/// Reads a photographed or scanned body-composition sheet on the phone, with Apple's text
/// recognition — free, offline, and nothing leaves the device.
///
/// The sheet is read several times: whole, and cut into overlapping tiles of three sizes. A tile
/// gives the recogniser the sheet's small print at a larger size, which is where most of its
/// figures are; the whole page reads the headings that tiles cut in half. Each pass is read by
/// `InBodySheetReader` on its own, and the passes vote (`SheetReading.merging`).
///
/// `RecognizeTextRequest` rather than iOS 26's `RecognizeDocumentsRequest`: measured on a creased
/// photo of a real sheet, the document request found no tables and read fewer figures than the
/// whole-page text request, and tiling the text request read the most of all.
enum SheetRecognizer {
    /// The tilings each pass cuts the page into, as columns by rows.
    private static let tilings: [(columns: Int, rows: Int)] = [(1, 1), (2, 3), (2, 4), (3, 4)]
    /// How far each tile reaches into its neighbours, so a line on a seam is whole in one of them.
    private static let overlap = 0.12

    @concurrent
    static func read(_ page: SheetImage) async throws -> SheetReading {
        var passes: [SheetReading] = []
        for tiling in tilings {
            try Task.checkCancellation()
            passes.append(InBodySheetReader.read(try await lines(on: page, tiling: tiling)))
        }
        return SheetReading.merging(passes)
    }

    private static func lines(on page: SheetImage, tiling: (columns: Int, rows: Int)) async throws -> [RecognizedLine] {
        var lines: [RecognizedLine] = []
        for row in 0..<tiling.rows {
            for column in 0..<tiling.columns {
                let tile = region(column: column, row: row, of: tiling)
                var request = RecognizeTextRequest()
                request.recognitionLevel = .accurate
                request.usesLanguageCorrection = false
                request.regionOfInterest = NormalizedRect(normalizedRect: tile)
                let observations = try await request.perform(on: page.image, orientation: page.orientation)
                lines += observations.compactMap { observation in
                    observation.topCandidates(1).first.map { place($0.string, observation.boundingBox.cgRect, in: tile) }
                }
            }
        }
        return lines
    }

    /// One tile, in Vision's normalised space, whose origin is the bottom left.
    private static func region(column: Int, row: Int, of tiling: (columns: Int, rows: Int)) -> CGRect {
        let width = 1 / Double(tiling.columns), height = 1 / Double(tiling.rows)
        let left = max(0, Double(column) * width - overlap * width)
        let right = min(1, Double(column + 1) * width + overlap * width)
        let top = max(0, Double(row) * height - overlap * height)
        let bottom = min(1, Double(row + 1) * height + overlap * height)
        return CGRect(x: left, y: 1 - bottom, width: right - left, height: bottom - top)
    }

    /// A line found in a tile, placed on the page with the origin at the top left. Vision answers
    /// in the tile's own normalised space.
    private static func place(_ text: String, _ box: CGRect, in tile: CGRect) -> RecognizedLine {
        let x = tile.minX + box.minX * tile.width
        let bottom = tile.minY + box.minY * tile.height
        let height = box.height * tile.height
        return RecognizedLine(text: text, x: x, y: 1 - (bottom + height), width: box.width * tile.width, height: height)
    }
}

/// A page to read: its pixels and which way up they are.
struct SheetImage: Sendable {
    let image: CGImage
    let orientation: CGImagePropertyOrientation

    init(image: CGImage, orientation: CGImagePropertyOrientation = .up) {
        self.image = image
        self.orientation = orientation
    }

    init?(_ image: UIImage) {
        guard let cgImage = image.cgImage else { return nil }
        self.init(image: cgImage, orientation: CGImagePropertyOrientation(image.imageOrientation))
    }

    init?(data: Data) {
        guard let image = UIImage(data: data) else { return nil }
        self.init(image)
    }

    /// The first page of a PDF, drawn tall enough for the sheet's small print — a gym's PDF and a
    /// photo saved as one both come this way.
    init?(pdf url: URL) {
        guard let page = PDFDocument(url: url)?.page(at: 0) else { return nil }
        let bounds = page.bounds(for: .mediaBox)
        guard bounds.height > 0 else { return nil }
        let scale = 2600 / bounds.height
        let rendered = page.thumbnail(of: CGSize(width: bounds.width * scale, height: 2600), for: .mediaBox)
        self.init(rendered)
    }
}

private extension CGImagePropertyOrientation {
    init(_ orientation: UIImage.Orientation) {
        switch orientation {
        case .up: self = .up
        case .down: self = .down
        case .left: self = .left
        case .right: self = .right
        case .upMirrored: self = .upMirrored
        case .downMirrored: self = .downMirrored
        case .leftMirrored: self = .leftMirrored
        case .rightMirrored: self = .rightMirrored
        @unknown default: self = .up
        }
    }
}
