<script lang="ts">
    import { onMount } from 'svelte';
    import { goto } from '$app/navigation';
    import { bootUserDto, fetchBootUserDto } from '$lib/stores';
    import { load } from './+layout';

    let bootUser: { name: string; hasCreatedProfile: boolean; };

    bootUserDto.subscribe(value => {
        bootUser = value;
        bootUser = bootUser
        value = value
        console.log("Home Page, subscribe, value is: ", value)
    });

    onMount(async () => {
        await fetchBootUserDto(fetch)
        
        bootUser = bootUser
        console.log("Home Page on mount, bootuser is: ", bootUser)
        console.log("Name: ", bootUser.name)
        if (bootUser.name == null || !bootUser.hasCreatedProfile) {
            goto('/auth');
        } 
    });

    
</script>

<div>
    <h1>Welcome to the Home Page</h1>
    <p>Hi {bootUser.name}. Has created profile: {bootUser.hasCreatedProfile}</p>
</div>
