import SwiftUI

/// Creating the Lionheart profile every training write depends on.
///
/// A `Form`, not a `List`: it collects three facts and submits them, which is what `Form` and its
/// stock controls are for. The copy says what the profile is *for* rather than that a field is
/// required — the athlete has just signed in and has no reason to know an Identity account and a
/// Lionheart profile are two records.
struct OnboardingView: View {
    let store: ProfileStore

    @State private var displayName = ""
    @State private var age = ""
    @State private var weight = ""
    @FocusState private var focused: Field?

    private enum Field: Hashable {
        case displayName, age, weight
    }

    private var draftState: ProfileDraftState {
        ProfileDraftState.of(displayName: displayName, age: age, weight: weight)
    }

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    Text("Tempo records what you did and reads it against what your body is doing. It needs a few things about you before it can log anything.")
                        .font(.subheadline)
                        .foregroundStyle(Color(.secondaryLabel))
                }

                // `LabeledContent` rather than a placeholder-only `TextField`: a placeholder is
                // gone the moment there is a value in the field, which leaves a filled form
                // showing three numbers with nothing saying which is which.
                Section {
                    LabeledContent("Name") {
                        TextField("", text: $displayName)
                            .multilineTextAlignment(.trailing)
                            .textContentType(.name)
                            .textInputAutocapitalization(.words)
                            .autocorrectionDisabled()
                            .focused($focused, equals: .displayName)
                            .submitLabel(.next)
                            .onSubmit { focused = .age }
                    }
                    problem(.nameMissing)

                    LabeledContent("Age") {
                        TextField("", text: $age)
                            .multilineTextAlignment(.trailing)
                            .keyboardType(.numberPad)
                            .focused($focused, equals: .age)
                    }
                    problem(.ageImplausible)

                    LabeledContent("Body weight") {
                        TextField("", text: $weight)
                            .multilineTextAlignment(.trailing)
                            .keyboardType(.decimalPad)
                            .focused($focused, equals: .weight)
                    }
                    problem(.weightImplausible)
                } header: {
                    Text("About you")
                } footer: {
                    // The server's column is a bare number with no unit behind it, so the honest
                    // instruction is to be consistent rather than to name a unit the domain does
                    // not have.
                    Text("Weight is stored as a plain number — use the same unit you log your lifts in.")
                }

                if let failure = store.creation.failure {
                    Section {
                        Label(failure.message, systemImage: "exclamationmark.triangle")
                            .foregroundStyle(.red)
                            .font(.footnote)
                    }
                }

                Section {
                    Button {
                        submit()
                    } label: {
                        HStack {
                            if store.creation.isSubmitting { ProgressView() }
                            Text(store.creation.isSubmitting ? "Setting you up" : "Start training")
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .disabled(draftState.draft == nil || store.creation.isSubmitting)
                }
            }
            .navigationTitle("Welcome")
            .onChange(of: draftState) { store.resumeEditing() }
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    SignOutButton()
                }
            }
        }
    }

    /// The inline hint for one field, shown only once the athlete has typed something there.
    ///
    /// Annotating an untouched field is how a welcome screen greets somebody with three errors.
    @ViewBuilder
    private func problem(_ problem: ProfileDraftProblem) -> some View {
        if draftState.has(problem), hasBeenTyped(problem) {
            Text(problem.message)
                .font(.caption)
                .foregroundStyle(Color(.secondaryLabel))
        }
    }

    private func hasBeenTyped(_ problem: ProfileDraftProblem) -> Bool {
        switch problem {
        case .nameMissing: !displayName.isEmpty
        case .ageImplausible: !age.isEmpty
        case .weightImplausible: !weight.isEmpty
        }
    }

    private func submit() {
        guard let draft = draftState.draft else { return }
        focused = nil
        Task { await store.createProfile(draft) }
    }
}

/// Signing out from a screen that is not inside the tabs.
///
/// It exists because the gate can strand somebody: an account signed in on a server that has no
/// profile for it, or the wrong account entirely, with Settings — and its sign-out — behind the
/// tabs this screen is standing in front of.
private struct SignOutButton: View {
    @Environment(AuthStore.self) private var auth
    @State private var isConfirming = false

    var body: some View {
        Button("Sign out") { isConfirming = true }
            .confirmationDialog("Sign out of Tempo?", isPresented: $isConfirming) {
                Button("Sign out", role: .destructive) { Task { await auth.signOut() } }
            }
    }
}
