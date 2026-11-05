import SwiftUI

/// The week's hard sets across the body at three resolutions — push, pull and legs; the movement
/// patterns; the sixteen muscle groups — each against its target, with what could not be placed.
struct VolumeView: View {
    let volume: TrainingVolume
    var today: CalendarDay = CalendarDay(Date())

    var body: some View {
        List {
            Section {
                ForEach(volume.families) { family in
                    VolumeRow(
                        label: family.family.name, sets: Double(family.hardSets), target: family.target,
                        lastTrained: family.lastTrained, today: today, isPaused: family.isPaused)
                }
            } header: {
                Text("Families")
            } footer: {
                Text("Every hard set of a movement counts once toward its family. Target 8 a week.")
            }

            Section {
                ForEach(volume.patterns) { pattern in
                    VolumeRow(
                        label: pattern.pattern.name, sets: Double(pattern.hardSets), target: pattern.target,
                        lastTrained: pattern.lastTrained, today: today)
                }
            } header: {
                Text("Patterns")
            } footer: {
                Text("Target 4 a week for each compound pattern. Isolation and other work is counted toward its muscles, not judged as a pattern.")
            }

            Section {
                ForEach(volume.muscles) { muscle in
                    VolumeRow(
                        label: muscle.muscle.name, sets: muscle.sets, target: muscle.target,
                        lastTrained: muscle.lastTrained, today: today)
                }
            } header: {
                Text("Muscle groups")
            } footer: {
                Text("A set counts whole for each muscle it trains as a primary mover and half for a secondary one (Pelland 2026). Target 4 a week: the first four to eight sets buy most of what more would.")
            }

            if !volume.unplaced.isEmpty {
                Section {
                    ForEach(volume.unplaced) { unplaced in
                        NavigationLink(value: HistoryRoute.movement(unplaced.movementDataId)) {
                            LabeledContent(unplaced.name) {
                                Text(Formatting.count(unplaced.hardSets, "set")).monospacedDigit()
                            }
                        }
                    }
                } header: {
                    Text("Not counted")
                } footer: {
                    Text("Nothing says what these train. Classify one from its movement page and its sets count.")
                }
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle("Volume")
        .navigationBarTitleDisplayMode(.inline)
        .safeAreaInset(edge: .top, spacing: 0) {
            Text(windowCaption)
                .font(.caption)
                .foregroundStyle(Color(.secondaryLabel))
                .frame(maxWidth: .infinity)
                .padding(.vertical, 4)
                .background(.bar)
        }
    }

    private var windowCaption: String {
        "Hard sets, \(Formatting.date(volume.window.from)) – \(Formatting.date(volume.window.to)). Warm-ups never count."
    }
}
