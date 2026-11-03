<script lang="ts">
  let { path, class: className = 'w-6 h-6' }: { path: string; class?: string } = $props();
</script>

<svg
  xmlns="http://www.w3.org/2000/svg"
  fill="none"
  viewBox="0 0 24 24"
  stroke-width="1.5"
  stroke="currentColor"
  class={className}
  aria-hidden="true"
>
  <path stroke-linecap="round" stroke-linejoin="round" d={path} />
</svg>
