import PhotosUI
import SwiftUI
import UniformTypeIdentifiers

/// Getting a scan in: photograph the sheet, pick a photo or a PDF of it, or type it — then check
/// every figure against the paper and save. Also the correction of a recorded scan, which starts at
/// the check.
///
/// The sheet is read on the phone (`SheetRecognizer`); nothing is sent anywhere until Save, and
/// nothing is saved that the athlete has not looked at.
struct ScanEntrySheet: View {
    let store: BodyCompositionStore
    let bodyweight: BodyweightStore
    /// The scan being corrected, or `nil` to record a new one.
    let correcting: BodyCompositionScan?

    @Environment(\.dismiss) private var dismiss
    @State private var step: Step
    /// The athlete's answer on a day's bodyweight, once he has given one. Kept with its day, so an
    /// answer about one day is not carried to another when the date is corrected.
    @State private var bodyweightChoice: (day: CalendarDay, records: Bool)?
    @State private var isSaving = false
    @State private var refusal: LoadFailure?
    @State private var isCameraPresented = false
    @State private var isFileImporterPresented = false
    @State private var isPhotoPickerPresented = false
    @State private var photo: PhotosPickerItem?

    init(store: BodyCompositionStore, bodyweight: BodyweightStore, correcting: BodyCompositionScan? = nil) {
        self.store = store
        self.bodyweight = bodyweight
        self.correcting = correcting
        _step = State(initialValue: correcting.map { .reviewing(ScanDraft.correcting($0), wasRead: false) } ?? .choosing)
    }

    /// Where the entry stands.
    enum Step: Equatable {
        case choosing
        case reading
        case unreadable(String)
        case reviewing(ScanDraft, wasRead: Bool)
    }

    var body: some View {
        NavigationStack {
            content
                .navigationTitle(correcting == nil ? "Add scan" : "Correct scan")
                .navigationBarTitleDisplayMode(.inline)
                .toolbar { toolbar }
        }
        .interactiveDismissDisabled(isReviewing)
        .fullScreenCover(isPresented: $isCameraPresented) {
            DocumentCamera(
                scanned: { image in
                    isCameraPresented = false
                    read(SheetImage(image))
                },
                cancelled: { isCameraPresented = false })
            .ignoresSafeArea()
        }
        // Presented from the sheet rather than from a picker row: the row goes the moment reading
        // starts, and a presentation whose owner has gone takes the sheet down with it.
        .photosPicker(isPresented: $isPhotoPickerPresented, selection: $photo, matching: .images)
        .fileImporter(isPresented: $isFileImporterPresented, allowedContentTypes: [.pdf, .image]) { result in
            if case .success(let url) = result { read(file: url) }
        }
        .onChange(of: photo) { _, item in
            guard let item else { return }
            Task {
                let data = try? await item.loadTransferable(type: Data.self)
                read(data.flatMap(SheetImage.init(data:)))
            }
        }
        .alert(
            "Not saved",
            isPresented: Binding(get: { refusal != nil }, set: { if !$0 { refusal = nil } })
        ) {
            Button("OK") { refusal = nil }
        } message: {
            if let refusal { Text(refusal.message) }
        }
        .task(id: draft?.day) {
            // The day's own bodyweight decides whether the form has to ask what the scan's weight does to it.
            await bodyweight.refreshIfNeeded()
        }
    }

    @ViewBuilder
    private var content: some View {
        switch step {
        case .choosing:
            sources
        case .reading:
            ProgressView("Reading the sheet")
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .unreadable(let reason):
            ContentUnavailableView {
                Text("Could not read that")
            } description: {
                Text(reason)
            } actions: {
                Button("Choose again") { step = .choosing }
                Button("Type it in") { step = .reviewing(.blank(), wasRead: false) }
            }
        case .reviewing(let draft, let wasRead):
            ScanReviewForm(
                draft: Binding(get: { draft }, set: { step = .reviewing($0, wasRead: wasRead) }),
                wasRead: wasRead,
                dayBodyweight: dayBodyweight(on: draft.day),
                recordsBodyweight: Binding(
                    get: { recordsBodyweight(for: draft) },
                    set: { records in bodyweightChoice = draft.day.map { ($0, records) } }),
                previousHeight: store.history.latest?.sheet.height)
        }
    }

