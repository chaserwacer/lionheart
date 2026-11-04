import SwiftUI

/// Last night and the night before, as two rows: each opens onto its night where Oura filed one,
/// and says plainly where it has not.
///
/// Generic over the route because Today and Body each push onto their own route enum.
struct RecentNightRows<Route: Hashable>: View {
    let nights: RecentNights
    /// Whether the nights count back from today, the only day they are last night and the night
    /// before.
    let isToday: Bool
    /// Where a pushed night zooms out of, on a screen that zooms.
    var zoom: Namespace.ID?
    let route: (CalendarDay) -> Route

    var body: some View {
        row(nights.lastNight, isLastNight: true)
        row(nights.nightBefore, isLastNight: false)
    }

    @ViewBuilder
    private func row(_ night: RecentNights.Night, isLastNight: Bool) -> some View {
        let caption = Formatting.nightCaption(night, isLastNight: isLastNight, isToday: isToday)
        switch night {
        case .filed(let event):
            let link = NavigationLink(value: route(event.day)) {
                NightSummaryRow(night: event, caption: caption)
            }
            if let zoom {
                link.matchedTransitionSource(id: route(event.day), in: zoom)
            } else {
                link
            }
        case .notFiled:
            VStack(alignment: .leading, spacing: 2) {
                Text(caption)
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
                // This morning's night arrives with the ring's next sync; an older one never will.
                Text(isLastNight && isToday ? "Not synced yet" : "No night recorded")
                    .foregroundStyle(Color(.secondaryLabel))
            }
            .padding(.vertical, 2)
            .accessibilityElement(children: .combine)
        }
    }
}
