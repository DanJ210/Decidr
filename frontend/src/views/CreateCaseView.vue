<script setup lang="ts">
import { computed, reactive } from 'vue'
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
    const firstOther = authStore.users.find((u) => u.id !== authStore.selectedUser?.id)
    if (firstOther) {
      form.invitedUserId = firstOther.id
    }
  }
}

void loadData()

const friends = computed(() => friendsStore.friends)

const otherUsers = computed(() =>
  authStore.users.filter((u) => u.id !== authStore.selectedUser?.id),
)

const friendIds = computed(() => new Set(friends.value.map((f) => f.id)))

const inviteOptions = computed(() => {
  const friendList = otherUsers.value.filter((u) => friendIds.value.has(u.id))
  const nonFriends = otherUsers.value.filter((u) => !friendIds.value.has(u.id))
  return { friends: friendList, others: nonFriends }
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
          <p class="notice">Pick a friend or any user to write the opposing side.</p>
          <label>
            Invite User
            <select v-model="form.invitedUserId" required>
              <optgroup v-if="inviteOptions.friends.length" label="Friends">
                <option v-for="user in inviteOptions.friends" :key="user.id" :value="user.id">
                  {{ user.displayName }} (@{{ user.userName }})
                </option>
              </optgroup>
              <optgroup v-if="inviteOptions.others.length" :label="inviteOptions.friends.length ? 'Other Users' : 'Users'">
                <option v-for="user in inviteOptions.others" :key="user.id" :value="user.id">
                  {{ user.displayName }} (@{{ user.userName }})
                </option>
              </optgroup>
            </select>
          </label>
          <p class="notice">
            They will receive an invitation to write their response before the case goes live.
          </p>
        </section>
      </div>

      <div class="action-bar">
        <button type="submit" class="action-btn" :disabled="courtStore.mutating">
          Send Invitation &amp; Create Case
        </button>
        <RouterLink to="/" class="case-link">Cancel</RouterLink>
      </div>
    </form>

    <p v-if="courtStore.error" class="notice error">{{ courtStore.error }}</p>
  </section>
</template>

