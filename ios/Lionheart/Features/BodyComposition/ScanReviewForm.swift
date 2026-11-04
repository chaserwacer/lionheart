import SwiftUI

/// Every figure of a scan, laid out as the sheet groups them, for the athlete to check against the
/// paper before anything is saved.
///
/// A figure the reading could not make out is an empty box marked "Not read". One the sheet's own
/// arithmetic contradicts is marked "Check", with the sum it fails stated at the top. Panels the
/// analyser did not print are switched off rather than left as empty boxes.
struct ScanReviewForm: View {
    @Binding var draft: ScanDraft
    /// Whether the figures came from a reading of the sheet, which is what "Not read" means.
    let wasRead: Bool
    /// What the scan's day already holds as its bodyweight.
    let dayBodyweight: DayBodyweight
    @Binding var recordsBodyweight: Bool
    /// The height the last scan was given, offered for a height the reading could not make out.
    var previousHeight: Height?

    var body: some View {
        let checks = SheetCheck.disagreements(in: draft)
        let flagged = Set(checks.map(\.kind.field)).union(draft.disagreements.keys)

        Form {
            summarySection(checks)
            scanSection
            Section("Body") {
                figure(.height, unit: draft.heightUnit.abbreviation, flagged: flagged)
                if let previousHeight, previousHeight.unit == draft.heightUnit, draft.number(.height) == nil {
                    Button("Use \(SheetFigure.number(previousHeight.value)) \(previousHeight.unit.abbreviation) from the last scan") {
                        draft.entries[.height] = ScanDraft.text(previousHeight.value, for: .height)
                    }
                    .font(.subheadline)
                }
                ForEach(SheetField.core, id: \.self) { field in
                    figure(field, unit: unit(of: field), flagged: flagged)
                }
            }
            ForEach(SheetPanel.allCases, id: \.self) { panel in
                panelSection(panel, flagged: flagged)
            }
            bodyweightSection
        }
    }

    // MARK: - Summary

    @ViewBuilder
    private func summarySection(_ checks: [SheetCheck]) -> some View {
        Section {
            if wasRead {
                Text("Read \(draft.filledCount) of \(draft.askedFields.count) figures. Empty boxes were not legible; fill them from the paper.")
                    .font(.subheadline)
            }
            ForEach(checks, id: \.kind) { check in
                Text(check.message)
                    .font(.subheadline)
                    .foregroundStyle(.orange)
            }
            ForEach(disagreements, id: \.field) { entry in
                VStack(alignment: .leading, spacing: 6) {
                    Text("\(entry.field.title) read as \(entry.values.map { ScanDraft.text($0, for: entry.field) }.joined(separator: " or ")); check the paper.")
                        .font(.subheadline)
                        .foregroundStyle(.orange)
                    HStack {
                        ForEach(entry.values, id: \.self) { value in
                            Button(ScanDraft.text(value, for: entry.field)) {
                                draft.entries[entry.field] = ScanDraft.text(value, for: entry.field)
                            }
                            .buttonStyle(.bordered)
                            .monospacedDigit()
                        }
                    }
                }
            }
        }
    }

    /// Figures the passes read differently, with each reading offered.
    private var disagreements: [(field: SheetField, values: [Double])] {
        draft.disagreements
            .map { ($0.key, $0.value) }
            .sorted { $0.field.title < $1.field.title }
    }

    // MARK: - The scan

    private var scanSection: some View {
        Section("Scan") {
            LabeledContent("Analyser") {
                TextField("InBody570", text: $draft.device)
                    .multilineTextAlignment(.trailing)
                    .autocorrectionDisabled()
                    .textInputAutocapitalization(.never)
            }
            Picker("Unit", selection: $draft.unit) {
                if draft.unit == nil { Text(wasRead ? "Not read" : "Choose").tag(WeightUnit?.none) }
                Text("lb").tag(WeightUnit?.some(.pounds))
                Text("kg").tag(WeightUnit?.some(.kilograms))
            }
            optionalDate
            optionalTime
        }
    }

