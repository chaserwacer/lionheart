<script lang="ts">
    import { bootUserDto, fetchBootUserDto } from "$lib/stores";
    import { goto } from "$app/navigation";
    import { page } from "$app/stores";
    import { onMount } from "svelte";
    import { writable } from "svelte/store";
    import { AuthenticationHeaderParser } from "@azure/msal-browser";

    let email = "";
    let password = "";
    let displayName = "";
    let age = "";
    let weight = "";
    let errorMessage = writable("");

    async function register() {
        try {
            const response = await fetch(
                "/register?useCookies=true&useSessionCookies=true",
                {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                    },
                    body: JSON.stringify({
                        email: email,
                        password: password,
                    }),
                },
            );

            if (response.ok) {
                await fetchBootUserDto(fetch);
            } else {
                const errorText = await response.text();
                errorMessage.set(`Failed to register your account: ${errorText}`);
            }
        } catch (error) {
            errorMessage.set('An unknown error occurred during registration.');
        }
    }

    async function login() {
        try {
            const response = await fetch(
                "/login?useCookies=true&useSessionCookies=true",
                {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                    },
                    body: JSON.stringify({
                        email: email,
                        password: password,
                    }),
                },
            );

            if (response.ok) {
                await fetchBootUserDto(fetch);
                if ($bootUserDto.hasCreatedProfile) {
                    goto("/");
                }
            } else {
                const errorText = await response.text();
                errorMessage.set(`Failed to log you in: ${errorText}`);
            }
        } catch (error) {
            if (error instanceof Error) {
                errorMessage.set(`Error logging you in: ${error.message}`);
            } else {
                errorMessage.set('An unknown error occurred during login.');
            }
        }
    }

    async function createProfile() {
        try {
            const response = await self.fetch("/api/user/createProfile", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify({
                    displayName: displayName,
                    age: parseInt(age),
                    weight: parseInt(weight),
                }),
            });

            if (response.ok) {
                await fetchBootUserDto(fetch);
                if ($bootUserDto.hasCreatedProfile) {
                    goto("/");
                }
            } else {
                console.error("Failed to create profile:", response.statusText);
            }
        } catch (error) {
            console.error("Error creating your profile:", error);
        }
    }
</script>

<svelte:head>
    <title>Authentication</title>
</svelte:head>

<div class="flex flex-row items-center justify-center">
    <img class="h-1/4 w-1/4" src="/src/assets/logo.png" alt="Lion Logo" />
    <!-- <div class="divider divider-horizontal divider-neutral"></div> -->
    <h1 class="text-6xl font-bold">Welcome to <br /> Project Lionheart</h1>
</div>

<div class="divider divider-neutral"></div>

{#if $bootUserDto.name === null || $bootUserDto.name === ""}
    <div class="flex flex-col items-center justify-center">
        <div>
            <h1 class="text-2xl font-bold">REGISTER / LOGIN</h1>
        </div>
        <div class="w-6/12">
            <div class="form-control">
                <label for="email" class="label">
                    <span class="label-text">Email</span>
                </label>
                <input
                    type="email"
                    id="email"
                    bind:value={email}
                    class="input input-bordered"
                    required
                />
            </div>
            <div class="form-control">
                <label for="password" class="label">
                    <span class="label-text">Password</span>
                </label>
                <input
                    type="password"
                    id="password"
                    bind:value={password}
                    class="input input-bordered"
                    required
                />
            </div>
            <div class="text-center pt-5">
                <button
                    type="button"
                    on:click={register}
                    class="btn btn-primary">Register</button
                >
                <button type="button" on:click={login} class="btn btn-primary"
                    >Login</button
                >
            </div>
        </div>
    </div>
    <div class="divider divider-neutral"></div>
{:else if !$bootUserDto.hasCreatedProfile}
    <h1 class="text-center">Welcome {$bootUserDto.name}, thank you for registering your account. Please continue the sign up process via creating 
        a profile.
    </h1>

    <div class="divider divider-neutral"></div>
    <div class="flex flex-col items-center justify-center">
        <div class="flex flex-col items-center">       
            <h1 class="text-2xl font-bold">CREATE PROFILE</h1>
        </div>
        <div class="w-6/12">
            <form on:submit|preventDefault={createProfile} class="space-y-4">
                <div class="form-control">
                    <label for="displayName" class="label">
                        <span class="label-text">Display Name</span>
                    </label>
                    <input
                        type="text"
                        id="displayName"
                        bind:value={displayName}
                        class="input input-bordered"
                        required
                    />
                </div>
                <div class="form-control">
                    <label for="age" class="label">
                        <span class="label-text">Age</span>
                    </label>
                    <input
                        type="number"
                        id="age"
                        bind:value={age}
                        class="input input-bordered"
                        required
                    />
                </div>
                <div class="form-control">
                    <label for="weight" class="label">
                        <span class="label-text">Weight</span>
                    </label>
                    <input
                        type="number"
                        id="weight"
                        bind:value={weight}
                        class="input input-bordered"
                        required
                    />
                </div>
                <button type="submit" class="btn btn-primary"
                    >Create Profile</button
                >
            </form>
        </div>
    </div>
{/if}
{#if $errorMessage}
<div role="alert" class="alert alert-error">
    <svg
      xmlns="http://www.w3.org/2000/svg"
      class="h-6 w-6 shrink-0 stroke-current"
      fill="none"
      viewBox="0 0 24 24">
      <path
        stroke-linecap="round"
        stroke-linejoin="round"
        stroke-width="2"
        d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z" />
    </svg>
    <span>{$errorMessage}</span>
  </div>
{/if}