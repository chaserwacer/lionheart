/**
 * Reading a `SetEntry` back as a line of text. One function per shape rather than a bag of
 * optional fields, because the domain's set list is a union and each case says a different thing.
 */

import {
	DISTANCE_UNIT_LABELS,
	WEIGHT_UNIT_LABELS,
	type Facet,
	type Rpe,
	type SetCommand,
	type SetEntry,
	type SetPrescription,
	type SetPrescriptionCommand
} from '$lib/api/training';

const FACET_AXIS_LABELS = [
	'modality',
	'pattern',
	'region',
	'intent',
	'life',
	'body'
] as const;

export function facetLabel(facet: Facet): string {
	return `${FACET_AXIS_LABELS[facet.axis] ?? 'facet'}: ${facet.value}`;
}

function rpeSuffix(rpe: Rpe): string {
	return rpe === null ? '' : ` @ RPE ${rpe}`;
}

/** `TimeSpan` arrives as `hh:mm:ss(.fffffff)`; hours are dropped when there are none. */
function duration(value: string): string {
	const [hours, minutes, seconds] = value.split(':');
	if (seconds === undefined) return value;
	const whole = seconds.split('.')[0];
	return Number(hours) > 0 ? `${Number(hours)}:${minutes}:${whole}` : `${Number(minutes)}:${whole}`;
}

/**
 * A written set and a drafted one differ only in where their loose case keeps its words: a
 * `SetEntry` carries the athlete's `Utterance`, a `SetCommand` carries plain text. Every measured
 * case is field-for-field identical, so one reading serves both.
 */
export type AnySet = SetEntry | SetCommand;
export type AnyPrescription = SetPrescription | SetPrescriptionCommand;

export type MovementName = (id: string) => string;

function looseText(set: Extract<AnySet, { $type: 'freeform' }>): string {
	if ('text' in set) return set.text;
	return set.source.$type === 'texted' ? set.source.rawText : '';
}

export function describeSet(set: AnySet, movementName: MovementName): string {
	switch (set.$type) {
		case 'lift':
			return `${movementName(set.movementDataId)} — ${set.reps} × ${set.weight} ${WEIGHT_UNIT_LABELS[set.unit]}${rpeSuffix(set.rpe)}`;
		case 'distanceTime':
			return `${movementName(set.movementDataId)} — ${set.distance} ${DISTANCE_UNIT_LABELS[set.unit]} in ${duration(set.duration)}${rpeSuffix(set.rpe)}`;
		case 'trailRun':
			return `Trail run — ${set.distance} ${DISTANCE_UNIT_LABELS[set.unit]} in ${duration(set.duration)}, ${Math.round(set.elevationGainMeters)} m gain${rpeSuffix(set.rpe)}`;
		case 'freeform':
			return looseText(set) || 'Logged as written';
	}
}

export function describePrescription(
	prescription: AnyPrescription,
	movementName: MovementName
): string {
	switch (prescription.$type) {
		case 'lift':
			return `${movementName(prescription.movementDataId)} — ${prescription.targetReps} × ${prescription.targetWeight} ${WEIGHT_UNIT_LABELS[prescription.unit]}${rpeSuffix(prescription.targetRpe)}`;
		case 'distanceTime':
			return `${movementName(prescription.movementDataId)} — ${prescription.targetDistance} ${DISTANCE_UNIT_LABELS[prescription.unit]} in ${duration(prescription.targetDuration)}`;
		case 'freeform':
			return prescription.text;
	}
}

/**
 * Sets are stored flat; movement grouping is derivable from consecutive movement ids, which is what
 * `SetEntry` says it is for. Grouping is a reading of the list, never a shape the client sends back.
 */
export function groupByMovement(
	sets: readonly SetEntry[]
): { readonly movementDataId: string | null; readonly sets: readonly SetEntry[] }[] {
	const groups: { movementDataId: string | null; sets: SetEntry[] }[] = [];

	for (const set of sets) {
		const movementDataId =
			set.$type === 'lift' || set.$type === 'distanceTime' ? set.movementDataId : null;
		const open = groups.at(-1);

		if (open !== undefined && open.movementDataId === movementDataId && movementDataId !== null) {
			open.sets.push(set);
		} else {
			groups.push({ movementDataId, sets: [set] });
		}
	}

	return groups;
}
