<script lang="ts">
  import { currentStep } from '$lib/stores/ingestionStore';
  import type { WizardStep } from '$lib/stores/ingestionStore';

  const steps: { key: WizardStep; label: string }[] = [
    { key: 'input', label: 'Input' },
    { key: 'review', label: 'Review & Resolve' },
    { key: 'commit', label: 'Commit' },
    { key: 'done', label: 'Done' },
  ];

  function stepIndex(step: WizardStep): number {
    return steps.findIndex(s => s.key === step);
  }
</script>

<ul class="steps steps-horizontal w-full mb-8">
  {#each steps as step, i}
    <li class="step {stepIndex($currentStep) >= i ? 'step-primary' : ''}">
      {step.label}
    </li>
  {/each}
</ul>
