import SwiftUI

/// What the model is given about the athlete, in the athlete's own hands.
///
/// It is the premise behind every answer on the Chat screen, and an answer is only worth trusting
/// if its premise is inspectable. The briefing leads, exactly as the next message will carry it,
/// because it is what most answers rest on; the two standing tiers follow with their as-of instants.
///
/// Read-only on purpose. The card is a derived view over the sessions, wellness entries, Oura days
/// and injuries — it is regenerated from them and is never a source of truth, so editing it here
/// would be editing a shadow.
struct ContextCardSheet: View {
    let store: ChatStore

    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            List {
                switch store.contextCard {
                case .loading:
                    LoadingRow()
                case .unavailable(let failure):
                    unavailable(failure)
                case .loaded(let card):
                    content(card, staleBecause: nil)
                case .stale(let card, let staleness):
                    content(card, staleBecause: staleness)
                }
            }
            .listStyle(.insetGrouped)
            .navigationTitle("Athlete context")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Done") { dismiss() }
                }
            }
            // Refreshing it can rebuild it server-side, which costs a model call — so it is a pull
            // the athlete asks for and never something this sheet does on its own.
            .refreshable { await store.refreshContextCard() }
        }
    }

    @ViewBuilder
    private func content(_ card: AthleteContextCard, staleBecause staleness: Staleness?) -> some View {
        if let staleness {
            StaleBanner(staleness)
        }

        if card.isEmpty {
            Section {
                ContentUnavailableView {
                    Label("Nothing to summarise yet", systemImage: "person.text.rectangle")
                } description: {
                    Text("Lionheart builds this from your sessions, wellness entries, Oura days and injuries. Log a few and it fills in.")
                }
                .listRowBackground(Color.clear)
            }
        } else {
            briefing(card.today)
            tier(
                "Who you are",
                text: card.stableProfile,
                asOf: card.stableProfileAsOf,
                footer: "Slow-changing: your identity, your baselines, and trends over the last year.")
            tier(
                "Last four weeks",
                text: card.recentState,
                asOf: card.recentStateAsOf,
                footer: "Runs and body, recomputed as you log them. Fixed for a chat session once it opens.")
        }
    }

    /// The briefing, in the monospaced face it is written in: it is a dense block of figures the
    /// model reads line by line, and aligned digits are how the athlete checks it the same way.
    @ViewBuilder
    private func briefing(_ text: String) -> some View {
        Section {
            if let briefing = text.nonEmpty {
                Text(briefing)
                    .font(.footnote.monospaced())
                    .textSelection(.enabled)
            } else {
                Text("This server does not send the briefing yet.")
                    .foregroundStyle(Color(.secondaryLabel))
            }
        } header: {
            Text("With your next message")
        } footer: {
            Text("Read fresh for every message you send, so the model never guesses what today is, what you trained or how you slept.")
        }
    }

    @ViewBuilder
    private func tier(
        _ title: String, text: String, asOf: Date?, footer: String
    ) -> some View {
        Section {
            if let prose = text.nonEmpty {
                Text(prose)
                    .font(.callout)
                    .textSelection(.enabled)
            } else {
                Text("Nothing on record for this yet.")
                    .foregroundStyle(Color(.secondaryLabel))
            }
        } header: {
            Text(title)
        } footer: {
            if let asOf {
                Text("\(footer) Computed \(asOf.formatted(.relative(presentation: .named))).")
            } else {
                Text(footer)
            }
        }
    }

    private func unavailable(_ failure: LoadFailure) -> some View {
        ContentUnavailableView {
            Label("Not available", systemImage: "exclamationmark.triangle")
        } description: {
            Text(failure.message)
        } actions: {
            if failure.isWorthRetrying {
                Button("Try again") { Task { await store.refreshContextCard() } }
            }
        }
        .listRowBackground(Color.clear)
    }
}
