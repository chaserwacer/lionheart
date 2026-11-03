/**
 * The training reading surface: programs, the session window, and the movement library.
 *
 * The remodel moved sessions from a status column to a sealed lifecycle, so nothing here asks
 * "what status is it" — the cases are matched on `$type`, and a session that has not happened
 * simply has no sets to ask about.
 */

import {
	CreateEquipmentEndpointClient,
	CreateEquipmentRequest,
	CreateMovementBaseEndpointClient,
	CreateMovementBaseRequest,
	DeleteEquipmentEndpointClient,
	DeleteMovementBaseEndpointClient,
	GetAllMuscleGroupsAsyncClient,
	GetEquipmentsEndpointClient,
	GetMovementBasesEndpointClient,
	GetMovementDatasEndpointClient,
	GetMovementModifiersEndpointClient,
	UpdateEquipmentEndpointClient,
	UpdateEquipmentRequest,
	UpdateMovementBaseEndpointClient,
	UpdateMovementBaseRequest,
	type EquipmentDTO,
	type MovementBaseDTO,
	type MovementDataDTO,
	type MovementModifierDTO,
	type MuscleGroup
} from '$lib/api/ApiClient';
import { describe } from '$lib/api/result';
import {
	getPrograms,
	getSessions,
	type TrainingProgramView,
	type TrainingSessionView
} from '$lib/api/training';

/** The window the app keeps loaded: recent history plus the planned week ahead. */
const DAYS_BEHIND = 28;
const DAYS_AHEAD = 14;

function daysFromNow(days: number): Date {
	const moment = new Date();
	moment.setDate(moment.getDate() + days);
	return moment;
}

/** Sessions order by the day they belong to; the server has already read it at the right offset. */
function byDate(session: TrainingSessionView): number {
	return new Date(`${session.date}T00:00:00`).getTime();
}

function movementLabel(movement: MovementDataDTO): string {
	const parts = [
		movement.movementModifier?.name,
		movement.equipment?.name,
		movement.movementBase?.name
	].filter((part): part is string => Boolean(part));
	return parts.join(' ') || 'Movement';
}

class TrainingData {
	programs = $state<TrainingProgramView[]>([]);
	sessions = $state<TrainingSessionView[]>([]);
	equipment = $state<EquipmentDTO[]>([]);
	movementBases = $state<MovementBaseDTO[]>([]);
	movementModifiers = $state<MovementModifierDTO[]>([]);
	movementDatas = $state<MovementDataDTO[]>([]);
	muscleGroups = $state<MuscleGroup[]>([]);

	error = $state('');

	readonly completedSessions = $derived(
		this.sessions.filter((session) => session.$type === 'completed').sort((a, b) => byDate(a) - byDate(b))
	);

	readonly lastCompletedSession = $derived(this.completedSessions.at(-1) ?? null);

	/** What is still to come: planned, and not yet in the past. */
	readonly upcomingSessions = $derived.by(() => {
		const today = new Date(new Date().toDateString()).getTime();
		return this.sessions
			.filter((session) => session.$type === 'planned' && byDate(session) >= today)
			.sort((a, b) => byDate(a) - byDate(b));
	});

	readonly nextSession = $derived(this.upcomingSessions.at(0) ?? null);

	readonly activeSession = $derived(this.sessions.find((session) => session.$type === 'active') ?? null);

	readonly recentSessions = $derived(
		[...this.sessions].sort((a, b) => byDate(b) - byDate(a)).slice(0, 10)
	);

	/** Reads a movement id back as the athlete's own name for it. */
	movementName(movementDataId: string): string {
		const movement = this.movementDatas.find((candidate) => candidate.movementDataID === movementDataId);
		return movement === undefined ? 'Movement' : movementLabel(movement);
	}

	async loadAll(): Promise<void> {
		await Promise.all([
			this.loadPrograms(),
			this.loadSessions(),
			this.loadLibrary()
		]);
	}

	async loadPrograms(): Promise<void> {
		const result = await getPrograms();
		if (result.ok) this.programs = result.value;
		else this.error = describe(result.problem);
	}

	async loadSessions(): Promise<void> {
		const result = await getSessions(daysFromNow(-DAYS_BEHIND), daysFromNow(DAYS_AHEAD));
		if (result.ok) this.sessions = result.value;
		else this.error = describe(result.problem);
	}

	/** Replaces one session in the window after a transition, so callers need not refetch it all. */
	absorb(session: TrainingSessionView): void {
		const known = this.sessions.some(
			(candidate) => candidate.trainingSessionId === session.trainingSessionId
		);
		this.sessions = known
			? this.sessions.map((candidate) =>
					candidate.trainingSessionId === session.trainingSessionId ? session : candidate
				)
			: [...this.sessions, session];
	}

	forget(sessionId: string): void {
		this.sessions = this.sessions.filter((session) => session.trainingSessionId !== sessionId);
	}

