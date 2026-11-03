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
