# Getting Lionheart onto a phone

Everything in `docs/ios-architecture.md` assumes the simulator. This file is the part that needs a
real device, an Apple ID, and a few decisions only the owner can make.

Two things have to be true at once: the phone has to trust the build, and the build has to be able
to reach the backend. They are independent, and both have a free answer.

## 1. Signing: free Apple ID or the paid program

| | Free Apple ID | Apple Developer Program ($99/year) |
|---|---|---|
| Cost | nothing | $99/year |
| Build lifetime on device | **7 days**, then it refuses to launch | 1 year |
| Install method | Xcode, phone plugged in or on the same network | Xcode, or TestFlight over the air |
| Apps at once | 3 | unlimited |
| Re-signing | re-run from Xcode weekly | only when the build expires |

**Decided 2026-11-03 (owner): stay on the free tier.** Reinstalling weekly is acceptable, and a
personal app installed from the machine it is built on is exactly what the free tier is for. The
$99 would buy TestFlight (a build over the air, no cable) and a 1-year lifetime instead of seven
days.

Two consequences that bind any future work, so they are recorded here rather than rediscovered:

- **Sign in with Apple is out.** The capability cannot be enabled on a free membership — it
  cannot even be tested — so authentication stays ASP.NET Identity with bearer tokens in the
  Keychain. That already means one password entry per install rather than per launch, which is
  most of what Apple sign-in would have bought.
- **Nothing may depend on a paid-only capability.** Push notifications, APNs-driven Live Activity
  updates, and associated domains are all off the table. A Live Activity updated *locally* by the
  app is fine; one that needs a push to change is not.

### The free path, start to finish

1. In Xcode, **Settings → Accounts**, add the Apple ID. Xcode creates a "Personal Team" from it.
2. Find the team id:
   ```bash
   security find-identity -v -p codesigning
   ```
   The ten-character string in the certificate name is the team id. Xcode's **Signing &
   Capabilities** tab shows the same thing.
3. Put it in `ios/Configs/Local.xcconfig` (copy `Local.xcconfig.example`; the file is gitignored for
   the same reason `.env` is):
   ```
   DEVELOPMENT_TEAM = ABCDE12345
   ```
4. Regenerate and open:
   ```bash
   cd ios && xcodegen generate && open Lionheart.xcodeproj
   ```
5. Plug the phone in, pick it as the run destination, and run. The first launch needs
   **Settings → General → VPN & Device Management → Developer App → Trust** on the phone.

`CODE_SIGN_STYLE` is already `Automatic`, so Xcode provisions the bundle id itself. Nothing else in
`project.yml` needs changing, and the team id must not be committed.

### If the owner pays

TestFlight needs an App Store Connect record and a build uploaded with `xcodebuild -exportArchive`
or Xcode's Organizer. That is worth setting up only if installing over the air matters more than the
$99; nothing in the app requires it.

## 2. Reaching the backend

The simulator talks to the Mac's loopback directly. **A phone cannot** — `localhost` on the phone is
the phone. The app reads its origin from the `LH_API_BASE_URL` build setting, so this is a config
change, never a code change. Note the `$(LH_SLASH)` trick: two slashes start a comment in an xcconfig
file, so the URL has to be assembled (`Base.xcconfig` explains it).

### Same Wi-Fi as the Mac — the simple case

Find the Mac's address and serve on it:

```bash
ipconfig getifaddr en0
dotnet run --launch-profile lionheart --urls http://0.0.0.0:7025
```

The default launch profile binds loopback only, so `--urls http://0.0.0.0:7025` is the part that
matters — without it the phone gets a connection refused even with the right address.

```
LH_API_BASE_URL = http:$(LH_SLASH)$(LH_SLASH)192.168.1.10:7025
```

Plain HTTP works here because `project.yml` declares `NSAllowsLocalNetworking`, the narrow App
Transport Security exemption for loopback and private-range addresses. It deliberately does **not**
declare `NSAllowsArbitraryLoads`, which would also permit plain HTTP in a Release build. So this
works on the home network and correctly refuses to work for a public HTTP host.

