<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useCourtStore } from '../stores/court'
import { useFriendsStore } from '../stores/friends'

const courtStore = useCourtStore()
const authStore = useAuthStore()
const friendsStore = useFriendsStore()
const router = useRouter()

const form = reactive({
  title: '',
  category: '',
  summary: '',
  sideAClaim: '',
  invitedUserId: '',
})

async function loadData() {
  if (!authStore.users.length) {
    await authStore.loadUsers()
  }

  const userId = authStore.selectedUser?.id
  if (userId) {
    await friendsStore.loadFriends(userId)
  }

  if (!form.invitedUserId) {
    const firstFriend = friendsStore.friends[0]
    if (firstFriend) {
      form.invitedUserId = firstFriend.id
    }
  }
}

void loadData()

const inviteCandidates = computed(() => friendsStore.friends)

watch(inviteCandidates, (friends) => {
  if (!friends.length) {
    form.invitedUserId = ''
    return
  }

  if (!friends.some((friend) => friend.id === form.invitedUserId)) {
    form.invitedUserId = friends[0].id
  }
})

async function submit() {
  const userId = authStore.selectedUser?.id
  if (!userId || !form.invitedUserId) return

  const created = await courtStore.createCase({
    title: form.title,
    category: form.category,
    summary: form.summary,
    sideAUserId: userId,
    sideAClaim: form.sideAClaim,
    invitedUserId: form.invitedUserId,
  })

  if (created) {
    await router.push(`/cases/${created.id}`)
  }
}
</script>

<template>
  <section class="detail-shell">
    <p class="kicker">New Court Case</p>
    <h1>Start an Argument Duel</h1>

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
          <h2>Your Side (Side A)</h2>
          <p class="notice">
            Playing as: <strong>{{ authStore.selectedUser?.displayName ?? '—' }}</strong>
          </p>
          <label>
            Your Claim
            <textarea v-model="form.sideAClaim" required rows="4" placeholder="State your argument…" />
          </label>
        </section>

        <section>
          <h2>Invite to Side B</h2>
          <p class="notice" role="status" aria-live="polite">Pick a connected friend to write the opposing side.</p>
          <label v-if="inviteCandidates.length">
            Invite User
            <select v-model="form.invitedUserId" required>
              <option value="" disabled>Choose a friend…</option>
              <option v-for="user in inviteCandidates" :key="user.id" :value="user.id">
                {{ user.displayName }} (@{{ user.userName }})
              </option>
            </select>
          </label>
          <p v-else class="notice">
            You need at least one friend connection before creating a case.
            <RouterLink to="/friends" class="case-link">Manage friends</RouterLink>
          </p>
          <p class="notice">
            They will receive an invitation to write their response before the case goes live.
            Add friends from the Friends page to invite more people.
          </p>
        </section>
      </div>

      <div class="action-bar">
        <button
          type="submit"
          class="action-btn"
          :disabled="courtStore.mutating || !form.invitedUserId"
          :aria-describedby="form.invitedUserId ? undefined : 'create-case-help'"
        >
          Send Invitation &amp; Create Case
        </button>
        <RouterLink to="/" class="case-link">Cancel</RouterLink>
      </div>
      <p v-if="!form.invitedUserId" id="create-case-help" class="notice">
        Select a connected friend before sending the invitation.
      </p>
    </form>

    <p v-if="courtStore.error" class="notice error">{{ courtStore.error }}</p>
  </section>
</template>
