<script lang="ts">
    import { syncOuraData } from "$lib/ouraStore";
    let isLoading = false;
    let syncSuccess: boolean | null = null;

    async function syncOura() {
        isLoading = true;
        syncSuccess = null; // Reset the status

        try {
            const res = await syncOuraData(fetch);
            syncSuccess = res !== undefined && res !== null; // Ensure boolean value
        } catch (error) {
            syncSuccess = false;
        } finally {
            isLoading = false;
        }
    }
</script>

<button
    class="btn btn-sm btn-outline btn-primary"
    on:click={syncOura}
    disabled={isLoading}
>
    {#if isLoading}
    <span class="loading loading-spinner loading-xs"></span>
    {:else if syncSuccess === true}
        Success!
    {:else if syncSuccess === false}
        Failed
    {:else}
        Sync Oura
    {/if}
</button>
