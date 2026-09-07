<script lang="ts">
  import { page } from "$app/state";
  import { goto } from "$app/navigation";

  // A session's membership in a program is one optional field on the session, not a second place
  // it lives, so there is one canonical detail route and this nested one only points at it.
  $effect(() => {
    const sessionId = page.params.sessionId;
    if (sessionId) goto(`/training/session/${sessionId}`, { replaceState: true });
  });
</script>

<svelte:head>
  <title>Session - Lionheart</title>
</svelte:head>

<p class="p-8 text-sm text-base-content/50">Opening the session…</p>
