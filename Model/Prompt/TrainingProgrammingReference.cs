using System.Text;

namespace lionheart.Services.AI
{
    /// <summary>
    /// Single-file reference doc injected into prompts.
    /// Keep this concise and practical; it’s appended as a system message.
    /// </summary>
    public static class TrainingProgrammingReference
    {
        public const string Text = @"
TRAINING PROGRAMMING REFERENCE

Scope
- This guide defines: RPE progression week-to-week, example weekly layouts for main/secondary lifts, when/why to use squat/bench/deadlift variations, and how to program accessories (incl. tricep pushdown, pendulum squat).
- Always respect program constraints and tool rules from the main system message.

A) RPE PROGRESSION (Main Lifts Only: Squat / Bench / Deadlift)
- Week 1: Baseline/technical. Top set ~RPE 6-7 (no grinding). Back-offs ~RPE 5.5-6.5.
- Week 2: +0.5 RPE vs Week 1 (same rep targets unless specified). Back-offs +0.5 as needed.
- Week 3: +1.0 RPE vs Week 1 (cap main top sets at RPE 9–9.5; no ugly reps). Back-offs +0.5–1.0 as needed.
- Optional Week 4 (deload/transition): −2.0 RPE from Week 1 targets (volume reduced 30–50%).
- If readiness is poor, keep the RPE target but reduce load accordingly. Do not overshoot targets.

B) EXAMPLE WEEKLY LAYOUTS (Use within frequency goals)
- Bench usually highest frequency; separate heavy hip/back stress on consecutive days.
- Templates (examples; pick one that matches user frequency and preferred weekdays):

1) 3-day S/B/D split (Mon/Wed/Fri):
  Day 1 (Mon): Squat — Top set (1–3 reps), 2–4 back-offs; Secondary: Bench (moderate); Accessories 2–4 @ RPE 8–9.
  Day 2 (Wed): Bench — Heavier exposure; Secondary: Squat (tech/volume) or lower-back-sparing hinge; Accessories.
  Day 3 (Fri): Deadlift — Top set (1–3), 2–4 back-offs; Secondary: Bench (skill/volume); Accessories.

2) 4-day S/B/D split (Mon/Tue/Thu/Sat):
  Day 1: Squat (strength) + Bench (skill)
  Day 2: Bench (volume) + Pull/Upper accessories
  Day 3: Deadlift (strength) + Bench (skill/volume)
  Day 4: Squat (volume/tech) or DL (tech), plus shoulders/triceps/back

3) Bench 3–4x/week, Squat 2–3x/week, Deadlift 1–2x/week:
  - Ensure at least one higher-intensity exposure and one lower-intensity/skill exposure for Bench.
  - Separate heavy Squat and heavy Deadlift days by ≥48h when possible.

Main Lift Structure (per session)
- First movement = main lift of the day.
- Include one Top Set (1–3 reps; highest RPE of the day for that lift).
- Then 2–4 back-off sets at reduced load (match or slightly increase reps for volume).
- Accessories 2–4 at RPE 8–9 (8–15 reps typical; 2–4 sets).

C) VARIATIONS: WHAT / WHY / WHEN (Use as movementModifier)
Use these to target weak points, add skill practice, or manage fatigue. Always set:
- movementModifier.name (e.g., Competition, Paused, Tempo 3-2-0)
- movementModifier.duration:
  • Competition: 0
  • Paused: number of pause seconds (e.g., 2)
  • Tempo a-b-c: a+b+c total seconds (e.g., 3+2+0 → 5)
- Include equipmentID and full equipment object from GetEquipmentsAsync.

SQUAT VARIATIONS
- Competition Squat: default when specificity is needed; duration=0.
- Paused Squat (1–3s): improve bottom control, reduce dive-bombing, build isometric strength.
- Tempo Squat (e.g., 3-2-0): groove patterning, increase time under tension, reduce absolute load to manage fatigue.
- High Bar / Safety Bar / Front Squat: shift emphasis to quads/upper back; helpful if low-back is fatigued.
When to include:
- If depth/in-the-hole control is limiting → Paused or Tempo.
- If torso strength is limiting → Front/SSB.
- If overall fatigue is high → use a variation that lowers systemic load (tempo/high bar) instead of comp heavy.

BENCH VARIATIONS
- Competition Bench: default for specificity.
- Paused Bench (1–2s on chest): standard for powerlifting; builds pause consistency and control.
- Tempo Bench (e.g., 3-1-0): control descent, improve bar path.
- Close-Grip / Spoto Bench (1–2s hover): triceps focus, mid-range control.
When to include:
- If losing tightness on chest → Paused.
- If mid-range slows → Close-Grip or Spoto.
- If technique inconsistency → Tempo.
- Keep at least one comp-style exposure per week when possible.

DEADLIFT VARIATIONS
- Competition Deadlift: default specificity (conventional or sumo per lifter).
- Paused Deadlift (1–2s at shin/knee): solve off-the-floor or knee-passing issues.
- Tempo Deadlift (e.g., 3-0-0): reinforce position; manage fatigue with lower absolute load.
- RDL: hinge strength, hamstrings; moderate loads, higher reps.
When to include:
- Off-the-floor → Paused at shin.
- Low-back fried → choose RDL (moderate), paused singles (lighter), or technique day instead of heavy comp.

D) ACCESSORY PROGRAMMING (General Rules)
- Choose 2–4 that support the main lift or address weak links; RPE 8–9 unless stated.
- Sets/Reps: 2–4 sets × 5-10 reps
- Progression week-to-week:
- Keep accessories joint-friendly; avoid hammering the same joint patterns on consecutive days.

Accessory Examples (specifically requested):
1) Tricep Pushdown
  - Goal: triceps strength for bench lockout and pressing density.
  - Prescription: 3–4 sets × 8-10 reps @ RPE 8–9.
  - Variants: straight bar, rope, single-arm. Keep shoulders down/ribs stacked; full elbow extension each rep.

2) Pendulum Squat
  - Goal: quad hypertrophy with reduced spinal loading; great on Squat or Deadlift volume days.
  - Prescription: 3–4 sets × 6-8 reps @ RPE 8–9.
  - Placement: pair on days without a heavy deadlift or squat the next day to avoid quad/hip overlap fatigue.

E) WEEKDAY ANCHOR + IDs + EQUIPMENT (Implementation Reminders)
- Week 1 selects exact weekdays; Weeks 2–3 must reuse the same weekdays (+7d, +14d).
- Main lift first; 3–5 total movements per session; accessories at RPE 8–9.
- Use only UUIDs from tool calls for movementBaseID and equipmentID; include full equipment object.
- movementModifier:
  • Competition → name=Competition, duration=0
  • Paused → name=Paused, duration=pauseSeconds
  • Tempo a-b-c → name=Tempo a-b-c, duration=a+b+c
";
    }
}
