/**
 * The training-session routes, typed by hand.
 *
 * `ApiClient.ts` is generated and must not be edited, but it cannot be used for these: the OpenAPI
 * document describes `TrainingSessionView` as its base properties only and `SetCommand` as `{}` —
 * Swashbuckle is not emitting the `[JsonPolymorphic]` derived types, so NSwag generates a class
 * whose `fromJS` silently drops `$type`, `sets`, `prescriptions` and every other subtype member.
 * That is the branch docs/api-and-composition.md §Open 3 warned about. Reads here therefore go
 * through `send`, which hands back the JSON as it arrived, and the discriminated unions the domain
 * actually has are declared below.
 */

import { send, type Result } from './result';

export type WeightUnit = 0 | 1;
export type DistanceUnit = 0 | 1 | 2 | 3;
export type Provenance = 0 | 1;
export type Valence = 0 | 1 | 2 | 3;
export type FacetAxis = 0 | 1 | 2 | 3 | 4 | 5;

export const WEIGHT_UNIT_LABELS: Readonly<Record<WeightUnit, string>> = { 0: 'kg', 1: 'lb' };
export const DISTANCE_UNIT_LABELS: Readonly<Record<DistanceUnit, string>> = {
	0: 'm',
	1: 'yd',
	2: 'mi',
	3: 'km'
};

/** `RpeJsonConverter` writes the fill-state as a number or null — never an object. */
export type Rpe = number | null;

export type Facet = { readonly axis: FacetAxis; readonly value: string };

/** How an entry was logged; `Texted` keeps the athlete's own words alongside any structure. */
export type Utterance =
	| { readonly $type: 'texted'; readonly rawText: string; readonly structureConfidence: number }
	| { readonly $type: 'formed' };

type SetEntryBase = { readonly source: Utterance; readonly facets: readonly Facet[] };

/** One performed set. A session holds a flat ordered list of these; there is no movement entity. */
export type SetEntry = SetEntryBase &
	(
		| {
				readonly $type: 'lift';
				readonly movementDataId: string;
				readonly reps: number;
				readonly weight: number;
				readonly unit: WeightUnit;
				readonly rpe: Rpe;
		  }
		| {
				readonly $type: 'distanceTime';
				readonly movementDataId: string;
				readonly distance: number;
				readonly unit: DistanceUnit;
				readonly duration: string;
				readonly rpe: Rpe;
		  }
		| {
				readonly $type: 'trailRun';
				readonly distance: number;
				readonly unit: DistanceUnit;
				readonly duration: string;
				readonly elevationGainMeters: number;
				readonly rpe: Rpe;
		  }
		| { readonly $type: 'freeform' }
	);

export type SetPrescription =
	| {
			readonly $type: 'lift';
			readonly movementDataId: string;
			readonly targetReps: number;
			readonly targetWeight: number;
			readonly unit: WeightUnit;
			readonly targetRpe: Rpe;
	  }
	| {
			readonly $type: 'distanceTime';
			readonly movementDataId: string;
			readonly targetDistance: number;
			readonly unit: DistanceUnit;
			readonly targetDuration: string;
	  }
	| { readonly $type: 'freeform'; readonly text: string };

export type Period =
	| { readonly $type: 'instant'; readonly at: string }
	| { readonly $type: 'interval'; readonly start: string; readonly end: string };

export type PerceivedEffortRatings = {
	readonly recordedAt?: string;
	readonly accumulatedFatigue?: number | null;
	readonly difficultyRating?: number | null;
	readonly engagementRating?: number | null;
	readonly externalVariablesRating?: number | null;
};

export type EffortState =
	| { readonly $type: 'none' }
	| { readonly $type: 'reported'; readonly ratings: PerceivedEffortRatings };

export type DecorationView =
	| { readonly $type: 'none' }
	| {
			readonly $type: 'decorated';
			readonly facets: readonly Facet[];
			readonly freeTags: readonly string[];
			readonly valence: Valence;
	  };

