<script lang="ts">
    import { bootUserDto, fetchBootUserDto } from "$lib/stores/stores";
    import { goto } from "$app/navigation";
    import { writable } from "svelte/store";
    import {
        LionheartClient,
        CreateProfileEndpointClient,
        LoginRequest,
        CreateProfileRequest,
    } from "$lib/api/ApiClient";

    let email = "";
    let password = "";
    let displayName = "";
    let age = "";
    let weight = "";
    let errorMessage = writable("");

    const lionheartClient = new LionheartClient();
    const createProfileClient = new CreateProfileEndpointClient();

    async function register() {
        try {
            // Note: Using manual fetch for register because the generated ApiClient
            // doesn't include useCookies/useSessionCookies query params
            const response = await fetch(
                "/register?useCookies=true&useSessionCookies=true",
                {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ email: email, password: password }),
                },
            );

            if (!response.ok) {
                var body = await response.json();
                if (body?.errors) {
                    // flatten all validation messages
                    const messages = Object.values(body.errors).flat();
                    errorMessage.set(messages.join(" "));
                } else if (body?.title) {
                    errorMessage.set(body.title);
                } else {
                    errorMessage.set("Registration failed.");
                }
                return;
            }
            await fetchBootUserDto(fetch);
            login();

            
        } catch (error) {
            if (error instanceof Error) {
                errorMessage.set(
                    `Failed to register your account: ${error.message}`,
                );
            } else {
                errorMessage.set(
                    "An unknown error occurred during registration.",
                );
            }
        }
    }

    async function login() {
        try {
            const request = new LoginRequest({ email, password });
            await lionheartClient.login(true, true, request);
            await fetchBootUserDto(fetch);
            if ($bootUserDto.hasCreatedProfile) {
                goto("/");
            }
        } catch (error) {
            if (error instanceof Error) {
                console.log("Login error: ", error);
            }
            errorMessage.set("Login failed. Ensure your email and password are correct.");
                          
        }
    }

    async function createProfile() {
        try {
            const request = new CreateProfileRequest({
                displayName: displayName,
                age: parseInt(age),
                weight: parseInt(weight),
            });
            await createProfileClient.post(request);
            await fetchBootUserDto(fetch);
            if ($bootUserDto.hasCreatedProfile) {
                goto("/");
            }
        } catch (error) {
            if (error instanceof Error) {
                errorMessage.set(
                    `Error creating your profile: ${error.message}`,
                );
            } else {
                errorMessage.set(
                    "An unknown error occurred while creating your profile.",
                );
            }
        }
    }
</script>

<svelte:head>
    <title>Authentication</title>
</svelte:head>
<div class="flex flex-col mx-auto">
    <div class="flex flex-row items-center justify-center">
        <h1 class="text-5xl font-thin mt-10">Project Lionheart</h1>
    </div>

    <div class="divider divider-neutral"></div>

    {#if $bootUserDto.name === null || $bootUserDto.name === ""}
        <div class="flex flex-col items-center justify-center">
            <fieldset
                class="fieldset bg-base-200 border-base-300 rounded-box w-full border p-4"
            >
                <label for="email" class="label">Email</label>
                <input
                    type="email"
                    bind:value={email}
                    id="email"
                    class="input w-full"
                    placeholder="My awesome page"
                />

                <label for="password" class="label">Password</label>
                <input
                    type="password"
                    bind:value={password}
                    id="password"
                    class="input w-full"
                    placeholder="my-awesome-page"
                />

                <div class="text-center pt-5 space-x-4">
                    <button
                        type="button"
                        on:click={register}
                        class="btn btn-primary p-2">Register</button
                    >
                    <button
                        type="button"
                        on:click={login}
                        class="btn btn-primary p-2">Login</button
                    >
                    {#if $errorMessage}
                        <p class="pt-4 w-64 break-words justify-center mx-auto">
                            <span class="text-error font-bold"
                                >{$errorMessage}</span
                            >
                        </p>
                    {/if}
                </div>
            </fieldset>
        </div>
    {:else if !$bootUserDto.hasCreatedProfile}
       

        <div class="flex flex-col items-center justify-center">
            <div class="flex flex-col items-center">
                <h1 class="text-xl font-bold">CREATE PROFILE</h1>
            </div>
          


            <fieldset
                class="fieldset bg-base-200 border-base-300 rounded-box w-full border p-4"
            >
                <label for="displayName" class="label">Name</label>
                <input
                    type="text"
                    bind:value={displayName}
                    id="displayName"
                    class="input w-full"
                    placeholder=""
                />

                <label for="age" class="label">Age</label>
                <input
                    type="number"
                    bind:value={age}
                    id="age"
                    class="input w-full"
                    placeholder=""
                />

                <label for="weight" class="label">Weight</label>
                <input
                    type="number"
                    bind:value={weight}
                    id="weight"
                    class="input w-full"
                    placeholder=""
                />

                <div class="text-center pt-5 space-x-4">
                   
                    <button
                        type="button"
                        on:click={createProfile}
                        class="btn btn-primary p-2">Create Profile</button
                    >
                    {#if $errorMessage}
                        <p class="pt-4 w-64 break-words justify-center mx-auto">
                            <span class="text-error font-bold"
                                >{$errorMessage}</span
                            >
                        </p>
                    {/if}
                </div>
            </fieldset>
        </div>
    {/if}
</div>
