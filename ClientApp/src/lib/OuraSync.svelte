<script lang="ts">
    import { syncOuraData } from "$lib/ouraStore";
    import { pageUpdate } from "$lib/stores";
    let isLoading = false;
    let syncSuccess: boolean | null = null;

    async function syncOura() {
        isLoading = true;
        syncSuccess = null; // Reset the status

        try {
            const res = await syncOuraData(fetch);
            syncSuccess = res !== undefined && res !== null; // Ensure boolean value
            $pageUpdate = new Date();
        } catch (error) {
            syncSuccess = false;
        } finally {
            isLoading = false;
        }
    }
</script>

<button
    class="btn btn-sm btn-outline btn-primary {isLoading ? 'btn-disabled' : ''}"
    on:click={syncOura}
    
>
    {#if isLoading}
        Syncing....
    {:else if syncSuccess === true}
        Oura Sync Success!
    {:else if syncSuccess === false}
        Oura Sync Failed
    {:else}
        Sync Oura Data
    {/if}
</button>
