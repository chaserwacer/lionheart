/**
 * The interpretation engine's two routes.
 *
 * Hand-written for the same reason as `training.ts`: the OpenAPI document describes
 * `InterpretationOutcome` as `{}` and `DraftEntry` as its base members only, so the generated
 * `ParsedInterpretation.toJSON()` would strip `$type` and every subtype member off the draft. That
 * matters more here than anywhere else — `/confirm` works by the client echoing the draft back
 * *unmodified*, and a mutilated echo is a second parse the athlete pays for.
 */

import { send, type Result } from './result';

/**
 * The draft, opaque by design. It is never read, edited or rebuilt on the client: it is received
 * as JSON and handed straight back, which is what keeps the resume stateless (§3) and keeps key
 * order intact — System.Text.Json needs `$type` to arrive first.
 */
export type InterpretationDraft = Readonly<Record<string, unknown>>;

/** 0 = MovementBase, 1 = Equipment. Mirrors `ReferenceKind`. */
export type ReferenceKind = 0 | 1;

export const REFERENCE_KIND_LABELS: Readonly<Record<ReferenceKind, string>> = {
	0: 'movement',
	1: 'equipment'
};

export type EntityMatch = {
	readonly entityId: string;
	readonly name: string;
	/** The resolver's ranking, in [0, 1]. Candidates arrive already ordered. */
	readonly confidence: number;
};

export type UnresolvedReference = {
	readonly rawName: string;
	readonly kind: ReferenceKind;
	readonly candidates: readonly EntityMatch[];
};

/** What the athlete decided about one name. A null id means "none of these — create it". */
export type ReferenceChoice = {
	readonly rawName: string;
	readonly kind: ReferenceKind;
	readonly chosenEntityId: string | null;
};

/**
 * Where a run ended. Both cases are 200s: being asked which name was meant is an outcome of a
 * request that was understood, not a refusal.
 */
export type InterpretationOutcome =
	| {
			readonly $type: 'committed';
			readonly sessionIds: readonly string[];
			readonly contextIds: readonly string[];
			readonly warnings: readonly string[];
	  }
	| {
			readonly $type: 'awaitingConfirmation';
			readonly draft: InterpretationDraft;
			readonly references: readonly UnresolvedReference[];
	  };

export function interpret(
	rawText: string,
	loggedAt: Date,
	signal?: AbortSignal
): Promise<Result<InterpretationOutcome>> {
	return send('/api/interpretation/interpret', {
		method: 'POST',
		body: { rawText, loggedAt: loggedAt.toISOString() },
		signal
	});
}

/**
 * Resumes a run with the athlete's choices. The draft goes back exactly as it came: answering only
 * some of the names is legitimate, and the reply is another outcome — often awaiting the rest.
 */
export function confirm(
	draft: InterpretationDraft,
	choices: readonly ReferenceChoice[],
	signal?: AbortSignal
): Promise<Result<InterpretationOutcome>> {
	return send('/api/interpretation/confirm', {
		method: 'POST',
		body: { draft, choices },
		signal
	});
}
