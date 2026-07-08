---
description: UX work on Lionheart, verified live in the browser via the Claude extension
argument-hint: [verify-url]
---

# Lionheart UX session

You are doing UX / frontend work on the Lionheart project (SvelteKit + Vite
frontend in `ClientApp/`, ASP.NET Core backend). The rules below apply for the
REST OF THIS SESSION, not just this one turn.

## Verification target

Target URL: $1

If the line above is empty, no URL was passed — use the default:

    http://localhost:5173

That is the Vite/SvelteKit dev server, which proxies API calls (`/swagger`,
`/login`, `/register`, and the API routes) to the ASP.NET backend on
`http://localhost:7025`. Use the target URL as the verification target for
every UX change for the rest of the session unless I give you a new one.

## Preflight — do this once, at the start

Before doing any work, do ONE quick load of the target URL in the browser to
confirm it's reachable. One hit only — no screenshot, no narration, no retry
loop.

- Loads OK -> reply `env reachable` and continue.
- Fails (connection refused, dev server down, backend not running, etc.) ->
  STOP, say so in one line, and ask me to start/restart the dev env. Do not
  retry repeatedly and do not start your own environment.

## Verify in the browser — don't just read the source

After any UX change, verify it against the RUNNING app using the Claude
browser extension: navigate to the target URL, look at the actual rendered
result, and confirm the change does what was intended. Do not conclude a UX
change is correct by reasoning about the code alone — actually look at the
page (screenshot / read the rendered DOM) before reporting success.

## The dev environment is mine, not yours

I am running the dev environment myself (the Vite dev server and the ASP.NET
backend) and viewing it at the target URL. Do NOT start your own Vite
instance, `dotnet run`, or tunnel. Assume the environment is already up and
just use the browser to inspect it. Only stand up your own environment if
there is a genuinely compelling reason — and if so, tell me why first and wait
for my go-ahead.

## When a restart is needed

The frontend hot-reloads. Pure frontend changes — Svelte components, styles,
client-side TypeScript, static assets — show up in the browser on a refresh
with NO restart. Just refresh the target URL and verify.

The ASP.NET backend does NOT hot-reload. If your work changes any backend
code — Endpoints, Services, Model, DTOs, DbContext / migrations, DI /
Program.cs / host config, or anything that must be recompiled — the running
environment is now stale and I must restart my backend before the browser
will reflect it.

When that happens, STOP and make this the most prominent thing in your reply,
with as few surrounding words as possible. Emit it on its own line, exactly:

    >>>>>> RESTART DEV <<<<<<

You may add ONE short line naming the backend file(s) that changed. Nothing
more — do not re-explain, do not re-screenshot, do not keep working. Wait for
me to confirm the restart is done, then re-verify in the browser. The point
of the bare signal is to keep token usage down, so keep it terse.

## Loop

1. Make the change.
2. Did it touch backend code?
   - Yes -> emit the RESTART DEV banner, name the file(s), and wait for me.
   - No (frontend only) -> refresh the target URL in the browser.
3. Look at the rendered result and report what you actually see, then iterate.
