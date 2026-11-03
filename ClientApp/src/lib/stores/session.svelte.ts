/**
 * One session's detail screen.
 *
 * The remodel replaced the `Movement` wrapper entity and its per-set rows with a flat ordered list
 * replaced whole through `PUT /sets`, so this holds a draft list and writes it in one call. There
 * are no set ids to track and no index-drift hazard, because no index is ever addressed remotely.
 * Lifecycle changes are transitions, and a transition from the wrong state is a refusal the screen
 * shows rather than a button it silently ignores.
 */

import { describe, type Problem, type Result } from '$lib/api/result';
import {
	completeSession as completeSessionCall,
	deleteSession as deleteSessionCall,
	getSession,
	replacePrescriptions,
	replaceSets,
	skipSession as skipSessionCall,
	startSession as startSessionCall,
	updateSessionNotes,
	type SetCommand,
	type SetEntry,
	type SetPrescription,
	type SetPrescriptionCommand,
	type TrainingSessionView,
	type WeightUnit
} from '$lib/api/training';
import { training } from './training.svelte';

/** What a session's list offers for editing: its targets, its performed sets, or nothing. */
type EditableList = 'prescriptions' | 'sets' | 'none';

/**
 * A performed set read back as the command that would restate it. `Source` and `Facets` are the
 * server's to assign, so they are dropped rather than echoed — a client may not state either.
 */
function toCommand(set: SetEntry): SetCommand {
	switch (set.$type) {
		case 'lift':
			return {
				$type: 'lift',
				movementDataId: set.movementDataId,
				reps: set.reps,
				weight: set.weight,
				unit: set.unit,
				rpe: set.rpe
			};
		case 'distanceTime':
			return {
				$type: 'distanceTime',
				movementDataId: set.movementDataId,
				distance: set.distance,
				unit: set.unit,
				duration: set.duration,
				rpe: set.rpe
			};
		case 'trailRun':
			return {
				$type: 'trailRun',
				distance: set.distance,
				unit: set.unit,
				duration: set.duration,
				elevationGainMeters: set.elevationGainMeters,
				rpe: set.rpe
			};
		case 'freeform':
			return {
				$type: 'freeform',
				text: set.source.$type === 'texted' ? set.source.rawText : ''
			};
	}
}

function toPrescriptionCommand(prescription: SetPrescription): SetPrescriptionCommand {
	switch (prescription.$type) {
		case 'lift':
			return {
				$type: 'lift',
				movementDataId: prescription.movementDataId,
				targetReps: prescription.targetReps,
				targetWeight: prescription.targetWeight,
				unit: prescription.unit,
				targetRpe: prescription.targetRpe
			};
		case 'distanceTime':
			return {
				$type: 'distanceTime',
				movementDataId: prescription.movementDataId,
				targetDistance: prescription.targetDistance,
				unit: prescription.unit,
				targetDuration: prescription.targetDuration
			};
		case 'freeform':
			return { $type: 'freeform', text: prescription.text };
	}
}

class SessionDetail {
	session = $state<TrainingSessionView | null>(null);
	loading = $state(false);
	error = $state('');

	/** The list being edited. Empty until edit mode starts, so a stale draft cannot be saved. */
	draftSets = $state<SetCommand[]>([]);
	draftPrescriptions = $state<SetPrescriptionCommand[]>([]);
	draftNotes = $state('');
	editing = $state(false);
	saving = $state(false);

	/** Which list this session edits at all: performed sets, targets, or nothing (terminal). */
	readonly editable = $derived.by((): EditableList => {
		switch (this.session?.$type) {
			case 'planned':
				return 'prescriptions';
			case 'active':
				return 'sets';
			// A completed session's sets stay correctable — a parse is a first draft, not a verdict.
			case 'completed':
				return 'sets';
			default:
				return 'none';
		}
	});

	async load(sessionId: string): Promise<void> {
		this.loading = true;
		this.error = '';

		const result = await getSession(sessionId);
		this.loading = false;

		if (result.ok) this.#adopt(result.value);
		else this.#refuse(result.problem);
	}

	reset(): void {
		this.session = null;
		this.draftSets = [];
		this.draftPrescriptions = [];
		this.draftNotes = '';
		this.editing = false;
		this.error = '';
	}

