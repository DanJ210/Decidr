<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useCourtStore } from '../stores/court'

const route = useRoute()
const router = useRouter()
const courtStore = useCourtStore()
const authStore = useAuthStore()

const sideBClaim = ref('')

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
  if (!selected) return 0
  return selected.verdict.votesForSideA + selected.verdict.votesForSideB
})

const isInvited = computed(() => {
  const selected = caseItem.value
  const user = activeUser.value
  return (
    selected?.status === 'Pending' &&
    !!user &&
    selected.invitedUserId === user.id
  )
})

const inviterName = computed(() => {
  const selected = caseItem.value
  if (!selected) return ''
  return selected.sideA.userName
})

const isParticipant = computed(() => {
  const selected = caseItem.value
  const user = activeUser.value
  if (!selected || !user) return false
  return selected.sideA.userId === user.id || selected.sideB?.userId === user.id
})

const canVote = computed(() => {
  const selected = caseItem.value
  const user = activeUser.value
  return !!selected && !!user && selected.status === 'Open' && !isParticipant.value
})

const canCloseCase = computed(() => {
  const selected = caseItem.value
  const user = activeUser.value
  if (!selected || !user || selected.status !== 'Open') return false

  const isModerator = user.role === 'Moderator'
  return isParticipant.value || isModerator
})

const closePermissionMessage = computed(() => {
  const selected = caseItem.value
  const user = activeUser.value
  if (!selected || selected.status !== 'Open') return ''
  if (!user) return 'Select an active user to interact with this case.'
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
  if (!selectedUser || !selectedCase) return

  const success = await courtStore.vote(selectedCase.id, selectedUser.id, side)
  if (success) {
    await courtStore.loadCase(selectedCase.id)
  }
}

async function closeCase() {
  const selectedCase = caseItem.value
  const user = activeUser.value
  if (!selectedCase || !user || !canCloseCase.value) return

  const success = await courtStore.closeCase(selectedCase.id, user.id)
  if (success) {
    await courtStore.loadCase(selectedCase.id)
  }
}

async function acceptInvitation() {
  const selectedCase = caseItem.value
  const user = activeUser.value
  if (!selectedCase || !user || !sideBClaim.value.trim()) return

  const success = await courtStore.acceptInvitation(selectedCase.id, user.id, sideBClaim.value.trim())
  if (success) {
    await courtStore.loadCase(selectedCase.id)
    sideBClaim.value = ''
  }
}

async function declineInvitation() {
  const selectedCase = caseItem.value
  const user = activeUser.value
  if (!selectedCase || !user) return

  const success = await courtStore.declineInvitation(selectedCase.id, user.id)
  if (success) {
    await router.push('/')
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

      <!-- Pending: invitation banner for the invited user -->
      <section v-if="caseItem.status === 'Pending'" class="verdict">
        <div v-if="isInvited">
          <h2>You've Been Invited!</h2>
          <p>
            <strong>@{{ inviterName }}</strong> has invited you to argue the opposing side of this case.
            Write your response below to make this case go live.
          </p>

          <div class="arguments">
            <section>
              <h2>Side A · {{ caseItem.sideA.userName }}</h2>
              <p>{{ caseItem.sideA.claim }}</p>
            </section>
            <section>
              <h2>Your Side (Side B)</h2>
              <label>
                Your Claim
                <textarea v-model="sideBClaim" rows="4" placeholder="State your opposing argument…" required />
              </label>
            </section>
          </div>

          <div class="action-bar">
            <button
              type="button"
              class="action-btn"
              :disabled="!sideBClaim.trim() || courtStore.mutating"
              @click="acceptInvitation"
            >
              Accept &amp; Go Live
            </button>
            <button
              type="button"
              class="action-btn danger"
              :disabled="courtStore.mutating"
              @click="declineInvitation"
            >
              Decline Invitation
            </button>
          </div>
        </div>
        <div v-else>
          <h2>Awaiting Response</h2>
          <p>
            <strong>@{{ caseItem.sideA.userName }}</strong> has started this case and is waiting for
            the invited user to write their side before it goes live.
          </p>
          <div class="arguments">
            <section>
              <h2>Side A · {{ caseItem.sideA.userName }}</h2>
              <p>{{ caseItem.sideA.claim }}</p>
            </section>
            <section>
              <h2>Side B</h2>
              <p class="notice">Pending — waiting for the invited user to respond.</p>
            </section>
          </div>
        </div>
      </section>

      <!-- Open / Closed: full case view -->
      <template v-else>
        <div class="arguments">
          <section>
            <h2>Side A · {{ caseItem.sideA.userName }}</h2>
            <p>{{ caseItem.sideA.claim }}</p>
          </section>
          <section>
            <h2>Side B · {{ caseItem.sideB?.userName }}</h2>
            <p>{{ caseItem.sideB?.claim }}</p>
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
              :disabled="!canVote || courtStore.mutating"
              @click="vote('A')"
            >
              Vote Side A
            </button>
            <button
              type="button"
              class="action-btn"
              :disabled="!canVote || courtStore.mutating"
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

          <p v-if="caseItem.status === 'Open' && isParticipant" class="status-text">
            You are a participant in this case and cannot vote.
          </p>
          <p v-if="closePermissionMessage" class="status-text">{{ closePermissionMessage }}</p>
        </section>
      </template>

      <RouterLink to="/" class="case-link">Back to Cases</RouterLink>
    </article>
  </section>
</template>