    @ViewBuilder
    private var optionalDate: some View {
        if let day = draft.day {
            DatePicker(
                "Test date",
                selection: Binding(get: { day.middayDate() }, set: { draft.day = CalendarDay($0) }),
                in: ...Date(),
                displayedComponents: .date)
        } else {
            LabeledContent("Test date") {
                Button(wasRead ? "Not read — set" : "Set") { draft.day = CalendarDay(Date()) }
            }
        }
    }

    @ViewBuilder
    private var optionalTime: some View {
        if let time = draft.time {
            DatePicker(
                "Test time",
                selection: Binding(
                    get: { time.date(on: draft.day ?? CalendarDay(Date())) },
                    set: { draft.time = ClockTime($0) }),
                displayedComponents: .hourAndMinute)
        } else {
            LabeledContent("Test time") {
                Button(wasRead ? "Not read — set" : "Set") { draft.time = ClockTime(Date()) }
            }
        }
    }

    // MARK: - Panels

    @ViewBuilder
    private func panelSection(_ panel: SheetPanel, flagged: Set<SheetField>) -> some View {
        Section {
            Toggle("On this sheet", isOn: Binding(
                get: { draft.panels.contains(panel) },
                set: { isOn in
                    if isOn { draft.panels.insert(panel) } else { draft.panels.remove(panel) }
                }))
            if draft.panels.contains(panel) {
                switch panel {
                case .segmentalLean:
                    ForEach(BodySegment.allCases, id: \.self) { segment in
                        pair(segment.title, mass: .leanMass(segment), percent: .leanPercent(segment), flagged: flagged)
                    }
                case .segmentalFat:
                    ForEach(BodySegment.allCases, id: \.self) { segment in
                        pair(segment.title, mass: .fatMass(segment), percent: .fatPercent(segment), flagged: flagged)
                    }
                case .impedance:
                    impedanceGrid
                case .bodyWater, .dryLeanMass, .extracellularWaterRatio, .fitnessScore, .massControl,
                     .basalMetabolicRate, .visceralFatLevel:
                    ForEach(panel.fields, id: \.self) { field in
                        figure(field, unit: unit(of: field), flagged: flagged)
                    }
                }
            }
        } header: {
            Text(panel.title)
        } footer: {
            if draft.panels.contains(panel) { footer(for: panel) }
        }
    }

    @ViewBuilder
    private func footer(for panel: SheetPanel) -> some View {
        switch panel {
        case .segmentalLean, .segmentalFat:
            Text("Mass, and percent of the analyser's own reference for this height and weight.")
        case .massControl:
            Text("Signed: + to gain, − to lose.")
        case .impedance:
            Text("Ohms. A row the sheet does not show legibly can be left empty.")
        case .bodyWater, .dryLeanMass, .extracellularWaterRatio, .fitnessScore, .basalMetabolicRate, .visceralFatLevel:
            EmptyView()
        }
    }

    // MARK: - Bodyweight

    @ViewBuilder
    private var bodyweightSection: some View {
        let weight = draft.number(.weight)
        let unit = draft.unit
        if let day = draft.day, let weight, let unit {
            Section {
                if let logged = dayBodyweight.differs(from: weight, unit) {
                    Picker("Bodyweight for \(Formatting.date(day))", selection: $recordsBodyweight) {
                        Text("Scan's \(Formatting.weight(weight, unit))").tag(true)
                        Text("Keep \(Formatting.weight(logged.weight, logged.unit))").tag(false)
                    }
                    .pickerStyle(.inline)
                } else if dayBodyweight == .unknown {
                    Toggle("Log \(Formatting.weight(weight, unit)) for \(Formatting.date(day))", isOn: $recordsBodyweight)
                } else {
                    Text("Also logs \(Formatting.weight(weight, unit)) as the bodyweight for \(Formatting.date(day)).")
                        .font(.subheadline)
                        .foregroundStyle(Color(.secondaryLabel))
                }
            } header: {
                Text("Bodyweight")
            } footer: {
                if dayBodyweight.differs(from: weight, unit) != nil {
                    Text("That day already has a bodyweight logged. One day has one bodyweight.")
                } else if dayBodyweight == .unknown {
                    Text("The day's logged bodyweight could not be read here. Logging replaces any it has.")
                }
            }
        }
    }

