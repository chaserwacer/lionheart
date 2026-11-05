import SwiftUI
import Testing

@testable import Lionheart

/// The mark's geometry. Its claims are the ones the opening, the loading indicator and the rendered
/// icon rely on: whichever bars are drawn, they sit where they do in the whole mark, so separately
/// filled bars stack into one; and growing keeps a bar's foot and lean.
@Suite("The Progression mark")
struct ProgressionMarkTests {
    private static let frame = CGRect(x: 0, y: 0, width: 84, height: 76)

    @Test("The whole mark fills a frame of its own proportions edge to edge")
    func wholeMarkFillsItsFrame() {
        let bounds = ProgressionMark().path(in: Self.frame).boundingRect
        #expect(abs(bounds.minX - 0) < 0.001)
        #expect(abs(bounds.maxX - 84) < 0.001)
        #expect(abs(bounds.minY - 0) < 0.001)
        #expect(abs(bounds.maxY - 76) < 0.001)
    }

    @Test("A bar drawn alone is where it stands in the whole mark")
    func barAloneKeepsItsPlace() {
        let third = ProgressionMark(bars: [.third]).path(in: Self.frame).boundingRect
        // Foot at 66 and top reaching 98 in the design box, which starts at 14 across and 8 down.
        #expect(abs(third.minX - 52) < 0.001)
        #expect(abs(third.maxX - 84) < 0.001)
        #expect(abs(third.minY - 0) < 0.001)
    }

    @Test("Each bar is a step taller than the one before it")
    func barsStepUp() {
        let heights = ProgressionMark.Bar.allCases.map {
            ProgressionMark(bars: [$0]).path(in: Self.frame).boundingRect.height
        }
        #expect(heights == heights.sorted())
        #expect(Set(heights).count == 3)
    }

    @Test("Growing keeps the foot on the baseline and the lean")
    func growingKeepsFootAndLean() {
        let half = ProgressionMark(bars: [.third], rise: 0.5).path(in: Self.frame).boundingRect
        #expect(abs(half.maxY - 76) < 0.001)
        #expect(abs(half.height - 38) < 0.001)
        // 16 wide, and the top runs ahead of the foot by the lean times the height.
        #expect(abs(half.width - (16 + 38 * ProgressionMark.lean)) < 0.05)
    }

    @Test("Nothing has grown at zero, and a rise past the ends is held to them")
    func riseIsClamped() {
        #expect(ProgressionMark(rise: 0).path(in: Self.frame).isEmpty)
        #expect(ProgressionMark(rise: 1.4).path(in: Self.frame).boundingRect
            == ProgressionMark().path(in: Self.frame).boundingRect)
    }

    @Test("A frame of another shape holds the mark centred, at its own proportions")
    func fitsAndCentres() {
        let square = CGRect(x: 0, y: 0, width: 100, height: 100)
        let bounds = ProgressionMark().path(in: square).boundingRect
        #expect(abs(bounds.width / bounds.height - ProgressionMark.aspectRatio) < 0.001)
        #expect(abs(bounds.midX - 50) < 0.001)
        #expect(abs(bounds.midY - 50) < 0.001)
    }
}
