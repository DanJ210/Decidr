<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { CircleAlert, LoaderCircle } from '@lucide/vue'
import { useRouter } from 'vue-router'
import { takeAuthenticationReturnPath } from '../authConfig'
import { useAuthStore } from '../stores/auth'

const authStore = useAuthStore()
const router = useRouter()
const failed = computed(() => authStore.authenticationStatus === 'error')

onMounted(async () => {
  await authStore.loadUsers()

  if (authStore.isAuthenticated) {
    await router.replace(takeAuthenticationReturnPath())
  } else if (authStore.authenticationStatus === 'signedOut') {
    await router.replace('/')
  }
})
</script>

<template>
  <section class="auth-callback" aria-live="polite">
    <CircleAlert v-if="failed" :size="28" aria-hidden="true" />
    <LoaderCircle v-else class="auth-callback-spinner" :size="28" aria-hidden="true" />
    <h1>{{ failed ? 'Sign-in interrupted' : 'Completing sign in' }}</h1>
    <p v-if="failed">Return home and try signing in again.</p>
    <RouterLink v-if="failed" class="auth-callback-action" to="/">Return home</RouterLink>
  </section>
</template>
  