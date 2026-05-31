<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from './stores/auth'
import BottomNav from './components/BottomNav.vue'

const authStore = useAuthStore()
const menuOpen = ref(false)
const route = useRoute()

watch(route, () => {
  menuOpen.value = false
})

function toggleMenu() {
  menuOpen.value = !menuOpen.value
}

function closeMenu() {
  menuOpen.value = false
}

onMounted(() => {
  if (!authStore.users.length) {
    void authStore.loadUsers()
  }
})
</script>

<template>
  <div class="app-shell">
    <div class="top-area">
      <header class="top-nav">
        <RouterLink to="/" class="brand" @click="closeMenu">Decidr</RouterLink>

        <nav class="quick-links desktop-only" aria-label="Primary navigation">
          <RouterLink to="/" class="case-link">Feed</RouterLink>
          <RouterLink to="/cases/new" class="case-link">New Case</RouterLink>
          <RouterLink to="/friends" class="case-link">Friends</RouterLink>
          <RouterLink to="/rewards" class="case-link">Rewards</RouterLink>
        </nav>

        <button
          class="hamburger-btn mobile-only"
          :aria-expanded="menuOpen"
          :aria-label="menuOpen ? 'Close navigation menu' : 'Open navigation menu'"
          @click="toggleMenu"
        >
          <span class="hamburger-bar"></span>
          <span class="hamburger-bar"></span>
          <span class="hamburger-bar"></span>
        </button>
      </header>

      <nav v-if="menuOpen" class="hamburger-menu" aria-label="Site navigation">
        <RouterLink to="/" class="hamburger-item" @click="closeMenu">
          <span aria-hidden="true">🏠</span> Feed
        </RouterLink>
        <RouterLink to="/cases/new" class="hamburger-item" @click="closeMenu">
          <span aria-hidden="true">＋</span> New Case
        </RouterLink>
        <RouterLink to="/friends" class="hamburger-item" @click="closeMenu">
          <span aria-hidden="true">👥</span> Friends
        </RouterLink>
        <RouterLink to="/rewards" class="hamburger-item" @click="closeMenu">
          <span aria-hidden="true">🏆</span> Rewards
        </RouterLink>
      </nav>
    </div>

    <main class="main-content">
      <RouterView />
    </main>

    <BottomNav />
  </div>
</template>
