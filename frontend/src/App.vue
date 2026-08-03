<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { ChevronDown, Scale } from '@lucide/vue'
import { useAuthStore } from './stores/auth'
import BottomNav from './components/BottomNav.vue'

const authStore = useAuthStore()
const selectedUserInitial = computed(() => authStore.selectedUser?.displayName.charAt(0).toUpperCase() ?? '?')

function handleUserChange(event: Event) {
  const userId = (event.target as HTMLSelectElement).value
  if (userId) {
    authStore.setSelectedUser(userId)
  }
}

onMounted(() => {
  void authStore.loadUsers()
})
</script>

<template>
  <div class="app-shell">
    <div class="top-area">
      <header class="top-nav">
        <RouterLink to="/" class="brand" aria-label="Decidr home">
          <span class="brand-mark" aria-hidden="true"><Scale :size="18" :stroke-width="2.4" /></span>
          <span>Decidr</span>
        </RouterLink>

        <nav class="quick-links desktop-only" aria-label="Primary navigation">
          <RouterLink to="/" class="top-nav-link">Feed</RouterLink>
          <RouterLink to="/cases/new" class="top-nav-link">Create</RouterLink>
          <RouterLink to="/friends" class="top-nav-link">Friends</RouterLink>
          <RouterLink to="/rewards" class="top-nav-link">Rewards</RouterLink>
        </nav>

        <label v-if="!authStore.configured" class="user-switcher" for="active-user-select">
          <span class="user-avatar" aria-hidden="true">{{ selectedUserInitial }}</span>
          <span class="user-switcher-copy">
            <span class="user-switcher-label">Active profile</span>
            <span class="user-switcher-name">{{ authStore.selectedUser?.displayName ?? 'Select user' }}</span>
          </span>
          <ChevronDown class="user-switcher-chevron" :size="16" aria-hidden="true" />
        <select
          id="active-user-select"
          aria-label="Switch active profile"
          :value="authStore.selectedUserId ?? ''"
          :disabled="authStore.loading || !authStore.users.length"
          @change="handleUserChange"
        >
          <option value="" disabled>Select a user</option>
          <option v-for="user in authStore.users" :key="user.id" :value="user.id">
            {{ user.displayName }}
          </option>
        </select>
        </label>

        <button v-else-if="!authStore.isAuthenticated" class="user-switcher" type="button" :disabled="authStore.loading" @click="authStore.login">
          <span class="user-avatar" aria-hidden="true">?</span>
          <span class="user-switcher-copy">
            <span class="user-switcher-label">Account</span>
            <span class="user-switcher-name">Sign in</span>
          </span>
        </button>

        <button v-else class="user-switcher" type="button" @click="authStore.logout">
          <span class="user-avatar" aria-hidden="true">{{ selectedUserInitial }}</span>
          <span class="user-switcher-copy">
            <span class="user-switcher-label">Signed in</span>
            <span class="user-switcher-name">{{ authStore.selectedUser?.displayName }}</span>
          </span>
        </button>
      </header>
    </div>

    <main class="main-content">
      <RouterView />
    </main>

    <BottomNav />
  </div>
</template>
