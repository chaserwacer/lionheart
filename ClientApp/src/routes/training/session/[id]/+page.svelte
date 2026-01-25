<script lang="ts">
    import { page } from '$app/stores';
    import { goto } from '$app/navigation';
    import { onMount } from 'svelte';

    $: sessionId = $page.params.id;

    function goBack() {
        goto('/training');
    }
</script>

<svelte:head>
    <title>Session - Lionheart</title>
</svelte:head>

<div class="min-h-full bg-base-200">
    <div class="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <!-- Header -->
        <header class="mb-8">
            <button
                on:click={goBack}
                class="flex items-center gap-2 text-base-content/50 hover:text-base-content transition-colors mb-4"
            >
                <span>&larr;</span>
                <span class="text-sm font-mono uppercase tracking-widest">Back to Training</span>
            </button>

            <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                <div>
                    <h1 class="text-5xl sm:text-6xl font-display font-black tracking-tightest text-base-content leading-none">
                        SESSION
                    </h1>
                    <p class="text-sm font-mono uppercase tracking-widest text-base-content/50 mt-3">
                        Session Details
                    </p>
                </div>

                <div class="flex items-center gap-2">
                    <button class="btn btn-primary px-5 rounded-xl">
                        Start Session
                    </button>
                    <button class="btn btn-outline px-5 rounded-xl">
                        Edit
                    </button>
                </div>
            </div>
        </header>

        <!-- Stub Content -->
        <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-8">
            <div class="text-center py-12">
                <h2 class="text-2xl font-display font-black mb-4">Session Detail Page</h2>
                <p class="text-base-content/50 mb-2">This page will display the full session details.</p>
                <p class="text-sm font-mono text-base-content/30">Session ID: {sessionId}</p>

                <div class="mt-8 p-6 bg-base-200 rounded-xl max-w-md mx-auto text-left">
                    <h3 class="font-bold mb-4 text-sm uppercase tracking-wider text-base-content/50">Planned Features</h3>
                    <ul class="space-y-2 text-sm text-base-content/70">
                        <li>- Session date and status</li>
                        <li>- List of all movements</li>
                        <li>- Set logging (reps, weight, time)</li>
                        <li>- Perceived effort rating</li>
                        <li>- Notes and comments</li>
                        <li>- Edit/duplicate/delete actions</li>
                    </ul>
                </div>
            </div>
        </div>
    </div>
</div>
