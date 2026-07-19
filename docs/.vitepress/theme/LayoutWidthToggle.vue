<script setup lang="ts">
import { ref, onMounted } from 'vue'

const STORAGE_KEY = 'sm-docs-layout'

const fluid = ref(true)

function apply(value: boolean) {
  document.documentElement.classList.toggle('sm-fluid', value)
}

// The class is already set pre-paint by the inline head script in config.mts
// (avoids a layout flash on load); this just syncs the button state with it.
onMounted(() => {
  fluid.value = document.documentElement.classList.contains('sm-fluid')
})

function toggle() {
  fluid.value = !fluid.value
  apply(fluid.value)
  try {
    localStorage.setItem(STORAGE_KEY, fluid.value ? 'fluid' : 'centered')
  } catch {
    // Private browsing / storage disabled: toggle still works for the session.
  }
}
</script>

<template>
  <button
    class="SMLayoutWidthToggle"
    type="button"
    :title="fluid ? 'Switch to centered layout' : 'Switch to full-width layout'"
    :aria-label="fluid ? 'Switch to centered layout' : 'Switch to full-width layout'"
    :aria-pressed="fluid"
    @click="toggle"
  >
    <!-- minimize-2 / maximize-2 (Feather icons, MIT) -->
    <svg
      v-if="fluid"
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      stroke-linejoin="round"
    >
      <polyline points="4 14 10 14 10 20" />
      <polyline points="20 10 14 10 14 4" />
      <line x1="14" y1="10" x2="21" y2="3" />
      <line x1="3" y1="21" x2="10" y2="14" />
    </svg>
    <svg
      v-else
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      stroke-linejoin="round"
    >
      <polyline points="15 3 21 3 21 9" />
      <polyline points="9 21 3 21 3 15" />
      <line x1="21" y1="3" x2="14" y2="10" />
      <line x1="3" y1="21" x2="10" y2="14" />
    </svg>
  </button>
</template>

<style scoped>
.SMLayoutWidthToggle {
  display: none;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  margin-left: 8px;
  border-radius: 8px;
  color: var(--vp-c-text-2);
  transition: color 0.25s;
}

.SMLayoutWidthToggle:hover {
  color: var(--vp-c-text-1);
}

.SMLayoutWidthToggle svg {
  width: 18px;
  height: 18px;
}

/* Below 960px the sidebar collapses and both layouts render identically,
   so the toggle would be a no-op — hide it. */
@media (min-width: 960px) {
  .SMLayoutWidthToggle {
    display: flex;
  }
}
</style>
