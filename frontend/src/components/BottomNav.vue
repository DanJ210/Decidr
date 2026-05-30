<script setup lang="ts">
import { useRoute } from 'vue-router'

const route = useRoute()
</script>

<template>
  <nav class="bottom-nav" aria-label="Main navigation">
    <RouterLink
      to="/"
      class="bottom-nav-item"
      :class="{ active: route.path === '/' }"
      aria-label="Feed"
    >
      <span class="bottom-nav-icon" aria-hidden="true">🏠</span>
      <span class="bottom-nav-label">Feed</span>
    </RouterLink>

    <RouterLink
      to="/friends"
      class="bottom-nav-item"
      :class="{ active: route.path === '/friends' }"
      aria-label="Friends"
    >
      <span class="bottom-nav-icon" aria-hidden="true">👥</span>
      <span class="bottom-nav-label">Friends</span>
    </RouterLink>

    <RouterLink to="/cases/new" class="bottom-nav-fab" aria-label="New Case">
      <span aria-hidden="true">＋</span>
    </RouterLink>

    <RouterLink
      to="/rewards"
      class="bottom-nav-item"
      :class="{ active: route.path === '/rewards' }"
      aria-label="Rewards"
    >
      <span class="bottom-nav-icon" aria-hidden="true">🏆</span>
      <span class="bottom-nav-label">Rewards</span>
    </RouterLink>
  </nav>
</template>
