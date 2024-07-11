<script lang="ts">
    import { onMount } from 'svelte';
    import { goto } from '$app/navigation';
    import { bootUserDto } from '$lib/stores';

    let bootUser: { Name: any; HasCreatedProfile: any; };

    bootUserDto.subscribe(value => {
        bootUser = value;
    });

    onMount(() => {
        if (bootUser.Name === null) {
            goto('/auth');
        } else if (bootUser.HasCreatedProfile === false) {
            goto('/auth');
        }
    });
</script>

<div>
    <h1>Welcome to the Home Page</h1>
    <p>Hi {bootUser.Name}. Has created profile: {bootUser.HasCreatedProfile}</p>
</div>
