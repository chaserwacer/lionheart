<script>
    import { onMount } from 'svelte';
    import { goto } from '$app/navigation';
  
    let username = '';
    let password = '';
    /**
     * @type {null}
     */
    let user = null;
  
    async function signup() {
      const res = await fetch('/api/User', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password, name: '', age: 0, weight: 0 })
      });
  
      if (res.ok) {
        user = await res.json();
        goto('/dashboard'); // Redirect to dashboard or another protected route
      } else {
        alert('Signup failed');
      }
    }
  
    async function login() {
      const res = await fetch('/api/User/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password })
      });
  
      if (res.ok) {
        user = await res.json();
        goto('/dashboard'); // Redirect to dashboard or another protected route
      } else {
        alert('Login failed');
      }
    }
  
    onMount(() => {
      // Check if user is already signed in
      // If so, redirect to dashboard or another protected route
    });
  </script>
  
  <style>
    /* Add your styles here */
  </style>
  
  {#if !user}
    <div>
      <h1>Login / Signup</h1>
      <input type="text" bind:value={username} placeholder="Username" />
      <input type="password" bind:value={password} placeholder="Password" />
      <button on:click={signup}>Sign Up</button>
      <button on:click={login}>Login</button>
    </div>
  {/if}
  