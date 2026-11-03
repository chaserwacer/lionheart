import Foundation

/// The profile routes — the two calls that stand between a registered account and a usable app.
///
/// An Identity account and a Lionheart profile are separate records, and every training *write*
/// refuses an account without the second one: `POST /api/training/sessions/completed` answers 409,
/// "This account has no Lionheart profile yet." The reads do not — they answer 200 with an empty
/// list — so a profile-less athlete sees a working, permanently empty app and no explanation. That
/// asymmetry is why the client asks `has-created-profile` on boot instead of waiting to be refused.
struct UserAPI: Sendable {
    let client: APIClient

    /// Whether this identity has a Lionheart profile, and the name to greet them by if it does.
    func profileBoot() async throws -> AthleteProfileBoot {
        try await client.get("/api/user/has-created-profile")
    }

    /// The same answer, remembered: it decides whether the tabs exist at all, so a launch with no
    /// signal has to be able to answer it from what the server said last.
    func profileBoot(remembering key: ReadKey) async throws -> Remembered<AthleteProfileBoot> {
        try await client.get("/api/user/has-created-profile", remembering: key)
    }

    /// The last answer kept under `key`, without asking the server.
    func recalledProfileBoot(_ key: ReadKey) -> AthleteProfileBoot? {
        client.recall(key, as: AthleteProfileBoot.self)?.value
    }

    /// Creates the profile. Answers 201 with the new profile, or 409 if one already exists — which
    /// is the race between two devices and reads as "you already have one", not as an error.
    func createProfile(_ draft: ProfileDraft) async throws -> AthleteProfile {
        try await client.post("/api/user/create-profile", body: CreateProfileBody(draft))
    }
}

/// `CreateProfileRequest`: the three facts the athlete states about themselves.
///
/// `weight` is a `float` on the server with no unit column anywhere behind it, so it is sent as the
/// plain number the athlete typed. Nothing here invents a unit the domain does not have.
private struct CreateProfileBody: Encodable, Sendable {
    let displayName: String
    let age: Int
    let weight: Double

    init(_ draft: ProfileDraft) {
        displayName = draft.displayName
        age = draft.age
        weight = draft.weight
    }
}

/// The athlete context card route.
///
/// Separate from `UserAPI` because it is not about the account: it is the analysis layer's standing
/// summary, and it is the Chat screen that reads it.
struct ProfileAPI: Sendable {
    let client: APIClient

    /// The card the chat model is given about this athlete.
    ///
    /// The server materialises a missing card and refreshes a stale one on read, and each rebuild
    /// attempts a narrative sentence from the cheap model — so this call is occasionally slow, and
    /// where that model is a local server serving one request at a time it must not be made while a
    /// chat exchange is in flight. `ChatStore` is what holds that order.
    func contextCard() async throws -> AthleteContextCard {
        try await client.get("/api/profile/context-card")
    }
}
