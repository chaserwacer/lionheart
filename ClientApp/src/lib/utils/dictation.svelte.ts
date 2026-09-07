/**
 * Speech-to-text as an enhancement over the text box, never a replacement for it
 * (docs/quick-log.md): the browsers that have it get a microphone, the rest lose nothing.
 *
 * The Web Speech API is still vendor-prefixed on the browser this app exists to run on, and the
 * DOM lib does not describe it, so the surface actually used is declared here.
 */

type SpeechAlternative = { readonly transcript: string };
type SpeechResult = { readonly isFinal: boolean; readonly length: number; readonly [index: number]: SpeechAlternative };
type SpeechResultList = { readonly length: number; readonly [index: number]: SpeechResult };

type SpeechRecognitionEvent = { readonly resultIndex: number; readonly results: SpeechResultList };
type SpeechRecognitionErrorEvent = { readonly error: string };

type SpeechRecognition = {
	continuous: boolean;
	interimResults: boolean;
	lang: string;
	onresult: ((event: SpeechRecognitionEvent) => void) | null;
	onerror: ((event: SpeechRecognitionErrorEvent) => void) | null;
	onend: (() => void) | null;
	start(): void;
	stop(): void;
};

type SpeechRecognitionConstructor = new () => SpeechRecognition;

function constructorFor(): SpeechRecognitionConstructor | null {
	if (typeof window === 'undefined') return null;
	const candidate = window as unknown as {
		SpeechRecognition?: SpeechRecognitionConstructor;
		webkitSpeechRecognition?: SpeechRecognitionConstructor;
	};
	return candidate.SpeechRecognition ?? candidate.webkitSpeechRecognition ?? null;
}

/**
 * A dictation session. `transcript` is what has been heard so far — the caller decides what to do
 * with it, so the text box stays the single source of truth for what will be submitted.
 */
export class Dictation {
	listening = $state(false);

	/** Set when the browser refuses (no microphone permission, no network for the recognizer). */
	problem = $state('');

	#recognition: SpeechRecognition | null = null;

	readonly #Recognition = constructorFor();

	readonly available = this.#Recognition !== null;

	/** @param onTranscript called with the accumulated transcript as it firms up. */
	constructor(private readonly onTranscript: (transcript: string) => void) {}

	toggle(): void {
		if (this.listening) this.stop();
		else this.start();
	}

	start(): void {
		const Recognition = this.#Recognition;
		if (Recognition === null) return;

		this.problem = '';
		const recognition = new Recognition();
		recognition.continuous = true;
		recognition.interimResults = true;
		recognition.lang = navigator.language || 'en-US';

		// Every result, not just the ones from `resultIndex`. A continuous session finalises each
		// clause as its own result, and later events point past the finalised ones — reading from
		// `resultIndex` would hand back only what was said since the last pause.
		recognition.onresult = (event) => {
			let heard = '';
			for (let index = 0; index < event.results.length; index += 1) {
				heard += event.results[index][0].transcript;
			}
			this.onTranscript(heard);
		};

		recognition.onerror = (event) => {
			this.problem =
				event.error === 'not-allowed'
					? 'Microphone access is off — type it instead.'
					: 'Dictation stopped. Type it instead.';
			this.listening = false;
		};

		recognition.onend = () => {
			this.listening = false;
		};

		this.#recognition = recognition;
		recognition.start();
		this.listening = true;
	}

	stop(): void {
		this.#recognition?.stop();
		this.listening = false;
	}
}