The address changes when DHCP reassigns it, which is the main irritation of this path.

### Away from the Mac's network: Tailscale

**Decided 2026-11-03 (owner): the Mac stays at home and awake, and the phone reaches it over
Tailscale.** The Mac and the phone join the same private network, nothing is exposed publicly, and
the app is usable at the gym. Rejected alternatives:
- **ngrok**: a public URL in front of a personal training database and an Anthropic key, and its
  basic auth collides with the app's own bearer token.
- **Hosting the backend elsewhere**: needed only if the Mac could not stay on. It would also mean
  moving embeddings off LM Studio.

A Tailscale address is in `100.64.0.0/10`, which is *not* a private range as far as App Transport
Security is concerned. So the phone uses Tailscale's own HTTPS rather than an ATS exemption:

1. Install Tailscale on the Mac and the phone and sign both in to the same account. In the admin
   console's DNS page, turn on MagicDNS and HTTPS certificates.
2. On the Mac, put the backend behind Tailscale's HTTPS:
   ```bash
   /Applications/Tailscale.app/Contents/MacOS/Tailscale serve --bg 7025
   ```
   It prints the address, `https://<machine>.<tailnet>.ts.net`, and keeps serving across restarts.
3. Point the build at it in `Local.xcconfig`, then rebuild and reinstall:
   ```
   LH_API_BASE_URL = https:$(LH_SLASH)$(LH_SLASH)<machine>.<tailnet>.ts.net
   ```

That one address works at home and away, as long as Tailscale is connected on the phone. Leave
**Funnel** off: it would publish the server to the open internet, where Serve keeps it to the
devices on the tailnet.

The service below binds to loopback only, so once the phone goes through Tailscale the plain-HTTP
same-Wi-Fi path above no longer reaches it. That is deliberate: nothing else on the home network
can reach the server, and no password crosses the Wi-Fi unencrypted. The same-Wi-Fi recipe is still
how to point a phone at a `dotnet run` backend during development.

### Keeping the backend running

A server started from a terminal dies with the terminal. `scripts/home-server.sh` runs it as a
per-user launchd service instead, which starts at login and restarts if it exits:

```bash
scripts/home-server.sh deploy   # publish what is checked out and (re)start the service
scripts/home-server.sh status   # running or not, and the last log lines
scripts/home-server.sh stop
```

- **Where it runs.** It runs in the main checkout, on its `Data/lionheart.db` and `.env`, whichever
  worktree the script is called from.
- **Deploying new code.** It serves the checked-out code as of the last `deploy`, so after merging
  into `dev`, update the main checkout and deploy again.
- **Environment.** It runs as Production, so it neither regenerates the web client nor starts the
  Svelte dev server. The published build includes the built web app, so the web app is served on
  the same port.
- **Logs.** They go to `~/Library/Logs/Lionheart/backend.log`.

## 3. First run on a new phone

The database is whatever the backend is pointed at, so a phone talking to the dev backend sees the
dev data. A genuinely new account needs an Identity account and a Lionheart profile — two calls, and
the second is easy to miss. The recipe is in `docs/ios-architecture.md` → "Running it against the
backend".

## What to check when it does not work

| Symptom | Cause |
|---|---|
| App installs, refuses to launch, no message | Free-tier build older than 7 days. Re-run from Xcode. |
| "Untrusted Developer" | The Trust step on the phone, above. |
| Requests all fail, simulator is fine | `LH_API_BASE_URL` still points at `localhost`. |
| Right address, connection refused | Backend bound to loopback. Add `--urls http://0.0.0.0:7025`. |
| Worked yesterday, not today | DHCP moved the Mac's address. |
| Signs in, signed out next launch | A Keychain write failed. Settings surfaces this; it means signing is misconfigured, since the entitlement is what makes Keychain work. |
| 409 on everything after signing in | Identity account with no Lionheart profile. See above. |
