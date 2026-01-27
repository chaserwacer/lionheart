<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import { marked } from "marked";

  type ChatMessage = {
    content?: string | null;
  };

  type ChatConversation = {
    chatConversationID: string;
    name: string;
    lastUpdate: string;
    messages: ChatMessage[];
  };

  let loadingPreview = true;
  let previewConversation: ChatConversation | null = null;
  let previewError = false;

  const quickPrompts: string[] = [
    "How am I trending this week (readiness, sleep, stress)?",
    "What should I focus on today for training and recovery?",
    "Any signs I'm overreaching in the last 7 days?",
  ];

  $: hasPreviewMessages =
    (previewConversation?.messages ?? []).filter((m) => (m.content ?? "").trim().length > 0).length > 0;

  function formatMessage(text: string | null | undefined): string {
    if (!text) return "";
    return marked.parse(text, { async: false }) as string;
  } 

  function goToChat() {
    goto("/chat");
  }

  function openChatWithPrefill(prompt: string) {
    goto(`/chat?prefill=${encodeURIComponent(prompt)}`);
  }

  function onCardKeydown(event: KeyboardEvent) {
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      goToChat();
    }
  }

  onMount(async () => {
    try {
      loadingPreview = true;
      previewError = false;

      const res = await fetch("/api/chat/conversation/most-recent?messageCount=2", {
        method: "GET",
      });

      if (res.status === 204) {
        previewConversation = null;
        return;
      }

      if (!res.ok) {
        previewConversation = null;
        previewError = true;
        return;
      }

      previewConversation = (await res.json()) as ChatConversation;
    } catch (e) {
      console.error("Failed to load chat preview", e);
      previewConversation = null;
      previewError = true;
    } finally {
      loadingPreview = false;
    }
  });
</script>

<!-- Card -->
<div
  role="button"
  tabindex="0"
  on:click={goToChat}
  on:keydown={onCardKeydown}
  class="card bg-base-100 shadow-editorial hover:shadow-editorial-lg transition-all duration-200
         cursor-pointer p-6 text-left w-full h-full
         border-2 border-base-content/10 hover:border-base-content/30"
>
  <div class="flex items-start justify-between mb-6">
    <div class="flex flex-col gap-1">
      <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Lionheart Intelligence</span>
      <h3 class="text-2xl font-display font-black tracking-tight">Chat</h3>
    </div>
    <svg
      xmlns="http://www.w3.org/2000/svg"
      fill="none"
      viewBox="0 0 24 24"
      stroke-width="2"
      stroke="currentColor"
      class="w-6 h-6 text-base-content/30"
    >
      <path stroke-linecap="round" stroke-linejoin="round" d="m8.25 4.5 7.5 7.5-7.5 7.5" />
    </svg>
  </div>

  {#if loadingPreview}
    <div class="mt-1">
      <div class="flex justify-start">
        <div class="max-w-[92%] rounded-2xl bg-base-200 border-2 border-base-content/10 px-4 py-3">
          <p class="text-xs font-bold uppercase tracking-wider text-base-content/50">Loading</p>
          <p class="text-sm font-medium mt-1">Fetching your latest conversation…</p>
        </div>
      </div>
    </div>
  {:else if hasPreviewMessages}
    <div class="grid grid-cols-1 gap-6 lg:grid-cols-3 lg:gap-4">
      <!-- Preview (2/3 on lg) -->
      <div class="space-y-3 lg:col-span-3 ">
        {#each previewConversation?.messages ?? [] as message, idx}
          {#if message.content && message.content.trim().length > 0}
            {#if idx % 2 === 0}
              <div class="flex justify-start">
                <div class="max-w-[92%] rounded-2xl bg-base-200 border-2 border-base-content/10 px-4 py-3">
                  <p class="text-xs font-bold uppercase tracking-wider text-base-content/50">You</p>
                  <div class="prose prose-sm max-w-none mt-1 text-base-content line-clamp-4">
                    {@html formatMessage(message.content)}
                  </div>
                </div>
              </div>
            {:else}
              <div class="flex justify-end">
                <div class="max-w-[92%] rounded-2xl bg-primary/10 border-2 border-primary/20 px-4 py-3">
                  <p class="text-xs font-bold uppercase tracking-wider text-primary/80">chat</p>
                  <div class="prose prose-sm max-w-none mt-1 text-base-content line-clamp-4">
                    {@html formatMessage(message.content)}
                  </div>
                </div>
              </div>
            {/if}
          {/if}
        {/each}
      </div>

      
    </div>
  {:else}
    <!-- Empty state: show only examples (no fake chat bubbles) -->
    <div class="mt-1 rounded-2xl bg-base-200 border-2 border-base-content/10 p-5 md:hidden">
      <div class="flex items-start justify-between gap-4">
        <div>
          <p class="text-xs font-bold uppercase tracking-widest text-base-content/50">New chat</p>
          <p class="text-sm font-medium mt-1 text-base-content/80">Tap anywhere on this card to start.</p>
        </div>
        {#if previewError}
          <span class="text-xs font-mono uppercase tracking-widest text-base-content/40">offline</span>
        {/if}
      </div>
    </div>

   
  {/if}

  <div class="mt-6 pt-4 border-t-2 border-base-content/10">
    <!-- Quick prompts (1/3 on lg) -->
      <div class="hidden md:block lg:col-span-1 pb-4">
        <div class="h-full rounded-2xl bg-base-200 border-2 border-base-content/10 p-4">
          <p class="text-xs font-bold uppercase tracking-widest text-base-content/50">Quick prompts</p>
          <p class="text-sm font-medium mt-1 text-base-content/80">Tap to prefill chat.</p>

          <div class="mt-4 space-y-2">
            {#each quickPrompts as prompt}
              <button
                type="button"
                class="w-full text-left btn bg-base-100 border-2 border-base-content/10 hover:border-base-content/30 hover:bg-base-100/60 justify-start h-auto min-h-0 py-3 px-4 rounded-xl"
                on:click={(ev) => {
                  ev.stopPropagation();
                  openChatWithPrefill(prompt);
                }}
              >
                <span class="text-xs font-mono uppercase tracking-wider text-base-content/60">+</span>
                <span class="ml-3 text-sm font-semibold normal-case text-base-content leading-snug line-clamp-2">{prompt}</span>
              </button>
            {/each}
          </div>
        </div>
      </div>
    <div class="flex items-center justify-between">
      <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">Open Chat</span>
      {#if previewError}
        <span class="text-xs font-mono uppercase tracking-widest text-base-content/40">offline</span>
      {:else}
        <span class="text-xs font-mono uppercase tracking-widest text-base-content/40">/chat</span>
      {/if}
    </div>
  </div>
</div>
