import Foundation

/// What a signed-in athlete is allowed to see, which is not the same question as whether they are
/// signed in.
///
/// An Identity account and a Lionheart profile are separate records. Without the second one every
/// training *write* is refused with a 409 while every read answers 200 with nothing in it — so the
/// app works, shows empty screens forever, and explains none of it. The gate exists so that case
/// is a state the root switches on rather than a surprise the athlete meets at the first thing they
/// try to log.
///
/// `.unreachable` is the case that earns its keep. If the boot read fails there is no honest answer
/// to "does this athlete have a profile", and both other branches are wrong: the tabs would load
/// into failures, and the onboarding form would offer to create a profile that may already exist.
/// Saying so and offering a retry is the only truthful screen.
enum ProfileGate: Equatable, Sendable {
    /// The boot read has not answered yet. A case rather than defaulting to `.absent`, so the
    /// onboarding form does not flash on the launch of an app that is fully set up.
    case undetermined
    case absent
    case present(displayName: String)
    case unreachable(LoadFailure)

    /// Whether the five tabs may draw. Total, so a case added later has to decide rather than
    /// falling into "yes" and showing an athlete screens that cannot work.
    var allowsTraining: Bool {
        switch self {
        case .present: true
        case .undetermined, .absent, .unreachable: false
        }
    }

    /// Where the gate goes when a boot read finishes.
    ///
    /// A failure never overwrites a settled answer: once the profile is known to be present, a
    /// later refresh that cannot reach the server must not throw the athlete back out of the app
    /// they are already using.
    func applying(_ result: Result<AthleteProfileBoot, LoadFailure>) -> ProfileGate {
        switch result {
        case .success(let boot):
            return boot.hasProfile ? .present(displayName: boot.name) : .absent
        case .failure(let failure):
            switch self {
            case .present, .absent: return self
            case .undetermined, .unreachable: return .unreachable(failure)
            }
        }
    }
}

/// The three facts the athlete states about themselves to get a profile.
///
/// A value rather than three properties on the store, because they are one submission: the request
/// carries exactly these and nothing is valid on its own.
struct ProfileDraft: Equatable, Sendable {
    let displayName: String
    let age: Int
    /// Unitless. The server's column is a bare `float` with no unit anywhere behind it, so the
    /// client carries the number the athlete typed and claims nothing about what it measures.
    let weight: Double
}

/// What is wrong with what has been typed so far.
///
/// The bounds are deliberately wide. The server accepts an age of zero and a weight of zero, so
/// these are the client refusing to send a number that would read as nonsense on the context card
/// the model is handed — not the client second-guessing a server rule. Anything the server would
/// plausibly accept, this accepts.
enum ProfileDraftProblem: Equatable, Sendable, Hashable {
    case nameMissing
    case ageImplausible
    case weightImplausible

    /// Lowest and highest ages a person can be training at. Loose on purpose: the point is to
    /// catch a blank or a typo, not to have an opinion about masters athletes.
    static let plausibleAges = 10...120
    /// In whatever unit the athlete logs in, which covers kilograms and pounds in one range.
    static let plausibleWeights = 20.0...700.0

    var message: String {
        switch self {
        case .nameMissing: "Give a name the coach can call you by."
        case .ageImplausible:
            "An age between \(Self.plausibleAges.lowerBound) and \(Self.plausibleAges.upperBound)."
        case .weightImplausible: "A body weight, in whatever unit you log your lifts in."
        }
    }
}

/// Whether the onboarding form has enough to send.
///
/// A state rather than a thrown refusal because the form reads it continuously as the athlete
/// types — to enable the button and to decide which field to annotate — and a failure that is read
/// rather than propagated is a value.
enum ProfileDraftState: Equatable, Sendable {
    case incomplete([ProfileDraftProblem])
    case ready(ProfileDraft)

    var draft: ProfileDraft? {
        switch self {
        case .incomplete: nil
        case .ready(let draft): draft
        }
    }

    var problems: [ProfileDraftProblem] {
        switch self {
        case .incomplete(let problems): problems
        case .ready: []
        }
    }

    func has(_ problem: ProfileDraftProblem) -> Bool { problems.contains(problem) }

    /// Reads the typed text. Total: every combination of inputs produces a state, and the numbers
    /// arrive as text because that is what a text field holds — an empty field is a missing number,
    /// not a zero.
    static func of(displayName: String, age: String, weight: String) -> ProfileDraftState {
        let name = displayName.trimmingCharacters(in: .whitespacesAndNewlines)
        let parsedAge = Int(age.trimmingCharacters(in: .whitespaces))
        let parsedWeight = Double(
            weight.trimmingCharacters(in: .whitespaces).replacingOccurrences(of: ",", with: "."))

        var problems: [ProfileDraftProblem] = []
        if name.isEmpty { problems.append(.nameMissing) }
        if parsedAge.map({ !ProfileDraftProblem.plausibleAges.contains($0) }) ?? true {
            problems.append(.ageImplausible)
        }
        if parsedWeight.map({ !($0.isFinite && ProfileDraftProblem.plausibleWeights.contains($0)) }) ?? true {
            problems.append(.weightImplausible)
        }

        guard problems.isEmpty, let parsedAge, let parsedWeight else { return .incomplete(problems) }
        return .ready(ProfileDraft(displayName: name, age: parsedAge, weight: parsedWeight))
    }
}

/// Where a profile-creation attempt stands.
///
/// The same shape as `SignInState`, for the same reason: three mutually exclusive states, and the
/// form disables itself in exactly one of them.
enum ProfileCreationState: Equatable, Sendable {
    case editing
    case submitting
    case refused(LoadFailure)

    var isSubmitting: Bool {
        switch self {
        case .submitting: true
        case .editing, .refused: false
        }
    }

    var failure: LoadFailure? {
        switch self {
        case .editing, .submitting: nil
        case .refused(let failure): failure
        }
    }
}
