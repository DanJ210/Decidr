<script setup lang="ts">
import { onMounted } from 'vue'
import { useAuthStore } from './stores/auth'
import BottomNav from './components/BottomNav.vue'

const authStore = useAuthStore()

onMounted(() => {
  if (!authStore.users.length) {
    void authStore.loadUsers()
  }
})
</script>

<template>
  <div class="app-shell">
    <header class="top-nav">
      <RouterLink to="/" class="brand">Decidr</RouterLink>

      <nav class="quick-links desktop-only" aria-label="Primary navigation">
        <RouterLink to="/" class="case-link">Feed</RouterLink>
        <RouterLink to="/cases/new" class="case-link">New Case</RouterLink>
        <RouterLink to="/friends" class="case-link">Friends</RouterLink>
        <RouterLink to="/rewards" class="case-link">Rewards</RouterLink>
      </nav>

      <label class="user-picker">
        <span>Active User</span>
        <select
          :value="authStore.selectedUserId ?? ''"
          @change="authStore.setSelectedUser(($event.target as HTMLSelectElement).value)"
        >
          <option v-for="user in authStore.users" :key="user.id" :value="user.id">
            {{ user.displayName }} ({{ user.userName }})
          </option>
        </select>
      </label>
    </header>

    <main class="main-content">
      <RouterView />
    </main>

    <BottomNav />
  </div>
</template>
