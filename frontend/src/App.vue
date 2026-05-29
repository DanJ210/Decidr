<script setup lang="ts">
import { onMounted } from 'vue'
import { useAuthStore } from './stores/auth'

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
      <div class="nav-brand">
        <RouterLink to="/" class="brand">Decidr Court</RouterLink>
        <p>Settle internet disputes with public votes</p>
      </div>

      <div class="top-controls">
        <nav class="quick-links">
          <RouterLink to="/cases/new" class="case-link">New Case</RouterLink>
          <RouterLink to="/friends" class="case-link">Friends</RouterLink>
          <RouterLink to="/rewards" class="case-link">My Rewards</RouterLink>
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
      </div>
    </header>

    <main>
      <RouterView />
    </main>
  </div>
</template>
