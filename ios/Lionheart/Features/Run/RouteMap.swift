import MapKit
import SwiftUI

/// A run's route on a map: the path as one line in the accent colour, framed to fit it.
///
/// Points of interest are left off — the line is the subject, and a map full of cafés is not.
/// On the run's page the map is a still picture, because a map that pans inside a scrolling list
/// takes the scroll for itself; tapping it opens `RouteMapSheet`, which can be moved and zoomed.
struct RouteMap: View {
    let route: [RouteCoordinate]
    var isInteractive = false

    var body: some View {
        let coordinates = route.map { CLLocationCoordinate2D(latitude: $0.latitude, longitude: $0.longitude) }

        Map(initialPosition: .rect(Self.frame(around: coordinates)), interactionModes: isInteractive ? .all : []) {
            MapPolyline(coordinates: coordinates)
                .stroke(.tint, lineWidth: 3)
        }
        .mapStyle(.standard(pointsOfInterest: .excludingAll))
        .accessibilityLabel("Map of the route")
    }

    /// The smallest map rectangle holding every point, with a margin so the line does not touch
    /// the edges.
    static func frame(around coordinates: [CLLocationCoordinate2D]) -> MKMapRect {
        let points = coordinates.map(MKMapPoint.init)
        guard let first = points.first else { return .world }
        let bounds = points.dropFirst().reduce(MKMapRect(origin: first, size: MKMapSize(width: 0, height: 0))) { rect, point in
            rect.union(MKMapRect(origin: point, size: MKMapSize(width: 0, height: 0)))
        }
        let margin = max(bounds.width, bounds.height) * 0.15 + 200
        return bounds.insetBy(dx: -margin, dy: -margin)
    }
}

/// The route full screen, to move about and zoom into.
struct RouteMapSheet: View {
    let route: [RouteCoordinate]

    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            RouteMap(route: route, isInteractive: true)
                .ignoresSafeArea(edges: .bottom)
                .navigationTitle("Route")
                .navigationBarTitleDisplayMode(.inline)
                .toolbar {
                    ToolbarItem(placement: .topBarTrailing) {
                        Button("Done") { dismiss() }
                    }
                }
        }
    }
}
