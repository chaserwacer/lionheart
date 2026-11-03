/**
 * The client half of the server's `Result<T, Problem>`: a call either answered with a value or
 * refused with a named reason. Nothing here throws, so a caller cannot forget the failure path.
 *
 * This exists rather than reusing the generated client's `ApiException` because a thrown exception
 * carries its meaning in a status number that every call site then re-reads. `Problem` is the same
 * closed vocabulary the server maps from (docs/api-surface.md, "Problem → HTTP"), read back once.
 */

export type Problem =
	/** The request never reached the server — no connection, or it dropped mid-flight. */
	| { readonly kind: 'offline' }
	/** The caller stopped the request. The athlete tapping Stop, not something going wrong. */
	| { readonly kind: 'cancelled' }
	| { readonly kind: 'unauthorized' }
	| { readonly kind: 'notFound' }
	| { readonly kind: 'invalid'; readonly detail: string }
	| { readonly kind: 'conflict'; readonly detail: string }
	/** An external dependency answered unusably — for Quick Log, that is the local model. */
	| { readonly kind: 'upstream'; readonly detail: string }
	| { readonly kind: 'unexpected'; readonly detail: string };

export type Result<T> = { readonly ok: true; readonly value: T } | { readonly ok: false; readonly problem: Problem };

const succeeded = <T>(value: T): Result<T> => ({ ok: true, value });

const failed = <T>(problem: Problem): Result<T> => ({ ok: false, problem });

/** Whether retrying the identical request could plausibly succeed later. */
export function isRetryable(problem: Problem): boolean {
	return (
		problem.kind === 'offline' ||
		problem.kind === 'cancelled' ||
		problem.kind === 'upstream' ||
		problem.kind === 'unexpected'
	);
}

// The retryable cases say "saved" and stop there. Nothing auto-retries — the entry is kept and the
// athlete taps Retry — so promising that it will is a claim the app does not keep.
export function describe(problem: Problem): string {
	switch (problem.kind) {
		case 'offline':
			return 'No connection — saved. Tap retry.';
		case 'cancelled':
			return 'Stopped. Tap retry when you want it read.';
		case 'unauthorized':
			return 'Your session expired. Sign in again.';
		case 'notFound':
			return 'That is no longer there.';
		case 'invalid':
			return problem.detail;
		case 'conflict':
			return problem.detail;
		case 'upstream':
			return 'The interpreter is not answering — saved. Tap retry.';
		case 'unexpected':
			// A trace id is for the log, not for the athlete staring at a lost set.
			return 'The server could not finish that — saved. Tap retry.';
	}
}

type Json = Record<string, unknown>;

/** ProblemDetails is what the endpoints answer with; anything else is read as plain text. */
async function problemFrom(response: Response): Promise<Problem> {
	const body = await response.text();
	const detail = detailOf(body) || response.statusText || `HTTP ${response.status}`;

	switch (response.status) {
		case 400:
			return { kind: 'invalid', detail };
		case 401:
		case 403:
			return { kind: 'unauthorized' };
		case 404:
			return { kind: 'notFound' };
		case 409:
			return { kind: 'conflict', detail };
		case 502:
			return { kind: 'upstream', detail };
		default:
			return { kind: 'unexpected', detail };
	}
}

function detailOf(body: string): string {
	if (body.trim() === '') return '';
	try {
		const parsed: unknown = JSON.parse(body);
		if (typeof parsed !== 'object' || parsed === null) return body;
		const problem = parsed as Json;
		// ProblemDetails on the mapped refusals; `message` on the unhandled-exception handler.
		const detail = problem.detail ?? problem.title ?? problem.message;
		return typeof detail === 'string' ? detail : body;
	} catch {
		return body;
	}
}

export type RequestOptions = {
	readonly method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
	/** Serialized verbatim. Draft round-trips depend on key order surviving, so never rebuild it. */
	readonly body?: unknown;
	readonly signal?: AbortSignal;
};

/**
 * One request, one `Result`. Cookie auth is same-origin, so no header wiring is needed here.
 */
export async function send<T>(path: string, options: RequestOptions = {}): Promise<Result<T>> {
	const { method = 'GET', body, signal } = options;

	let response: Response;
	try {
		response = await fetch(path, {
			method,
			signal,
			headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
			body: body === undefined ? undefined : JSON.stringify(body)
		});
	} catch (cause) {
		// An aborted request is the caller's own doing, not a lost connection.
		if (cause instanceof DOMException && cause.name === 'AbortError') {
			return failed({ kind: 'cancelled' });
		}
		return failed({ kind: 'offline' });
	}

	if (!response.ok) return failed(await problemFrom(response));

	const text = await response.text();
	if (text.trim() === '') return succeeded(undefined as T);

	try {
		return succeeded(JSON.parse(text) as T);
	} catch {
		return failed({ kind: 'unexpected', detail: 'The server sent a body that is not JSON.' });
	}
}
