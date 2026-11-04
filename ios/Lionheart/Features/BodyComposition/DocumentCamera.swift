import SwiftUI
import VisionKit

/// VisionKit's document camera, which finds the sheet's edges, flattens it and evens its light —
/// a better page to read than a hand-held photo of creased paper.
///
/// A representable because SwiftUI has no document camera of its own. It hands back the first page
/// scanned; a result sheet is one page.
struct DocumentCamera: UIViewControllerRepresentable {
    let scanned: (UIImage) -> Void
    let cancelled: () -> Void

    static var isAvailable: Bool { VNDocumentCameraViewController.isSupported }

    func makeUIViewController(context: Context) -> VNDocumentCameraViewController {
        let camera = VNDocumentCameraViewController()
        camera.delegate = context.coordinator
        return camera
    }

    func updateUIViewController(_ controller: VNDocumentCameraViewController, context: Context) {}

    func makeCoordinator() -> Coordinator { Coordinator(scanned: scanned, cancelled: cancelled) }

    final class Coordinator: NSObject, VNDocumentCameraViewControllerDelegate {
        let scanned: (UIImage) -> Void
        let cancelled: () -> Void

        init(scanned: @escaping (UIImage) -> Void, cancelled: @escaping () -> Void) {
            self.scanned = scanned
            self.cancelled = cancelled
        }

        func documentCameraViewController(
            _ controller: VNDocumentCameraViewController, didFinishWith scan: VNDocumentCameraScan
        ) {
            if scan.pageCount > 0 { scanned(scan.imageOfPage(at: 0)) } else { cancelled() }
        }

        func documentCameraViewControllerDidCancel(_ controller: VNDocumentCameraViewController) {
            cancelled()
        }

        func documentCameraViewController(_ controller: VNDocumentCameraViewController, didFailWithError error: any Error) {
            cancelled()
        }
    }
}
