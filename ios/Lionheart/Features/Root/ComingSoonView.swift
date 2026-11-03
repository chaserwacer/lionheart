import SwiftUI

/// A tab that exists so the shell is the real shape, and says plainly what is coming.
///
/// `ContentUnavailableView` rather than a hand-drawn empty state: it is Apple's own answer to "this
/// screen has nothing in it yet", and it is already right in both appearances and at every Dynamic
/// Type size. Saying what the screen will do beats a shrug, and it tells the next agent where their
/// work lands.
struct ComingSoonView: View {
    let title: String
    let question: String
    let systemImage: String
    let whatIsComing: String

    var body: some View {
        NavigationStack {
            ContentUnavailableView {
                Label(question, systemImage: systemImage)
            } description: {
                Text(whatIsComing)
            }
            .navigationTitle(title)
        }
    }
}

struct HistoryView: View {
    var body: some View {
        ComingSoonView(
            title: "History",
            question: "What have I done?",
            systemImage: "list.bullet.rectangle",
            whatIsComing: "The session log, personal records and trends over time.")
    }
}

struct CoachView: View {
    var body: some View {
        ComingSoonView(
            title: "Coach",
            question: "What does it mean?",
            systemImage: "bubble.left.and.text.bubble.right",
            whatIsComing: "The chat, and the analyses it draws on.")
    }
}

struct BodyView: View {
    var body: some View {
        ComingSoonView(
            title: "Body",
            question: "What is my body doing?",
            systemImage: "heart.text.square",
            whatIsComing: "Oura days, wellness entries and injuries.")
    }
}
