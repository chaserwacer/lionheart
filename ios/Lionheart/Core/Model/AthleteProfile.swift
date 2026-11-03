import Foundation

/// What the server says about the signed-in identity's Lionheart profile.
///
/// `BootUserDTO` on the wire, and the name it carries means two different things depending on the
/// flag: the profile's display name once there is a profile, and the Identity user name — in
/// practice the email address — before there is one. So `displayName` is only read where
/// `hasProfile` is true, and the gate below is what enforces that rather than a comment asking
/// callers to remember it.
struct AthleteProfileBoot: Equatable, Sendable {
    let name: String
    let hasProfile: Bool
}

extension AthleteProfileBoot: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            name: container.lenient("name", or: ""),
            hasProfile: container.lenient("hasCreatedProfile", or: false))
    }
}

/// The profile a `POST /api/user/create-profile` created.
///
/// Only the three facts the athlete stated are read back. The route answers with the whole
/// `LionheartUser` entity, which carries the Identity user — password hash included — and nine
/// empty navigation collections; decoding the three members that mean something keeps that out of
/// the device entirely.
struct AthleteProfile: Equatable, Sendable {
    let displayName: String
    let age: Int
    /// Unitless, as the server stores it: there is no unit column behind this number anywhere in
    /// the domain, so the client must not claim one.
    let weight: Double
}

extension AthleteProfile: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            displayName: container.lenient("name", or: ""),
            age: container.lenient("age", or: 0),
            weight: container.lenient("weight", or: 0))
    }
}
