// src/types/svelte-dnd-action.d.ts
/// <reference types="svelte" />

declare namespace svelteHTML {
  interface HTMLAttributes<T> {
    'on:consider'?: (e: CustomEvent<any>) => void;
    'on:finalize'?: (e: CustomEvent<any>) => void;
  }
}
