<script lang="ts">
  import { Dictation } from "$lib/utils/dictation.svelte";

  let {
    value = $bindable(""),
    disabled = false,
    onsubmit,
  }: {
    value: string;
    disabled?: boolean;
    onsubmit: () => void;
  } = $props();

  // What the text box held when dictation started, so speech appends instead of overwriting.
  let dictationBase = "";

  const dictation = new Dictation((transcript) => {
    value = dictationBase === "" ? transcript : `${dictationBase} ${transcript}`;
  });

  const micIconPath =
    "M12 18.75a6 6 0 0 0 6-6v-1.5m-6 7.5a6 6 0 0 1-6-6v-1.5m6 7.5v3.75m-3.75 0h7.5M12 15.75a3 3 0 0 1-3-3V4.5a3 3 0 1 1 6 0v8.25a3 3 0 0 1-3 3Z";

  const canSubmit = $derived(value.trim() !== "" && !disabled);

  function toggleDictation() {
    if (!dictation.listening) dictationBase = value.trim();
    dictation.toggle();
  }

  function handleKeydown(event: KeyboardEvent) {
    // Enter sends; the athlete has a Shift+Enter for the rare multi-line entry.
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      if (canSubmit) submit();
    }
  }

  function submit() {
    if (dictation.listening) dictation.stop();
    onsubmit();
  }
</script>

<div class="flex flex-col gap-3">
  <label class="sr-only" for="quick-log-input">What did you do?</label>

  <div
    class="rounded-2xl border-2 border-base-content/10 bg-base-100 p-3 shadow-editorial
           focus-within:border-base-content/30"
  >
    <textarea
      id="quick-log-input"
      bind:value
      onkeydown={handleKeydown}
      rows="3"
      autocomplete="off"
      autocapitalize="sentences"
      placeholder="Sled pull+push worked up to 235, 2 or 3 sets, shoulder felt tweaky"
      class="w-full resize-none bg-transparent text-base leading-relaxed
             placeholder:text-base-content/30 focus:outline-hidden"
    ></textarea>

    <div class="mt-2 flex items-center gap-2">
      {#if dictation.available}
        <button
          type="button"
          onclick={toggleDictation}
          aria-pressed={dictation.listening}
          aria-label={dictation.listening ? "Stop dictating" : "Dictate"}
          class="btn btn-circle btn-sm border-2 min-h-11 h-11 w-11
                 {dictation.listening
            ? 'border-error/40 bg-error/10 text-error'
            : 'border-base-content/10 bg-base-200 text-base-content/60'}"
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
            stroke-width="1.5"
            stroke="currentColor"
            class="h-5 w-5"
            aria-hidden="true"
          >
            <path stroke-linecap="round" stroke-linejoin="round" d={micIconPath} />
          </svg>
        </button>
      {/if}

      {#if dictation.listening}
        <span class="text-xs font-bold uppercase tracking-widest text-error">Listening</span>
      {/if}

      <button
        type="button"
        onclick={submit}
        disabled={!canSubmit}
        class="btn btn-neutral ml-auto min-h-11 h-11 rounded-xl px-6 text-xs font-bold uppercase tracking-widest"
      >
        Log it
      </button>
    </div>
  </div>

  {#if dictation.problem}
    <p class="text-xs text-error">{dictation.problem}</p>
  {/if}
</div>
