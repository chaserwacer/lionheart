<script lang="ts">
    import { bootUserDto, fetchBootUserDto, rememberProfileCreated } from "$lib/stores/stores";
    import { goto } from "$app/navigation";
    import { LionheartClient, LoginRequest } from "$lib/api/ApiClient";

    let email = $state("");
    let password = $state("");
    let displayName = $state("");
    let age = $state("");
    let weight = $state("");
    let errorMessage = $state("");

    const lionheartClient = new LionheartClient();

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
                    errorMessage = messages.join(" ");
                } else if (body?.title) {
                    errorMessage = body.title;
                } else {
                    errorMessage = "Registration failed.";
                }
                return;
            }
            await fetchBootUserDto(fetch);
            login();

            
        } catch (error) {
            if (error instanceof Error) {
                errorMessage = `Failed to register your account: ${error.message}`;
            } else {
                errorMessage = "An unknown error occurred during registration.";
            }
        }
    }

    async function login() {
        try {
            const request = new LoginRequest({ email, password });
            await lionheartClient.login(true, true, request);
            await fetchBootUserDto(fetch);
            if ($bootUserDto.hasCreatedProfile) {
                goto("/log");
            }
        } catch (error) {
            if (error instanceof Error) {
                console.log("Login error: ", error);
            }
            errorMessage = "Login failed. Ensure your email and password are correct.";
                          
        }
    }

    /**
     * Creating a profile is also how the app learns one already exists: the endpoint answers 409
     * in that case, which is the only read of that fact the API offers. Either answer settles it.
     */
    async function createProfile() {
        errorMessage = "";

        const response = await fetch("/api/user/create-profile", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                displayName,
                age: parseInt(age),
                weight: parseInt(weight),
            }),
        });

        if (!response.ok && response.status !== 409) {
            errorMessage = `Could not create your profile (${response.status}).`;
            return;
        }

        rememberProfileCreated($bootUserDto.name ?? email);
        await fetchBootUserDto(fetch);
        goto("/log");
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
                        onclick={register}
                        class="btn btn-primary p-2">Register</button
                    >
                    <button
                        type="button"
                        onclick={login}
                        class="btn btn-primary p-2">Login</button
                    >
                    {#if errorMessage}
                        <p class="pt-4 w-64 break-words justify-center mx-auto">
                            <span class="text-error font-bold"
                                >{errorMessage}</span
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
                        onclick={createProfile}
                        class="btn btn-primary p-2">Create Profile</button
                    >
                    {#if errorMessage}
                        <p class="pt-4 w-64 break-words justify-center mx-auto">
                            <span class="text-error font-bold"
                                >{errorMessage}</span
                            >
                        </p>
                    {/if}
                </div>
            </fieldset>
        </div>
    {/if}
</div>
