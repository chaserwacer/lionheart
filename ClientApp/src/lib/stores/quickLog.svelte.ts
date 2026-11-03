/**
 * Quick Log's state (docs/quick-log.md).
 *
 * The one guarantee this file exists to keep: an utterance the athlete submitted is written to
 * local storage before the request leaves, and erased only when the server says it was committed.
 * Every other state — interpreting, awaiting names, failed — survives a reload, a dropped
 * connection and a closed tab, because losing a logged set is the failure this feature cannot have.
 */

import {
	confirm,
	interpret,
	type InterpretationDraft,
	type InterpretationOutcome,
	type ReferenceChoice,
	type UnresolvedReference
} from '$lib/api/interpretation';
import { describe, isRetryable, type Problem, type Result } from '$lib/api/result';
import { deleteContext, deleteSession, getSession, type TrainingSessionView } from '$lib/api/training';

const STORAGE_KEY = 'lionheart.quicklog.entries';

/** Where one submitted utterance has got to. Only `committed` is absent — that entry is gone. */
export type EntryState =
	| { readonly kind: 'interpreting'; readonly since: number }
	| {
			readonly kind: 'awaiting';
			readonly draft: InterpretationDraft;
			readonly references: readonly UnresolvedReference[];
			readonly choices: readonly ReferenceChoice[];
	  }
	| { readonly kind: 'failed'; readonly reason: string; readonly retryable: boolean };

export type LogEntry = {
	readonly id: string;
	readonly rawText: string;
	/** ISO. The moment the athlete submitted, not the moment the server got round to it. */
	readonly loggedAt: string;
	readonly state: EntryState;
};

/** What was written, once a run committed. Shown as a receipt and undone as a whole. */
export type Receipt = {
	readonly id: string;
	readonly rawText: string;
	readonly sessionIds: readonly string[];
	readonly contextIds: readonly string[];
	readonly warnings: readonly string[];
	readonly sessions: readonly TrainingSessionView[];
	readonly undone: boolean;
	/** Why the last undo did not land, if it did not. The receipt stays undoable while this is set. */
	readonly undoProblem: string | null;
};

function newId(): string {
	return crypto.randomUUID();
}

/**
 * An `interpreting` entry restored from storage is one whose tab died mid-request. Its fate is
 * genuinely unknown, so it comes back as a failure the athlete can retry rather than as a spinner
 * with nothing behind it.
 */
function rehydrate(entry: LogEntry): LogEntry {
	return entry.state.kind === 'interpreting'
		? {
				...entry,
				state: { kind: 'failed', reason: 'Interrupted before it finished.', retryable: true }
			}
		: entry;
}

function read(): LogEntry[] {
	if (typeof localStorage === 'undefined') return [];
	try {
		const stored: unknown = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '[]');
		return Array.isArray(stored) ? (stored as LogEntry[]).map(rehydrate) : [];
	} catch {
		return [];
	}
}

class QuickLog {
	/** The text box. Cleared on submit; durability lives in the persisted entry, not here. */
	utterance = $state('');

	entries = $state<LogEntry[]>([]);

	receipts = $state<Receipt[]>([]);

	/** Names for the movement ids a receipt's sets carry, so the card can read like the athlete's. */
	movementNames = $state<Record<string, string>>({});

	/** Live requests, keyed by entry, so an athlete can interrupt one without losing the utterance. */
	#inflight = new Map<string, AbortController>();

	/**
	 * Reads storage back, keeping any entry whose request is still running in this tab. `load` runs
	 * on every mount of the log screen, and navigating away mid-parse is normal on a local model —
	 * without
	 * this an entry that is still in flight would come back rehydrated as "interrupted", offering a
	 * Retry that starts a second run of a request already on its way to committing.
	 */
	load(): void {
		const live = new Map(
			this.entries.filter((entry) => this.#inflight.has(entry.id)).map((entry) => [entry.id, entry])
		);

		this.entries = read().map((entry) => live.get(entry.id) ?? entry);
		this.#persist();
	}

	#persist(): void {
		if (typeof localStorage === 'undefined') return;
		try {
			localStorage.setItem(STORAGE_KEY, JSON.stringify(this.entries));
		} catch {
			// A full or blocked store must not take the submit down with it; the request still goes.
		}
	}

