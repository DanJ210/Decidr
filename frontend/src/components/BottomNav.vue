<script setup lang="ts">
import { computed } from 'vue'
import { ChartNoAxesColumnIncreasing, Home, Plus, Trophy, Users } from '@lucide/vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const isFeedActive = computed(() => route.path === '/' || (route.path.startsWith('/cases/') && route.path !== '/cases/new'))
</script>

<template>
  <div class="mobile-navigation">
    <nav class="bottom-nav" aria-label="Main navigation">
      <RouterLink to="/" class="bottom-nav-item" :class="{ active: isFeedActive }">
        <Home :size="21" :stroke-width="2.2" aria-hidden="true" />
        <span class="bottom-nav-label">Feed</span>
      </RouterLink>

      <RouterLink to="/friends" class="bottom-nav-item" :class="{ active: route.path === '/friends' }">
        <Users :size="21" :stroke-width="2.2" aria-hidden="true" />
        <span class="bottom-nav-label">Friends</span>
      </RouterLink>

      <span class="bottom-nav-spacer" aria-hidden="true"></span>

      <RouterLink to="/standings" class="bottom-nav-item" :class="{ active: route.path === '/standings' }">
        <ChartNoAxesColumnIncreasing :size="21" :stroke-width="2.2" aria-hidden="true" />
        <span class="bottom-nav-label">Standings</span>
      </RouterLink>

      <RouterLink to="/rewards" class="bottom-nav-item" :class="{ active: route.path === '/rewards' }">
        <Trophy :size="21" :stroke-width="2.2" aria-hidden="true" />
        <span class="bottom-nav-label">Rewards</span>
      </RouterLink>
    </nav>

    <RouterLink
      to="/cases/new"
      class="create-fab"
      :class="{ active: route.path === '/cases/new' }"
      aria-label="Create a new case"
    >
      <Plus :size="26" :stroke-width="2.4" aria-hidden="true" />
    </RouterLink>
  </div>
</template>
