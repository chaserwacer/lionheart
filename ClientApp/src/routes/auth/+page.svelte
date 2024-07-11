<script lang="ts">
    import { bootUserDto, fetchBootUserDto } from '$lib/stores';
    import { goto } from '$app/navigation';

    let email = '';
    let password = '';
    let displayName = '';
    let age = '';
    let weight = '';
    let stage = 'register';

    async function register() {
        // Perform registration logic here
        // After registration, update the BootUserDto

        await fetchBootUserDto();
        bootUserDto.subscribe(value => {
            if (value.Name !== null) {
                stage = 'createProfile';
            }
        });
    }

    async function createProfile() {
        // Perform profile creation logic here
        // After profile creation, update the BootUserDto

        await fetchBootUserDto();
        bootUserDto.subscribe(value => {
            if (value.HasCreatedProfile) {
                goto('/');
            }
        });
    }
</script>

<div class="flex flex-row items-center justify-center">
    <img class="h-1/4 w-1/4" src="/src/assets/logo.png" alt="Lion Logo"> 
    <!-- <div class="divider divider-horizontal divider-neutral"></div> -->
    <h1 class="text-6xl font-bold">Welcome to <br> Project Lionheart</h1> 
</div>
<div class="divider divider-neutral"></div>

{#if stage === 'register'}

<div class="flex flex-col items-center justify-center ">
    <div>
        <h1 class="text-2xl font-bold">REGISTER / LOGIN</h1>
    </div>
    <div class="w-6/12">
        <form on:submit|preventDefault={register} class="space-y-4">
            <div class="form-control">
                <label for="email" class="label">
                    <span class="label-text">Email</span>
                </label>
                <input type="email" id="email" bind:value={email} class="input input-bordered" required />
            </div>
            <div class="form-control">
                <label for="password" class="label">
                    <span class="label-text">Password</span>
                </label>
                <input type="password" id="password" bind:value={password} class="input input-bordered" required />
            </div>
            <div class="text-center">
                <button type="submit" class="btn btn-primary">Register</button>
                <button type="submit" class="btn btn-primary">Login</button>
            </div>
            
            
        </form>
    </div>
</div>
<div class="divider divider-neutral"></div>
{/if}

{#if stage === 'createProfile'}
<div class="container mx-auto">
    <h1 class="text-2xl font-bold">Create Profile</h1>
    <form on:submit|preventDefault={createProfile} class="space-y-4">
        <div class="form-control">
            <label for="displayName" class="label">
                <span class="label-text">Display Name</span>
            </label>
            <input type="text" id="displayName" bind:value={displayName} class="input input-bordered" required />
        </div>
        <div class="form-control">
            <label for="age" class="label">
                <span class="label-text">Age</span>
            </label>
            <input type="number" id="age" bind:value={age} class="input input-bordered" required />
        </div>
        <div class="form-control">
            <label for="weight" class="label">
                <span class="label-text">Weight</span>
            </label>
            <input type="number" id="weight" bind:value={weight} class="input input-bordered" required />
        </div>
        <button type="submit" class="btn btn-primary">Create Profile</button>
    </form>
</div>
{/if}
