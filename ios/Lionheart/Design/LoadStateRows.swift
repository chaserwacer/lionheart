import SwiftUI

/// The `List` rows every `LoadState`-backed screen needs, which stock SwiftUI has no answer for: a
/// first load in progress, and a loaded screen that could not be refreshed.
///
/// `ContentUnavailableView` covers the empty and failed cases, and `ProgressView` covers the
/// spinner — but the *rows* around them are this app's, and History, Chat, Train, Today and Body
/// all draw them identically. Five copies of the stale banner would be five chances for one of them
/// to start blanking its screen instead.

/// What a screen shows while its first load is out.
///
/// Kept for the screens whose first load has no row shape worth promising — a sheet, or a section
/// that is a single figure. A list of rows uses `SkeletonRows` instead, which says what is coming.
struct LoadingRow: View {
    var body: some View {
        Section {
            HStack(spacing: 10) {
                ProgressView()
                Text("Loading").foregroundStyle(Color(.secondaryLabel))
            }
        }
    }
}

/// Rows shaped like the ones that are coming, in grey, while the first load is out.
///
/// This is Apple's own answer — real-shaped content under `.redacted(reason: .placeholder)` — and
/// it is what makes a list feel instant rather than empty: the screen's structure is there before
/// its content is, so nothing jumps when the content lands. The stand-in strings are never read,
/// only measured: their length sets each bar's width, and because they are `Text` at the screen's
/// own text styles the bars grow with Dynamic Type exactly as the real rows will.
///
/// Only the first load draws these. A refresh keeps the rows it already has — see `LoadState`.
struct SkeletonRows: View {
    /// How many rows to promise. Enough to fill the space the content will take, and no more:
    /// rows below the fold cost layout for something nobody sees.
    let count: Int
    /// The leading mark, where the rows being waited for have one.
    var systemImage: String?

    var body: some View {
        ForEach(0..<count, id: \.self) { index in
            SkeletonRow(
                systemImage: systemImage,
                headline: Self.headlines[index % Self.headlines.count],
                detail: Self.details[index % Self.details.count])
                // Announced once, by the first row. A screen that read out five grey rows would be
                // reading out its own scaffolding.
                .accessibilityElement(children: .ignore)
                .accessibilityLabel("Loading")
                .accessibilityHidden(index != 0)
        }
    }

    /// Stand-in strings of the lengths real rows run to, so the bars are not all one width — which
    /// is what makes a placeholder read as a list rather than as a loading graphic.
    private static let headlines = [
        "Back squat, Romanian deadlift",
        "Bench press",
        "Chin-ups, Barbell row, Curl",
    ]

    private static let details = [
        "Yesterday · 18 sets · 52 min",
        "Tuesday · 9 sets",
        "Last Friday · 24 sets · 1 h 06 min",
    ]
}

/// One grey row: a headline, a line under it, and the mark the real row leads with.
private struct SkeletonRow: View {
    let systemImage: String?
    let headline: String
    let detail: String

    var body: some View {
        HStack(alignment: .top, spacing: 12) {
            if let systemImage {
                Image(systemName: systemImage)
                    .font(.title3)
                    .foregroundStyle(Color(.tertiaryLabel))
            }
            VStack(alignment: .leading, spacing: 3) {
                Text(headline).font(.headline)
                Text(detail).font(.subheadline)
            }
        }
        .padding(.vertical, 2)
        .redacted(reason: .placeholder)
    }
}

/// The note that a loaded screen is not current.
///
/// The value stays on screen and this says why it is not current — the other half of
/// `LoadState.stale`, whose whole purpose is that a failed refresh in a basement does not blank
/// last week's readiness.
///
/// An unreachable server is marked the way an instrument marks a held reading: the time it dates
/// from, and nothing apologetic. A server that answered with a fault is a different sentence, its
/// own words, because the screen's age is not the news there. A screen opened onto the device's
/// last answer gets no row at all: the server usually answers before the row could be read, and a
/// row that came and went would move everything under it twice. `recalledNote(_:)` says it instead,
/// floating, once the server is slow.
struct StaleBanner: View {
    let staleness: Staleness

    init(_ staleness: Staleness) {
        self.staleness = staleness
    }

    var body: some View {
        switch staleness {
        case .recalled:
            EmptyView()
        case .unreachable, .refreshFailed:
            Section { note }
        }
    }

    @ViewBuilder
    private var note: some View {
        switch staleness {
        case .recalled:
            EmptyView()
        case .unreachable(let asOf):
            LabeledContent {
                Text("Server unreachable")
            } label: {
                Text(Formatting.asOf(asOf))
                    .monospacedDigit()
                    .foregroundStyle(Color(.label))
            }
            .font(.footnote)
            .foregroundStyle(Color(.secondaryLabel))
        case .refreshFailed(let failure):
            // `exclamationmark` leads: the symbol with the parts the other way round does not
            // exist, and an `Image(systemName:)` naming a symbol that is not there draws nothing
            // at all — the row then reads as a bare sentence with a gap where its icon should be.
            Label(failure.message, systemImage: "exclamationmark.arrow.triangle.2.circlepath")
                .font(.footnote)
                .foregroundStyle(Color(.secondaryLabel))
        }
    }
}

/// The time a recalled screen dates from, floating over the top of it once the server has kept it
/// waiting — the way Mail says it is checking without moving a message.
///
/// A second's grace, because the server on a good connection answers well inside it. Floating
/// chrome rather than a row, so neither its arrival nor its leaving moves the content under it.
private struct RecalledNote: ViewModifier {
    let asOf: Date?

    @State private var isShown = false
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    func body(content: Content) -> some View {
        content
            .overlay(alignment: .top) {
                if isShown, let asOf {
                    HStack(spacing: 8) {
                        ProgressView().controlSize(.small)
                        Text("\(Formatting.asOf(asOf)) · Updating")
                            .monospacedDigit()
                    }
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
                    .padding(.horizontal, 14)
                    .padding(.vertical, 8)
                    .glassChrome()
                    .padding(.top, 4)
                    .transition(reduceMotion ? .opacity : .move(edge: .top).combined(with: .opacity))
                    .accessibilityElement(children: .combine)
                }
            }
            .task(id: asOf) {
                guard asOf != nil, (try? await Task.sleep(for: .seconds(1))) != nil else {
                    withAnimation { isShown = false }
                    return
                }
                withAnimation { isShown = true }
            }
    }
}

extension View {
    /// Floats the time a recalled screen dates from over it while the server is slow to answer.
    /// `asOf` is the recalled answer's time, or `nil` once the screen is current or says why not.
    func recalledNote(_ asOf: Date?) -> some View {
        modifier(RecalledNote(asOf: asOf))
    }
}
