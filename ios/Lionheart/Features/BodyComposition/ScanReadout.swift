import SwiftUI

/// One scan read as an instrument: its headline figures and their change since the scan before,
/// then segmental lean as a pattern and a balance, and segmental fat.
///
/// Sections for a `List`, so the composition screen and a past scan's screen draw a scan the same
/// way. The rest of the sheet is `ScanPanels`, which the composition screen puts below its trends.
/// A panel the sheet did not carry has no section.
struct ScanReadout: View {
    let scan: BodyCompositionScan
    let history: CompositionHistory

    private var sheet: BodyCompositionSheet { scan.sheet }
    private var unit: WeightUnit { sheet.unit }

    var body: some View {
        headline
        if let lean = sheet.segmentalLean { segmentalLean(lean) }
        if let fat = sheet.segmentalFat { segmentalFat(fat) }
    }

    // MARK: - Headline

    private var headline: some View {
        Section {
            VStack(alignment: .leading, spacing: 8) {
                Text("\(sheet.device) · \(Formatting.date(scan.day)) · \(scan.time.text)")
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
                SummaryFigures(figures: [
                    ("Weight", SheetFigure.mass(sheet.weight, unit)),
                    ("Skeletal muscle", SheetFigure.mass(sheet.skeletalMuscleMass, unit)),
                    ("Body fat", SheetFigure.percent(sheet.percentBodyFat)),
                ])
                if let change = changeLine {
                    Text(change)
                        .font(.subheadline.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }
            .padding(.vertical, 2)
            figure("Body fat mass", SheetFigure.mass(sheet.bodyFatMass, unit))
            figure("Lean body mass", SheetFigure.mass(sheet.leanBodyMass, unit))
            figure("BMI", SheetFigure.number(sheet.bodyMassIndex))
            figure("Height", sheet.height.unit == .centimetres
                ? "\(SheetFigure.number(Imperial.inches(meters: sheet.height.value / 100))) in"
                : "\(SheetFigure.number(sheet.height.value)) \(sheet.height.unit.abbreviation)")
        }
    }

    /// The headline figures against the scan before this one, when there is one in the same unit.
    private var changeLine: String? {
        guard let previous = history.previous(to: scan) else { return nil }
        let parts: [String] = [
            history.change(.weight, at: scan).map { "weight \(Formatting.signedWeight($0, .pounds))" },
            history.change(.skeletalMuscleMass, at: scan).map { "muscle \(Formatting.signedWeight($0, .pounds))" },
            history.change(.percentBodyFat, at: scan).map { "body fat \(Formatting.change($0, fractionDigits: 1)) pt" },
        ].compactMap(\.self)
        return parts.isEmpty ? nil : "Since \(Formatting.date(previous.day)): \(parts.joined(separator: " · "))"
    }

    // MARK: - Segmental

    private func segmentalLean(_ lean: Segments<SegmentReading>) -> some View {
        let balance = SegmentalBalance.of(lean)
        return Section {
            SegmentChart(segments: lean, unit: unit)
            ViewThatFits(in: .horizontal) {
                Grid(alignment: .trailing, horizontalSpacing: 16, verticalSpacing: 6) {
                    GridRow {
                        Text("").gridColumnAlignment(.leading)
                        Text("Right")
                        Text("Left")
                        Text("L − R")
                    }
                    .font(.caption)
                    .foregroundStyle(Color(.secondaryLabel))
                    balanceRow("Arm", lean.rightArm.mass, lean.leftArm.mass, balance.armDifference, balance.armDifferencePercent)
                    balanceRow("Leg", lean.rightLeg.mass, lean.leftLeg.mass, balance.legDifference, balance.legDifferencePercent)
                }
                VStack(alignment: .leading, spacing: 6) {
                    balanceLine("Arms", lean.rightArm.mass, lean.leftArm.mass, balance.armDifference, balance.armDifferencePercent)
                    balanceLine("Legs", lean.rightLeg.mass, lean.leftLeg.mass, balance.legDifference, balance.legDifferencePercent)
                }
            }
            .font(.subheadline.monospacedDigit())
            .accessibilityElement(children: .combine)
            LabeledContent("Arms · trunk · legs") {
                Text(String(format: "%.1f · %.1f · %.1f%%", balance.armsOfReference, balance.trunkOfReference, balance.legsOfReference))
                    .monospacedDigit()
            }
        } header: {
            Text("Segmental lean")
        } footer: {
            Text("Percent of the analyser's own reference for this height and weight; the dashed rule is 100. Not a clinical norm.")
        }
    }

    /// A segment's mass in pounds, whichever unit the sheet printed.
    private func pounds(_ mass: Double) -> Double { Imperial.pounds(mass, from: unit) }

    private func balanceRow(_ title: String, _ right: Double, _ left: Double, _ difference: Double, _ percent: Double) -> some View {
        GridRow {
            Text(title).gridColumnAlignment(.leading)
            Text(SheetFigure.number(pounds(right)))
            Text(SheetFigure.number(pounds(left)))
            Text("\(Formatting.change(pounds(difference), fractionDigits: 2)) (\(Formatting.change(percent, fractionDigits: 1))%)")
        }
    }

    private func balanceLine(_ title: String, _ right: Double, _ left: Double, _ difference: Double, _ percent: Double) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(title)
            Text("R \(SheetFigure.number(pounds(right))) · L \(SheetFigure.number(pounds(left))) · L − R \(Formatting.change(pounds(difference), fractionDigits: 2)) (\(Formatting.change(percent, fractionDigits: 1))%)")
                .foregroundStyle(Color(.secondaryLabel))
        }
    }