type SessionEnvelope = {
	readonly trainingSessionId: string;
	readonly notes: string;
	readonly provenance: Provenance;
	readonly trainingProgramId: string | null;
	readonly createdAt: string;
	/** The calendar day the session belongs to, already read at the offset it was logged in. */
	readonly date: string;
};

/** The session lifecycle, as the sealed hierarchy the API actually answers with. */
export type TrainingSessionView = SessionEnvelope &
	(
		| {
				readonly $type: 'planned';
				readonly plannedFor: string;
				readonly prescriptions: readonly SetPrescription[];
		  }
		| { readonly $type: 'active'; readonly startedAt: string; readonly sets: readonly SetEntry[] }
		| {
				readonly $type: 'completed';
				readonly sets: readonly SetEntry[];
				readonly period: Period;
				readonly effort: EffortState;
				readonly decoration: DecorationView;
		  }
		| { readonly $type: 'skipped'; readonly plannedFor: string }
	);

export type SessionLifecycle = TrainingSessionView['$type'];

export const LIFECYCLE_LABELS: Readonly<Record<SessionLifecycle, string>> = {
	planned: 'Planned',
	active: 'Active',
	completed: 'Completed',
	skipped: 'Skipped'
};

export const LIFECYCLE_BADGES: Readonly<Record<SessionLifecycle, string>> = {
	planned: 'badge-ghost',
	active: 'badge-warning',
	completed: 'badge-success',
	skipped: 'badge-error'
};

/** The sets a session has performed, which is none at all in the two states that have not. */
export function setsOf(session: TrainingSessionView): readonly SetEntry[] {
	return session.$type === 'active' || session.$type === 'completed' ? session.sets : [];
}

export function prescriptionsOf(session: TrainingSessionView): readonly SetPrescription[] {
	return session.$type === 'planned' ? session.prescriptions : [];
}

// Commands. Purpose-built per transition, carrying only what a client is authorized to state —
// never an id, a provenance or a decoration (docs/api-and-composition.md §1).

export type SetCommand =
	| {
			readonly $type: 'lift';
			readonly movementDataId: string;
			readonly reps: number;
			readonly weight: number;
			readonly unit: WeightUnit;
			readonly rpe: number | null;
	  }
	| {
			readonly $type: 'distanceTime';
			readonly movementDataId: string;
			readonly distance: number;
			readonly unit: DistanceUnit;
			readonly duration: string;
			readonly rpe: number | null;
	  }
	| {
			readonly $type: 'trailRun';
			readonly distance: number;
			readonly unit: DistanceUnit;
			readonly duration: string;
			readonly elevationGainMeters: number;
			readonly rpe: number | null;
	  }
	| { readonly $type: 'freeform'; readonly text: string };

export type SetPrescriptionCommand =
	| {
			readonly $type: 'lift';
			readonly movementDataId: string;
			readonly targetReps: number;
			readonly targetWeight: number;
			readonly unit: WeightUnit;
			readonly targetRpe: number | null;
	  }
	| {
			readonly $type: 'distanceTime';
			readonly movementDataId: string;
			readonly targetDistance: number;
			readonly unit: DistanceUnit;
			readonly targetDuration: string;
	  }
	| { readonly $type: 'freeform'; readonly text: string };

export type PeriodCommand = { readonly start: string; readonly end: string | null };

export type PerceivedEffortCommand = {
	readonly accumulatedFatigue: number | null;
	readonly difficultyRating: number | null;
	readonly engagementRating: number | null;
	readonly externalVariablesRating: number | null;
};

const SESSIONS = '/api/training/sessions';

export function planSession(request: {
	plannedFor: string;
	prescriptions: readonly SetPrescriptionCommand[];
	notes: string;
	trainingProgramId: string | null;
}): Promise<Result<TrainingSessionView>> {
	return send(`${SESSIONS}/planned`, { method: 'POST', body: request });
}

export function startUnplannedSession(request: {
	startedAt: string;
	notes: string;
	trainingProgramId: string | null;
}): Promise<Result<TrainingSessionView>> {
	return send(`${SESSIONS}/active`, { method: 'POST', body: request });
}

