<script lang="ts">
    import { onMount } from 'svelte';
    import { goto } from '$app/navigation';
    import { bootUserDto, fetchBootUserDto } from '$lib/stores';

    // let bootUser: { name: string; hasCreatedProfile: boolean; };

    // bootUserDto.subscribe(value => {
    //     bootUser = value;
    //     bootUser = bootUser
    //     value = value
    //     console.log("Home Page, subscribe, value is: ", value)
    // });

    onMount(async () => {
        await fetchBootUserDto(fetch)
        //console.log("BootUserDto:  ", $bootUserDto)
        if ($bootUserDto.name === null || !$bootUserDto.hasCreatedProfile) {
            goto('/auth');
        } 
    });

    
</script>

<div>
    <h1>Welcome to the Home Page</h1>
    <p>Hi {$bootUserDto.name}. Has created profile: {$bootUserDto.hasCreatedProfile}</p>
</div>
