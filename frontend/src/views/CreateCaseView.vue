<script setup lang="ts">
import { reactive } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useCourtStore } from '../stores/court'

const courtStore = useCourtStore()
const authStore = useAuthStore()
const router = useRouter()

const form = reactive({
  title: '',
  category: '',
  summary: '',
  sideAUserId: '',
  sideAClaim: '',
  sideBUserId: '',
  sideBClaim: '',
})

function hydrateDefaultUsers() {
  if (authStore.users.length >= 2) {
    form.sideAUserId = authStore.selectedUser?.id ?? authStore.users[0].id
    form.sideBUserId = authStore.users.find((u) => u.id !== form.sideAUserId)?.id ?? authStore.users[1].id
  }
}

if (!authStore.users.length) {
  void authStore.loadUsers().then(hydrateDefaultUsers)
} else {
  hydrateDefaultUsers()
}

async function submit() {
  if (!form.sideAUserId || !form.sideBUserId) {
    return
  }

  const created = await courtStore.createCase({
    title: form.title,
    category: form.category,
    summary: form.summary,
    sideAUserId: form.sideAUserId,
    sideAClaim: form.sideAClaim,
    sideBUserId: form.sideBUserId,
    sideBClaim: form.sideBClaim,
  })

  if (created) {
    await router.push(`/cases/${created.id}`)
  }
}
</script>

<template>
  <section class="detail-shell">
    <p class="kicker">New Court Case</p>
    <h1>Create a New Argument Duel</h1>

    <form class="case-form" @submit.prevent="submit">
      <label>
        Title
        <input v-model="form.title" required />
      </label>

      <label>
        Category
        <input v-model="form.category" required />
      </label>

      <label>
        Summary
        <textarea v-model="form.summary" required rows="3" />
      </label>

      <div class="arguments">
        <section>
          <h2>Side A</h2>
          <label>
            User
            <select v-model="form.sideAUserId" required>
              <option v-for="user in authStore.users" :key="`a-${user.id}`" :value="user.id">
                {{ user.displayName }}
              </option>
            </select>
          </label>
          <label>
            Claim
            <textarea v-model="form.sideAClaim" required rows="4" />
          </label>
        </section>

        <section>
          <h2>Side B</h2>
          <label>
            User
            <select v-model="form.sideBUserId" required>
              <option v-for="user in authStore.users" :key="`b-${user.id}`" :value="user.id">
                {{ user.displayName }}
              </option>
            </select>
          </label>
          <label>
            Claim
            <textarea v-model="form.sideBClaim" required rows="4" />
          </label>
        </section>
      </div>

      <div class="action-bar">
        <button type="submit" class="action-btn" :disabled="courtStore.mutating">Create Case</button>
        <RouterLink to="/" class="case-link">Cancel</RouterLink>
      </div>
    </form>

    <p v-if="courtStore.error" class="notice error">{{ courtStore.error }}</p>
  </section>
</template>