export function startSession(id: string, startedAt: string): Promise<Result<TrainingSessionView>> {
	return send(`${SESSIONS}/${id}/start`, { method: 'POST', body: { startedAt } });
}

export function completeSession(
	id: string,
	period: PeriodCommand,
	effort: PerceivedEffortCommand | null
): Promise<Result<TrainingSessionView>> {
	return send(`${SESSIONS}/${id}/complete`, { method: 'POST', body: { period, effort } });
}

export function skipSession(id: string): Promise<Result<TrainingSessionView>> {
	return send(`${SESSIONS}/${id}/skip`, { method: 'POST' });
}

export function getSession(id: string): Promise<Result<TrainingSessionView>> {
	return send(`${SESSIONS}/${id}`);
}

export function getSessions(from: Date, to: Date): Promise<Result<TrainingSessionView[]>> {
	const window = new URLSearchParams({ from: from.toISOString(), to: to.toISOString() });
	return send(`${SESSIONS}?${window}`);
}

export function updateSessionNotes(id: string, notes: string): Promise<Result<TrainingSessionView>> {
	return send(`${SESSIONS}/${id}/notes`, { method: 'PATCH', body: { notes } });
}

/** Sets are replaced whole — there is deliberately no per-set route (docs/api-surface.md). */
export function replaceSets(
	id: string,
	sets: readonly SetCommand[]
): Promise<Result<TrainingSessionView>> {
	return send(`${SESSIONS}/${id}/sets`, { method: 'PUT', body: { sets } });
}

export function replacePrescriptions(
	id: string,
	prescriptions: readonly SetPrescriptionCommand[]
): Promise<Result<TrainingSessionView>> {
	return send(`${SESSIONS}/${id}/prescriptions`, { method: 'PUT', body: { prescriptions } });
}

export function deleteSession(id: string): Promise<Result<void>> {
	return send(`${SESSIONS}/${id}`, { method: 'DELETE' });
}

export function deleteContext(id: string): Promise<Result<void>> {
	return send(`/api/contexts/${id}`, { method: 'DELETE' });
}

// Programs. Polymorphic for the same reason sessions are, and flattened by the generator the
// same way, so they are typed here too.

type ProgramEnvelope = {
	readonly trainingProgramId: string;
	readonly title: string;
	readonly startDate: string;
	readonly endDate: string;
	readonly tags: readonly string[];
};

export type TrainingProgramView = ProgramEnvelope &
	(
		| { readonly $type: 'planned' }
		| { readonly $type: 'active' }
		| { readonly $type: 'completed'; readonly summary: string }
	);

export type ProgramLifecycle = TrainingProgramView['$type'];

export const PROGRAM_LIFECYCLE_LABELS: Readonly<Record<ProgramLifecycle, string>> = {
	planned: 'Planned',
	active: 'Active',
	completed: 'Completed'
};

const PROGRAMS = '/api/training/programs';

export function getPrograms(): Promise<Result<TrainingProgramView[]>> {
	return send(PROGRAMS);
}

export function getProgram(id: string): Promise<Result<TrainingProgramView>> {
	return send(`${PROGRAMS}/${id}`);
}

export function planProgram(request: {
	title: string;
	startDate: string;
	endDate: string;
	tags: readonly string[];
}): Promise<Result<TrainingProgramView>> {
	return send(PROGRAMS, { method: 'POST', body: request });
}

export function activateProgram(id: string): Promise<Result<TrainingProgramView>> {
	return send(`${PROGRAMS}/${id}/activate`, { method: 'POST' });
}

export function completeProgram(id: string, summary: string): Promise<Result<TrainingProgramView>> {
	return send(`${PROGRAMS}/${id}/complete`, { method: 'POST', body: { summary } });
}

export function updateProgram(
	id: string,
	request: { title: string; startDate: string; endDate: string; tags: readonly string[] }
): Promise<Result<TrainingProgramView>> {
	return send(`${PROGRAMS}/${id}`, { method: 'PATCH', body: request });
}

export function deleteProgram(id: string): Promise<Result<void>> {
	return send(`${PROGRAMS}/${id}`, { method: 'DELETE' });
}
