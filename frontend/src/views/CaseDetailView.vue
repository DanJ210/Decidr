<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useCourtStore } from '../stores/court'

const route = useRoute()
const courtStore = useCourtStore()
const authStore = useAuthStore()

onMounted(() => {
  const id = route.params.id
  if (typeof id === 'string') {
    void courtStore.loadCase(id)
  }
})

const caseItem = computed(() => courtStore.selectedCase)
const activeUser = computed(() => authStore.selectedUser)
const totalVotes = computed(() => {
  const selected = courtStore.selectedCase
  if (!selected) {
    return 0
  }

  return (
    selected.verdict.votesForSideA +
    selected.verdict.votesForSideB
  )
})

const canCloseCase = computed(() => {
  const selected = caseItem.value
  const user = activeUser.value
  if (!selected || !user || selected.status === 'Closed') {
    return false
  }

  const isParticipant = selected.sideA.userId === user.id || selected.sideB.userId === user.id
  const isModerator = user.role === 'Moderator'
  return isParticipant || isModerator
})

const closePermissionMessage = computed(() => {
  const selected = caseItem.value
  const user = activeUser.value
  if (!selected || selected.status === 'Closed') {
    return ''
  }

  if (!user) {
    return 'Select an active user to interact with this case.'
  }

  if (canCloseCase.value) {
    return user.role === 'Moderator'
      ? 'You can close this case as a moderator.'
      : 'You can close this case because you are one of the participants.'
  }

  return 'Only case participants or moderators can close this case.'
})

async function vote(side: 'A' | 'B') {
  const selectedUser = authStore.selectedUser
  const selectedCase = caseItem.value
  if (!selectedUser || !selectedCase) {
    return
  }

  const success = await courtStore.vote(selectedCase.id, selectedUser.id, side)
  if (success) {
    await courtStore.loadCase(selectedCase.id)
  }
}

async function closeCase() {
  const selectedCase = caseItem.value
  const user = activeUser.value
  if (!selectedCase || !user || !canCloseCase.value) {
    return
  }

  const success = await courtStore.closeCase(selectedCase.id, user.id)
  if (success) {
    await courtStore.loadCase(selectedCase.id)
  }
}
</script>

<template>
  <section>
    <p v-if="courtStore.loading" class="notice">Loading case details...</p>
    <p v-else-if="courtStore.error" class="notice error">{{ courtStore.error }}</p>

    <article v-else-if="caseItem" class="detail-shell">
      <header>
        <p class="kicker">{{ caseItem.category }}</p>
        <h1>{{ caseItem.title }}</h1>
        <p>{{ caseItem.summary }}</p>
      </header>

      <div class="arguments">
        <section>
          <h2>Side A · {{ caseItem.sideA.userName }}</h2>
          <p>{{ caseItem.sideA.claim }}</p>
        </section>
        <section>
          <h2>Side B · {{ caseItem.sideB.userName }}</h2>
          <p>{{ caseItem.sideB.claim }}</p>
        </section>
      </div>

      <section class="verdict">
        <h2>Community Verdict</h2>
        <p>Total votes: {{ totalVotes }}</p>
        <ul>
          <li>Side A: {{ caseItem.verdict.votesForSideA }}</li>
          <li>Side B: {{ caseItem.verdict.votesForSideB }}</li>
        </ul>

        <p class="status-text">
          Status: <strong>{{ caseItem.status }}</strong>
          <span v-if="caseItem.winnerSide"> · Winner: Side {{ caseItem.winnerSide }}</span>
        </p>

        <div class="action-bar">
          <button
            type="button"
            class="action-btn"
            :disabled="caseItem.status === 'Closed' || !authStore.selectedUser || courtStore.mutating"
            @click="vote('A')"
          >
            Vote Side A
          </button>
          <button
            type="button"
            class="action-btn"
            :disabled="caseItem.status === 'Closed' || !authStore.selectedUser || courtStore.mutating"
            @click="vote('B')"
          >
            Vote Side B
          </button>
          <button
            type="button"
            class="action-btn danger"
            :disabled="!canCloseCase || courtStore.mutating"
            @click="closeCase"
          >
            Close Case
          </button>
        </div>

        <p v-if="closePermissionMessage" class="status-text">{{ closePermissionMessage }}</p>
      </section>

      <RouterLink to="/" class="case-link">Back to Cases</RouterLink>
    </article>
  </section>
</template>