    private var sources: some View {
        List {
            Section {
                if DocumentCamera.isAvailable {
                    Button("Scan the sheet") { isCameraPresented = true }
                }
                Button("Choose a photo") { isPhotoPickerPresented = true }
                Button("Choose a PDF or image file") { isFileImporterPresented = true }
                Button("Type it in") { step = .reviewing(.blank(), wasRead: false) }
            } footer: {
                Text("The sheet is read on this phone. Every figure is shown for checking against the paper before anything is saved.")
            }
        }
        .listStyle(.insetGrouped)
    }

    @ToolbarContentBuilder
    private var toolbar: some ToolbarContent {
        ToolbarItem(placement: .cancellationAction) {
            Button("Cancel") { dismiss() }
        }
        if let draft {
            ToolbarItem(placement: .confirmationAction) {
                Button("Save") { Task { await save(draft) } }
                    .disabled(draft.scan == nil || isSaving)
            }
        }
    }

    private var draft: ScanDraft? {
        if case .reviewing(let draft, _) = step { draft } else { nil }
    }

    private var isReviewing: Bool { draft != nil }

    private func dayBodyweight(on day: CalendarDay?) -> DayBodyweight {
        guard let day, let series = bodyweight.state.value,
              day >= CalendarDay(Date()).adding(days: -BodyweightStore.lookBack) else { return .unknown }
        return series.measurements.first { $0.day == day }.map(DayBodyweight.logged) ?? .nothing
    }

    private func recordsBodyweight(for draft: ScanDraft) -> Bool {
        if let bodyweightChoice, bodyweightChoice.day == draft.day { return bodyweightChoice.records }
        return ScanDraft.recordsBodyweight(correcting: correcting?.sheet, day: dayBodyweight(on: draft.day))
    }

    // MARK: - Reading

    private func read(file url: URL) {
        let accessing = url.startAccessingSecurityScopedResource()
        defer { if accessing { url.stopAccessingSecurityScopedResource() } }
        let isPDF = UTType(filenameExtension: url.pathExtension)?.conforms(to: .pdf) ?? false
        read(isPDF ? SheetImage(pdf: url) : (try? Data(contentsOf: url)).flatMap(SheetImage.init(data:)))
    }

    private func read(_ page: SheetImage?) {
        guard let page else {
            step = .unreadable("That file holds no image this phone can open.")
            return
        }
        step = .reading
        Task {
            do {
                let reading = try await SheetRecognizer.read(page)
                step = reading.values.isEmpty
                    ? .unreadable("No sheet figures were found in it. A flat, evenly lit photo of the whole sheet reads best.")
                    : .reviewing(.reading(reading), wasRead: true)
            } catch is CancellationError {
                return
            } catch {
                step = .unreadable("The text could not be read: \(error.localizedDescription)")
            }
        }
    }

    // MARK: - Saving

    private func save(_ draft: ScanDraft) async {
        guard let scan = draft.scan else { return }
        let submission = ScanSubmission(
            day: scan.day, time: scan.time, sheet: scan.sheet, recordsBodyweight: recordsBodyweight(for: draft))
        isSaving = true
        defer { isSaving = false }
        do {
            if let correcting {
                try await store.correct(correcting.id, to: submission)
            } else {
                try await store.record(submission)
            }
            Haptics.setLogged()
            await bodyweight.refresh()
            dismiss()
        } catch is CancellationError {
            return
        } catch let failure as LoadFailure {
            refusal = failure
        } catch {
            refusal = .unavailable(String(describing: error))
        }
    }
}
