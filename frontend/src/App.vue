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

function handleUserChange(event: Event) {
  const userId = (event.target as HTMLSelectElement).value
  if (userId) {
    authStore.setSelectedUser(userId)
  }
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
          type="button"
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

      <div class="active-user-picker">
        <label for="active-user-select">Active User</label>
        <select
          id="active-user-select"
          :value="authStore.selectedUserId ?? ''"
          :disabled="authStore.loading || !authStore.users.length"
          @change="handleUserChange"
        >
          <option value="" disabled>Select a user</option>
          <option v-for="user in authStore.users" :key="user.id" :value="user.id">
            {{ user.displayName }}
          </option>
        </select>
      </div>

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