	startEditing(): void {
		if (this.session === null) return;
		this.draftNotes = this.session.notes;
		this.draftSets =
			this.session.$type === 'active' || this.session.$type === 'completed'
				? this.session.sets.map(toCommand)
				: [];
		this.draftPrescriptions =
			this.session.$type === 'planned' ? this.session.prescriptions.map(toPrescriptionCommand) : [];
		this.editing = true;
	}

	cancelEditing(): void {
		this.editing = false;
		this.draftSets = [];
		this.draftPrescriptions = [];
	}

	addLiftSet(movementDataId: string, reps: number, weight: number, unit: WeightUnit): void {
		this.draftSets = [
			...this.draftSets,
			{ $type: 'lift', movementDataId, reps, weight, unit, rpe: null }
		];
	}

	addFreeformSet(text: string): void {
		this.draftSets = [...this.draftSets, { $type: 'freeform', text }];
	}

	addLiftPrescription(
		movementDataId: string,
		targetReps: number,
		targetWeight: number,
		unit: WeightUnit
	): void {
		this.draftPrescriptions = [
			...this.draftPrescriptions,
			{ $type: 'lift', movementDataId, targetReps, targetWeight, unit, targetRpe: null }
		];
	}

	addFreeformPrescription(text: string): void {
		this.draftPrescriptions = [...this.draftPrescriptions, { $type: 'freeform', text }];
	}

	removeSet(index: number): void {
		this.draftSets = this.draftSets.filter((_, position) => position !== index);
	}

	removePrescription(index: number): void {
		this.draftPrescriptions = this.draftPrescriptions.filter((_, position) => position !== index);
	}

	/** Reordering is a local rearrangement; the whole list is written either way. */
	moveSet(index: number, offset: number): void {
		this.draftSets = reorder(this.draftSets, index, index + offset);
	}

	movePrescription(index: number, offset: number): void {
		this.draftPrescriptions = reorder(this.draftPrescriptions, index, index + offset);
	}

	async save(): Promise<void> {
		if (this.session === null) return;

		this.saving = true;
		const sessionId = this.session.trainingSessionId;
		const notesChanged = this.draftNotes !== this.session.notes;

		if (notesChanged) {
			const noted = await updateSessionNotes(sessionId, this.draftNotes);
			if (!noted.ok) {
				this.saving = false;
				this.#refuse(noted.problem);
				return;
			}
			this.#adopt(noted.value);
		}

		const written =
			this.editable === 'prescriptions'
				? await replacePrescriptions(sessionId, this.draftPrescriptions)
				: this.editable === 'sets'
					? await replaceSets(sessionId, this.draftSets)
					: null;

		this.saving = false;

		if (written === null) {
			this.editing = false;
			return;
		}

		if (written.ok) {
			this.#adopt(written.value);
			this.editing = false;
		} else {
			this.#refuse(written.problem);
		}
	}

	start(): Promise<void> {
		return this.#transition((id) => startSessionCall(id, new Date().toISOString()));
	}

	complete(): Promise<void> {
		const startedAt =
			this.session?.$type === 'active' ? this.session.startedAt : new Date().toISOString();
		return this.#transition((id) =>
			completeSessionCall(id, { start: startedAt, end: new Date().toISOString() }, null)
		);
	}

	skip(): Promise<void> {
		return this.#transition((id) => skipSessionCall(id));
	}

	async remove(): Promise<boolean> {
		if (this.session === null) return false;

		const sessionId = this.session.trainingSessionId;
		const result = await deleteSessionCall(sessionId);
		if (!result.ok) {
			this.#refuse(result.problem);
			return false;
		}

		training.forget(sessionId);
		this.reset();
		return true;
	}

	async #transition(
		call: (sessionId: string) => Promise<Result<TrainingSessionView>>
	): Promise<void> {
		if (this.session === null) return;

		this.saving = true;
		const result = await call(this.session.trainingSessionId);
		this.saving = false;

		if (result.ok) this.#adopt(result.value);
		else this.#refuse(result.problem);
	}

	#adopt(session: TrainingSessionView): void {
		this.session = session;
		this.error = '';
		training.absorb(session);
	}

	/** A refused transition is information, not an exception — it names the state it was in. */
	#refuse(problem: Problem): void {
		this.error = problem.kind === 'conflict' ? problem.detail : describe(problem);
	}
}

function reorder<T>(items: readonly T[], from: number, to: number): T[] {
	if (to < 0 || to >= items.length || from === to) return [...items];

	const next = [...items];
	const [moved] = next.splice(from, 1);
	next.splice(to, 0, moved);
	return next;
}

export const sessionDetail = new SessionDetail();
