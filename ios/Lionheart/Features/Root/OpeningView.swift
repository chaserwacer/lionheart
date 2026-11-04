import SwiftUI

/// The moment between the launch screen and the app: the bars rise one after another into the
/// lockup, the wordmark settles under them, the tagline follows, and the app shows through.
///
/// It plays once per launch, over the app rather than before it: the root underneath is already
/// reading the device's last answers and asking the server, so the opening never holds anything up
/// — it is under a second, and lets every touch through to nothing until it has gone.
///
/// The first frame is the launch screen's: Graphite, and the mark at the size and place
/// `UILaunchScreen` draws `LaunchMark`, so the hand-over has no seam. The last frame puts the lockup
/// where sign-in draws its own (`lockupFrame`), so on a signed-out launch only the form appears; signed
/// in, the lockup rests a little above centre and the app fades up through it. With Reduce Motion
/// nothing travels: the launch mark cross-fades into the lockup, and the lockup into the app.
struct OpeningView: View {
    /// Where sign-in has laid out its lockup, in screen points, when sign-in is what is underneath.
    let lockupFrame: CGRect?
    /// Called once the app is fully showing, to take this off the screen.
    let finished: () -> Void

    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    @State private var barsRisen = [false, false, false]
    @State private var showsWordmark = false
    @State private var showsTagline = false
    /// Reduce Motion's cross-fade, from the launch mark to the lockup.
    @State private var showsLockup = false
    @State private var isLeaving = false

    var body: some View {
        GeometryReader { proxy in
            let launchMarkTop = (proxy.size.height - BrandLockup.markSide) / 2
            let lockupTop = lockupFrame?.minY ?? proxy.size.height * 0.36 - BrandLockup.markSide / 2
            ZStack(alignment: .top) {
                Tokens.graphite
                if reduceMotion {
                    BrandMark()
                        .frame(width: BrandLockup.markSide, height: BrandLockup.markSide)
                        .frame(maxWidth: .infinity)
                        .offset(y: launchMarkTop)
                        .opacity(showsLockup ? 0 : 1)
                    BrandLockup()
                        .frame(maxWidth: .infinity)
                        .offset(y: lockupTop)
                        .opacity(showsLockup ? 1 : 0)
                } else {
                    BrandLockup(
                        barOffsets: barsRisen.map { $0 ? 0 : launchMarkTop - lockupTop },
                        showsWordmark: showsWordmark,
                        showsTagline: showsTagline)
                        .frame(maxWidth: .infinity)
                        .offset(y: lockupTop)
                }
            }
        }
        .ignoresSafeArea()
        .environment(\.colorScheme, .dark)
        // Faded as one picture: faded layer by layer, the ground would thin under the lockup and
        // the lockup dim over sign-in's identical one as it leaves.
        .compositingGroup()
        .opacity(isLeaving ? 0 : 1)
        .allowsHitTesting(false)
        .accessibilityHidden(true)
        .task { await play() }
    }

    /// The choreography, in seconds from the first frame. Every step is an animation the system
    /// interpolates; the sleeps only place them.
    private func play() async {
        // A beat on the launch screen's own frame, which is also when sign-in lays out underneath.
        guard await pause(0.1) else { return }
        if reduceMotion {
            withAnimation(.easeInOut(duration: 0.3)) { showsLockup = true }
            guard await pause(0.55) else { return }
        } else {
            for bar in 0..<3 {
                withAnimation(.spring(duration: 0.42, bounce: 0.14).delay(Double(bar) * 0.05)) {
                    barsRisen[bar] = true
                }
            }
            withAnimation(.easeOut(duration: 0.28).delay(0.3)) { showsWordmark = true }
            withAnimation(.easeOut(duration: 0.24).delay(0.42)) { showsTagline = true }
            guard await pause(0.64) else { return }
        }
        withAnimation(.easeIn(duration: 0.22)) { isLeaving = true }
        guard await pause(0.22) else { return }
        finished()
    }

    /// Sleeps, answering whether the opening is still on screen to carry on.
    private func pause(_ seconds: Double) async -> Bool {
        (try? await Task.sleep(for: .seconds(seconds))) != nil
    }
}
