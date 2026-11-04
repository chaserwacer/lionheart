import SwiftUI

/// Signing in against ASP.NET Identity, under Tempo's lockup.
///
/// The first screen of the app is its one brand moment: the lockup on Graphite, in both appearances,
/// continuing the launch screen and the opening. Everything below it is stock — a `Form`, the
/// system's fields with their content types so Password AutoFill offers the saved account, and the
/// keyboard's own Next and Go — because the strongest statement that it is native is that signing in
/// works exactly as it does everywhere else on the phone. There is no registration here — accounts
/// are created on the web app, and a second path to the same place would be surface nobody asked for.
struct SignInView: View {
    let auth: AuthStore
    /// Told where the lockup is drawn, in screen points, so the opening can end on it.
    var lockupPlaced: (CGRect) -> Void = { _ in }

    @State private var email = ""
    @State private var password = ""
    @FocusState private var focus: Field?

    private enum Field: Hashable {
        case email, password
    }

    var body: some View {
        Form {
            Section {
                BrandLockup()
                    .frame(maxWidth: .infinity)
                    .onGeometryChange(for: CGRect.self) { $0.frame(in: .global) } action: { lockupPlaced($0) }
                    .padding(.top, 36)
                    .padding(.bottom, 20)
                    .listRowBackground(Color.clear)
                    .listRowInsets(EdgeInsets())
            }

            Section {
                TextField("Email", text: $email)
                    .textContentType(.username)
                    .keyboardType(.emailAddress)
                    .textInputAutocapitalization(.never)
                    .autocorrectionDisabled()
                    .focused($focus, equals: .email)
                    .submitLabel(.next)
                    .onSubmit { focus = .password }

                SecureField("Password", text: $password)
                    .textContentType(.password)
                    .focused($focus, equals: .password)
                    .submitLabel(.go)
                    .onSubmit { submit() }
            } footer: {
                if let failure = auth.signIn.failure {
                    Text(failure.message)
                        .foregroundStyle(.red)
                }
            }

            Section {
                Button(action: submit) {
                    HStack(spacing: 8) {
                        if auth.signIn.isSubmitting {
                            ProgressView()
                            Text("Signing in")
                        } else {
                            Text("Sign in")
                        }
                    }
                    .font(.headline)
                    .frame(maxWidth: .infinity)
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.large)
                .disabled(!isSubmittable)
                .listRowBackground(Color.clear)
                .listRowInsets(EdgeInsets())
            } footer: {
                Text("Tempo Training Systems")
                    .font(.footnote)
                    .frame(maxWidth: .infinity)
                    .padding(.top, 28)
            }
        }
        .scrollContentBackground(.hidden)
        .background(Tokens.graphite)
        // Dark whatever the phone's appearance, so the ground, the fields, the status bar and the
        // keyboard all belong to the Graphite of the launch screen.
        .preferredColorScheme(.dark)
        .onChange(of: email) { _, _ in auth.resumeEditing() }
        .onChange(of: password) { _, _ in auth.resumeEditing() }
        // Deliberately no autofocus. Raising the keyboard before the athlete has seen the screen
        // hides the form behind it, and this is the first thing the app ever shows.
    }

    private var isSubmittable: Bool {
        Credentials.areSubmittable(email: email, password: password) && !auth.signIn.isSubmitting
    }

    private func submit() {
        guard isSubmittable else { return }
        focus = nil
        Task { await auth.submitSignIn(email: email, password: password) }
    }
}
