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
    <!-- <button class="btn" disabled="">Disabled using attribute</button> -->
    <div class="stats stats-vertical md:stats-horizontal shadow bg-accent">
        <div class="stat place-items-center">
          <div class="stat-title">Energy</div>
          <div class="stat-value">2 / 5</div>
          <div class="stat-desc">good</div>
        </div>
  
        <div class="stat place-items-center">
          <div class="stat-title">Mood</div>
          <div class="stat-value">3 / 5</div>
          <div class="stat-desc">great</div>
        </div>
  
        <div class="stat place-items-center">
          <div class="stat-title">Motivation</div>
          <div class="stat-value">4 / 5</div>
          <div class="stat-desc">okay</div>
        </div>
  
        <div class="stat place-items-center">
          <div class="stat-title">Stress</div>
          <div class="stat-value">1 / 5</div>
          <div class="stat-desc">bad</div>
        </div>
      </div>
    </div>