	/**
	 * The library loads as five independent reads: one endpoint being down should cost its own list,
	 * not the other four. A list that failed stays empty and says so, rather than looking merely
	 * unpopulated — an empty movement picker and a broken one look identical otherwise.
	 */
	async loadLibrary(): Promise<void> {
		const [equipment, bases, modifiers, movements, groups] = await Promise.all([
			this.#attempt('equipment', () => new GetEquipmentsEndpointClient('').get()),
			this.#attempt('the movement list', () => new GetMovementBasesEndpointClient('').get()),
			this.#attempt('modifiers', () => new GetMovementModifiersEndpointClient('').get()),
			this.#attempt('your movements', () => new GetMovementDatasEndpointClient('').get()),
			this.#attempt('muscle groups', () => new GetAllMuscleGroupsAsyncClient('').get())
		]);

		this.equipment = equipment ?? [];
		this.movementBases = bases ?? [];
		this.movementModifiers = modifiers ?? [];
		this.movementDatas = movements ?? [];
		this.muscleGroups = groups ?? [];
	}

	// The movement library kept its own endpoints through the remodel, and they are the generated
	// client's faithful half — flat requests, flat responses — so they are called through it.

	async createEquipment(name: string): Promise<void> {
		const created = await this.#attempt('equipment', () =>
			new CreateEquipmentEndpointClient('').post(new CreateEquipmentRequest({ name }))
		);
		if (created !== null) this.equipment = [...this.equipment, created];
	}

	async updateEquipment(equipmentID: string, name: string, enabled: boolean): Promise<void> {
		const updated = await this.#attempt('equipment', () =>
			new UpdateEquipmentEndpointClient('').post(
				new UpdateEquipmentRequest({ equipmentID, name, enabled })
			)
		);
		if (updated !== null) {
			this.equipment = this.equipment.map((item) =>
				item.equipmentID === equipmentID ? updated : item
			);
		}
	}

	async deleteEquipment(equipmentID: string): Promise<void> {
		const done = await this.#attempt('equipment', () =>
			new DeleteEquipmentEndpointClient('').delete(equipmentID)
		);
		if (done !== null) {
			this.equipment = this.equipment.filter((item) => item.equipmentID !== equipmentID);
		}
	}

	async createMovementBase(
		name: string,
		description?: string,
		muscleGroups?: MuscleGroup[]
	): Promise<void> {
		const created = await this.#attempt('the movement list', () =>
			new CreateMovementBaseEndpointClient('').post(
				CreateMovementBaseRequest.fromJS({ name, description, muscleGroups })
			)
		);
		if (created !== null) this.movementBases = [...this.movementBases, created];
	}

	async updateMovementBase(
		movementBaseID: string,
		name: string,
		description?: string,
		muscleGroups?: MuscleGroup[]
	): Promise<void> {
		const updated = await this.#attempt('the movement list', () =>
			new UpdateMovementBaseEndpointClient('').post(
				UpdateMovementBaseRequest.fromJS({ movementBaseID, name, description, muscleGroups })
			)
		);
		if (updated !== null) {
			this.movementBases = this.movementBases.map((item) =>
				item.movementBaseID === movementBaseID ? updated : item
			);
		}
	}

	async deleteMovementBase(movementBaseID: string): Promise<void> {
		const done = await this.#attempt('the movement list', () =>
			new DeleteMovementBaseEndpointClient('').delete(movementBaseID)
		);
		if (done !== null) {
			this.movementBases = this.movementBases.filter(
				(item) => item.movementBaseID !== movementBaseID
			);
		}
	}

	/**
	 * The generated client throws rather than answering a `Result`, so its failures are turned back
	 * into one here — null is "it did not happen, and `error` says so".
	 */
	async #attempt<T>(subject: string, call: () => Promise<T>): Promise<T | null> {
		try {
			return await call();
		} catch {
			this.error = `Could not reach ${subject}.`;
			return null;
		}
	}
}

export const training = new TrainingData();

/** A calendar day as the API states it, and as `<input type="date">` wants it. */
export function toDateOnly(value: Date): string {
	const year = value.getFullYear();
	const month = String(value.getMonth() + 1).padStart(2, '0');
	const day = String(value.getDate()).padStart(2, '0');
	return `${year}-${month}-${day}`;
}

/** Reads a `DateOnly` back as the athlete would say it — "Today", "Tomorrow", "3 days ago". */
export function relativeDay(dateOnly: string): string {
	if (!dateOnly) return 'No date';

	const day = new Date(`${dateOnly}T00:00:00`);
	if (Number.isNaN(day.getTime())) return dateOnly;

	const today = new Date(new Date().toDateString());
	const days = Math.round((day.getTime() - today.getTime()) / 86_400_000);

	if (days === 0) return 'Today';
	if (days === 1) return 'Tomorrow';
	if (days === -1) return 'Yesterday';
	if (days < 0 && days > -7) return `${Math.abs(days)} days ago`;
	if (days > 0 && days < 7) return `in ${days} days`;

	return day.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
}