    // MARK: - Rows

    private func figure(_ field: SheetField, unit: String?, flagged: Set<SheetField>) -> some View {
        LabeledContent {
            HStack(spacing: 4) {
                entry(field)
                if let unit {
                    Text(unit).foregroundStyle(Color(.secondaryLabel))
                }
            }
        } label: {
            VStack(alignment: .leading, spacing: 1) {
                Text(field.title)
                if flagged.contains(field) {
                    Text("Check").font(.caption.weight(.semibold)).foregroundStyle(.orange)
                }
            }
        }
    }

    private func pair(_ title: String, mass: SheetField, percent: SheetField, flagged: Set<SheetField>) -> some View {
        LabeledContent {
            HStack(spacing: 4) {
                entry(mass)
                Text(draft.unit?.abbreviation ?? "").foregroundStyle(Color(.secondaryLabel))
                entry(percent)
                Text("%").foregroundStyle(Color(.secondaryLabel))
            }
        } label: {
            VStack(alignment: .leading, spacing: 1) {
                Text(title)
                if flagged.contains(mass) || flagged.contains(percent) {
                    Text("Check").font(.caption.weight(.semibold)).foregroundStyle(.orange)
                }
            }
        }
    }

    private var impedanceGrid: some View {
        Grid(alignment: .trailing, horizontalSpacing: 6, verticalSpacing: 8) {
            GridRow {
                Text("kHz")
                ForEach(BodySegment.allCases, id: \.self) { Text($0.abbreviation) }
            }
            .font(.caption)
            .foregroundStyle(Color(.secondaryLabel))
            ForEach(SheetPanel.impedanceFrequencies, id: \.self) { kilohertz in
                GridRow {
                    Text("\(kilohertz)").font(.subheadline.monospacedDigit())
                    ForEach(BodySegment.allCases, id: \.self) { segment in
                        entry(.impedance(kilohertz: kilohertz, segment), prompt: "")
                    }
                }
            }
        }
    }

    private func entry(_ field: SheetField, prompt: String? = nil) -> some View {
        TextField(
            field.title,
            text: Binding(get: { draft.entries[field] ?? "" }, set: { draft.entries[field] = $0 }),
            prompt: Text(prompt ?? (wasRead ? "Not read" : "—")))
            .keyboardType(field.isSigned ? .numbersAndPunctuation : .decimalPad)
            .multilineTextAlignment(.trailing)
            .monospacedDigit()
            .frame(minWidth: 44)
            .fixedSize(horizontal: false, vertical: true)
            .accessibilityLabel(field.title)
    }

    private func unit(of field: SheetField) -> String? {
        switch field {
        case .weight, .skeletalMuscleMass, .bodyFatMass, .leanBodyMass, .intracellularWater, .extracellularWater,
             .totalBodyWater, .dryLeanMass, .leanMass, .fatMass, .fatControl, .leanControl:
            draft.unit?.abbreviation
        case .percentBodyFat, .leanPercent, .fatPercent: "%"
        case .basalMetabolicRate: "kcal"
        case .bodyMassIndex, .extracellularWaterRatio, .fitnessScore, .visceralFatLevel, .impedance, .height: nil
        }
    }
}

private extension SheetField {
    /// The control figures carry a sign, which the decimal pad has no key for.
    var isSigned: Bool { self == .fatControl || self == .leanControl }
}