    private func segmentalFat(_ fat: Segments<SegmentReading>) -> some View {
        Section {
            SegmentChart(segments: fat, unit: unit)
        } header: {
            Text("Segmental fat")
        } footer: {
            Text("Mass in lb, and percent of the analyser's own reference.")
        }
    }

    private func figure(_ title: String, _ value: String) -> some View {
        LabeledContent(title) { Text(value).monospacedDigit() }
    }
}

/// The panels of a sheet beyond its headline and segments: water and lean, the analyser's own
/// scores, and impedance.
struct ScanPanels: View {
    let sheet: BodyCompositionSheet

    private var unit: WeightUnit { sheet.unit }

    var body: some View {
        water
        assessments
        if let rows = sheet.impedance, !rows.isEmpty { impedance(rows) }
    }

    // MARK: - The other panels

    @ViewBuilder
    private var water: some View {
        let water = sheet.bodyWater
        let ratio = sheet.extracellularWaterRatio
        let dry = sheet.dryLeanMass
        if water != nil || ratio != nil || dry != nil {
            Section("Water and lean") {
                if let water {
                    figure("Intracellular water", SheetFigure.mass(water.intracellular, unit))
                    figure("Extracellular water", SheetFigure.mass(water.extracellular, unit))
                    figure("Total body water", SheetFigure.mass(water.total, unit))
                }
                if let ratio { figure("ECW/TBW", String(format: "%.3f", ratio)) }
                if let dry { figure("Dry lean mass", SheetFigure.mass(dry, unit)) }
            }
        }
    }

    @ViewBuilder
    private var assessments: some View {
        let score = sheet.fitnessScore
        let rate = sheet.basalMetabolicRate
        let level = sheet.visceralFatLevel
        let control = sheet.massControl
        if score != nil || rate != nil || level != nil || control != nil {
            Section {
                if let score { figure("Fitness score", "\(score)") }
                if let rate { figure("Basal metabolic rate", "\(rate) kcal") }
                if let level { figure("Visceral fat level", "\(level) of 20") }
                if let control {
                    figure("Fat control", SheetFigure.signedMass(control.bodyFat, unit))
                    figure("Lean control", SheetFigure.signedMass(control.leanBody, unit))
                }
            } header: {
                Text("The analyser's own scores")
            } footer: {
                Text("Computed by the analyser against its own reference.")
            }
        }
    }

    private func impedance(_ rows: [ImpedanceRow]) -> some View {
        Section {
            ViewThatFits(in: .horizontal) {
                Grid(alignment: .trailing, horizontalSpacing: 12, verticalSpacing: 6) {
                    GridRow {
                        Text("kHz").gridColumnAlignment(.leading)
                        ForEach(BodySegment.allCases, id: \.self) { Text($0.abbreviation) }
                    }
                    .font(.caption)
                    .foregroundStyle(Color(.secondaryLabel))
                    ForEach(rows, id: \.kilohertz) { row in
                        GridRow {
                            Text(MeasurementText.plain(row.kilohertz))
                            ForEach(BodySegment.allCases, id: \.self) { Text(SheetFigure.number(row.ohms[$0])) }
                        }
                    }
                }
                .font(.subheadline.monospacedDigit())
                VStack(alignment: .leading, spacing: 4) {
                    ForEach(rows, id: \.kilohertz) { row in
                        Text("\(MeasurementText.plain(row.kilohertz)) kHz: " + BodySegment.allCases.map {
                            "\($0.abbreviation) \(SheetFigure.number(row.ohms[$0]))"
                        }.joined(separator: " · "))
                        .font(.subheadline.monospacedDigit())
                    }
                }
            }
        } header: {
            Text("Impedance, Ω")
        }
    }

    private func figure(_ title: String, _ value: String) -> some View {
        LabeledContent(title) { Text(value).monospacedDigit() }
    }
}
