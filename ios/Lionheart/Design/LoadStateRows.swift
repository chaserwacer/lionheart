import SwiftUI

/// The `List` rows every `LoadState`-backed screen needs, which stock SwiftUI has no answer for: a
/// first load in progress, and a loaded screen that could not be refreshed.
///
/// `ContentUnavailableView` covers the empty and failed cases, and `ProgressView` covers the
/// spinner — but the *rows* around them are this app's, and History, Coach, Train, Today and Body
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
/// The value stays on screen and this says it could not be updated — the other half of
/// `LoadState.stale`, whose whole purpose is that a failed refresh in a basement does not blank
/// last week's readiness.
struct StaleBanner: View {
    let failure: LoadFailure

    var body: some View {
        Section {
            // `exclamationmark` leads: the symbol with the parts the other way round does not
            // exist, and an `Image(systemName:)` naming a symbol that is not there draws nothing
            // at all — the row then reads as a bare sentence with a gap where its icon should be.
            Label(failure.message, systemImage: "exclamationmark.arrow.triangle.2.circlepath")
                .font(.footnote)
                .foregroundStyle(Color(.secondaryLabel))
        }
    }
}