	#put(id: string, state: EntryState): void {
		this.entries = this.entries.map((entry) => (entry.id === id ? { ...entry, state } : entry));
		this.#persist();
	}

	#drop(id: string): void {
		this.entries = this.entries.filter((entry) => entry.id !== id);
		this.#persist();
	}

	find(id: string): LogEntry | undefined {
		return this.entries.find((entry) => entry.id === id);
	}

	/** Writes the utterance down, then interprets it. The order is the whole point. */
	async submit(): Promise<void> {
		const rawText = this.utterance.trim();
		if (rawText === '') return;

		const entry: LogEntry = {
			id: newId(),
			rawText,
			loggedAt: new Date().toISOString(),
			state: { kind: 'interpreting', since: Date.now() }
		};

		this.entries = [entry, ...this.entries];
		this.#persist();
		this.utterance = '';

		await this.#run(entry.id, (signal) => interpret(rawText, new Date(entry.loggedAt), signal));
	}

	/** Re-runs a failed entry against the same text and its original timestamp. */
	async retry(id: string): Promise<void> {
		const entry = this.find(id);
		if (!entry || entry.state.kind === 'interpreting') return;

		await this.#run(id, (signal) => interpret(entry.rawText, new Date(entry.loggedAt), signal));
	}

	/**
	 * Answers one name. The draft goes back untouched with every choice made so far, so the server
	 * re-resolves without re-parsing — and answering only some of the names simply comes back
	 * awaiting the rest.
	 */
	async choose(id: string, reference: UnresolvedReference, chosenEntityId: string | null): Promise<void> {
		const entry = this.find(id);
		if (!entry || entry.state.kind !== 'awaiting') return;

		const { draft, choices } = entry.state;
		const answered: ReferenceChoice[] = [
			...choices.filter((choice) => choice.rawName !== reference.rawName || choice.kind !== reference.kind),
			{ rawName: reference.rawName, kind: reference.kind, chosenEntityId }
		];

		await this.#run(id, (signal) => confirm(draft, answered, signal), answered);
	}

	/** Abandons an entry the athlete no longer wants logged. */
	discard(id: string): void {
		this.#inflight.get(id)?.abort();
		this.#inflight.delete(id);
		this.#drop(id);
	}

	/** Stops waiting on the model without losing the utterance — it stays retryable. */
	interrupt(id: string): void {
		this.#inflight.get(id)?.abort();
	}

	/**
	 * Deletes everything one run wrote. A delete that did not land leaves the receipt undoable and
	 * says so: reading "Undone" off a request that failed would tell the athlete a session is gone
	 * when it is still on the server.
	 */
	async undo(receiptId: string): Promise<void> {
		const receipt = this.receipts.find((candidate) => candidate.id === receiptId);
		if (!receipt || receipt.undone) return;

		const deletions = await Promise.all([
			...receipt.sessionIds.map((id) => deleteSession(id)),
			...receipt.contextIds.map((id) => deleteContext(id))
		]);

		const refusal = deletions.flatMap((deletion) => (deletion.ok ? [] : [deletion.problem]))[0];

		this.receipts = this.receipts.map((candidate) =>
			candidate.id === receiptId
				? refusal === undefined
					? { ...candidate, undone: true, undoProblem: null }
					: { ...candidate, undoProblem: describe(refusal) }
				: candidate
		);
	}

	dismissReceipt(receiptId: string): void {
		this.receipts = this.receipts.filter((receipt) => receipt.id !== receiptId);
	}

	async #run(
		id: string,
		call: (signal: AbortSignal) => Promise<Result<InterpretationOutcome>>,
		choices: readonly ReferenceChoice[] = []
	): Promise<void> {
		// One request per entry. A second run alongside the first would commit the same utterance
		// twice, which is the one failure this store exists to prevent.
		if (this.#inflight.has(id)) return;

		this.#put(id, { kind: 'interpreting', since: Date.now() });

		const controller = new AbortController();
		this.#inflight.set(id, controller);

		const result = await call(controller.signal);
		this.#inflight.delete(id);

		// The entry may have been discarded while the model was thinking.
		const entry = this.find(id);
		if (entry === undefined) return;

		if (result.ok) await this.#settle(entry, result.value, choices);
		else this.#fail(id, result.problem);
	}

	#fail(id: string, problem: Problem): void {
		this.#put(id, {
			kind: 'failed',
			reason: describe(problem),
			retryable: isRetryable(problem)
		});
	}

	async #settle(
		entry: LogEntry,
		outcome: InterpretationOutcome,
		choices: readonly ReferenceChoice[]
	): Promise<void> {
		if (outcome.$type === 'awaitingConfirmation') {
			this.#put(entry.id, {
				kind: 'awaiting',
				draft: outcome.draft,
				references: outcome.references,
				choices
			});
			return;
		}

		// Committed, and only now: the utterance has landed somewhere durable.
		this.#drop(entry.id);

		const sessions = await this.#loadSessions(outcome.sessionIds);
		this.receipts = [
			{
				id: entry.id,
				rawText: entry.rawText,
				sessionIds: outcome.sessionIds,
				contextIds: outcome.contextIds,
				warnings: outcome.warnings,
				sessions,
				undone: false,
				undoProblem: null
			},
			...this.receipts
		];
	}

	async #loadSessions(ids: readonly string[]): Promise<TrainingSessionView[]> {
		const loaded = await Promise.all(ids.map((id) => getSession(id)));
		return loaded.flatMap((result) => (result.ok ? [result.value] : []));
	}
}

export const quickLog = new QuickLog();
