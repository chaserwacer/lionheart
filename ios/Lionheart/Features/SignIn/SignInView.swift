import SwiftUI

/// Signing in against ASP.NET Identity.
///
/// A `Form` with stock fields and nothing else: this is the first screen of the app, and the
/// strongest statement that it is native is that it looks exactly like every other sign-in sheet
/// on the phone. There is no registration here — accounts are created on the web app, and a second
/// path to the same place would be surface nobody asked for.
struct SignInView: View {
    let auth: AuthStore

    @State private var email = ""
    @State private var password = ""
    @FocusState private var focus: Field?

    private enum Field: Hashable {
        case email, password
    }

    var body: some View {
        NavigationStack {
            Form {
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
                    Button {
                        submit()
                    } label: {
                        if auth.signIn.isSubmitting {
                            HStack(spacing: 8) {
                                ProgressView()
                                Text("Signing in")
                            }
                        } else {
                            Text("Sign in")
                        }
                    }
                    .disabled(!isSubmittable)
                }
            }
            .navigationTitle("Lionheart")
            .onChange(of: email) { _, _ in auth.resumeEditing() }
            .onChange(of: password) { _, _ in auth.resumeEditing() }
            // Deliberately no autofocus. Raising the keyboard before the athlete has seen the
            // screen hides the form behind it, and this is the first thing the app ever shows.
        }
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
