<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useAuthStore } from '../stores/auth'
import { useFriendsStore } from '../stores/friends'

type UserStatus = 'friend' | 'request-sent' | 'request-received' | 'none'

interface UserWithStatus {
  user: (typeof authStore.users)[0]
  status: UserStatus
  requestId?: string
}

const authStore = useAuthStore()
const friendsStore = useFriendsStore()

const userSearchTerm = ref('')
const friendSearchTerm = ref('')

async function loadAll(userId: string) {
  await Promise.all([
    friendsStore.loadFriends(userId),
    friendsStore.loadFriendRequests(userId),
    friendsStore.loadOutgoingRequests(userId),
  ])
}

onMounted(async () => {
  if (!authStore.users.length) {
    await authStore.loadUsers()
  }

  const userId = authStore.selectedUser?.id
  if (userId) {
    await loadAll(userId)
  }
})

watch(
  () => authStore.selectedUserId,
  async (userId) => {
    if (userId) {
      friendsStore.clearAll()
      await loadAll(userId)
    }
  },
)

const friendIds = computed(() => new Set(friendsStore.friends.map((f) => f.id)))

const normalizedUserSearch = computed(() => userSearchTerm.value.trim().toLowerCase())
const normalizedFriendSearch = computed(() => friendSearchTerm.value.trim().toLowerCase())

const userSearchResults = computed((): UserWithStatus[] => {
  if (!normalizedUserSearch.value) return []

  return authStore.users
    .filter((u) => u.id !== authStore.selectedUser?.id)
    .filter((u) =>
      u.displayName.toLowerCase().includes(normalizedUserSearch.value) ||
      u.userName.toLowerCase().includes(normalizedUserSearch.value),
    )
    .map((u): UserWithStatus => {
      if (friendIds.value.has(u.id)) return { user: u, status: 'friend' }
      const outgoing = friendsStore.outgoingRequests.find((r) => r.toUserId === u.id)
      if (outgoing) return { user: u, status: 'request-sent' }
      const incoming = friendsStore.incomingRequests.find((r) => r.fromUserId === u.id)
      if (incoming) return { user: u, status: 'request-received', requestId: incoming.id }
      return { user: u, status: 'none' }
    })
})

const filteredFriends = computed(() => {
  if (!normalizedFriendSearch.value) return friendsStore.friends
  return friendsStore.friends.filter(
    (f) =>
      f.displayName.toLowerCase().includes(normalizedFriendSearch.value) ||
      f.userName.toLowerCase().includes(normalizedFriendSearch.value),
  )
})

const fromUserName = (fromUserId: string) =>
  authStore.users.find((u) => u.id === fromUserId)?.displayName ?? fromUserId

const toUserName = (toUserId: string) =>
  authStore.users.find((u) => u.id === toUserId)?.displayName ?? toUserId

async function sendRequest(toUserId: string) {
  const userId = authStore.selectedUser?.id
  if (!userId) return
  await friendsStore.sendRequest(userId, toUserId)
}

async function respondToRequest(requestId: string, accept: boolean) {
  const userId = authStore.selectedUser?.id
  if (!userId) return

  const ok = await friendsStore.respondToRequest(requestId, userId, accept)
  if (ok) {
    await friendsStore.loadFriends(userId)
  }
}

async function removeFriend(friendUserId: string) {
  const userId = authStore.selectedUser?.id
  if (!userId) return

  await friendsStore.removeFriend(userId, friendUserId)
}
</script>

<template>
  <section class="detail-shell">
    <p class="kicker">Social</p>
    <h1>Friends</h1>

    <p v-if="friendsStore.loading" class="notice">Loading...</p>
    <p v-if="friendsStore.error" class="notice error">{{ friendsStore.error }}</p>

    <!-- Find People -->
    <section class="board">
      <header class="board-header">
        <h2>Find People</h2>
      </header>
      <form class="case-form" @submit.prevent>
        <label>
          Search by name or username
          <input v-model="userSearchTerm" placeholder="Start typing to search…" autocomplete="off" />
        </label>
      </form>

      <ul v-if="userSearchResults.length" class="case-grid search-results">
        <li v-for="{ user, status, requestId } in userSearchResults" :key="user.id" class="case-card">
          <div class="top-row">
            <h3>{{ user.displayName }}</h3>
            <span v-if="status === 'friend'" class="pill pill-friend">Friend</span>
            <span v-else-if="status === 'request-sent'" class="pill pill-pending">Pending</span>
            <span v-else-if="status === 'request-received'" class="pill pill-incoming">Wants to connect</span>
          </div>
          <p>@{{ user.userName }}</p>
          <div v-if="status === 'none'" class="action-bar">
            <button class="action-btn" :disabled="friendsStore.loading" @click="sendRequest(user.id)">
              Send Friend Request
            </button>
          </div>
          <div v-else-if="status === 'request-received'" class="action-bar">
            <button class="action-btn" @click="respondToRequest(requestId!, true)">Accept</button>
            <button class="action-btn danger" @click="respondToRequest(requestId!, false)">Decline</button>
          </div>
        </li>
      </ul>
      <p v-else-if="normalizedUserSearch" class="notice" style="margin-top:1rem">
        No users found matching "{{ userSearchTerm }}".
      </p>
    </section>

    <!-- Incoming friend requests -->
    <section v-if="friendsStore.incomingRequests.length" class="board">
      <header class="board-header">
        <h2>Friend Requests</h2>
        <span>{{ friendsStore.incomingRequests.length }} pending</span>
      </header>
      <ul class="case-grid">
        <li v-for="req in friendsStore.incomingRequests" :key="req.id" class="case-card">
          <p><strong>{{ fromUserName(req.fromUserId) }}</strong> wants to be your friend.</p>
          <div class="action-bar">
            <button class="action-btn" @click="respondToRequest(req.id, true)">Accept</button>
            <button class="action-btn danger" @click="respondToRequest(req.id, false)">Decline</button>
          </div>
        </li>
      </ul>
    </section>

    <!-- Outgoing pending friend requests -->
    <section v-if="friendsStore.outgoingRequests.length || friendsStore.outgoingError" class="board">
      <header class="board-header">
        <h2>Sent Requests</h2>
        <span>{{ friendsStore.outgoingRequests.length }} pending</span>
      </header>
      <p v-if="friendsStore.outgoingError" class="notice error">{{ friendsStore.outgoingError }}</p>
      <ul class="case-grid">
        <li v-for="req in friendsStore.outgoingRequests" :key="req.id" class="case-card">
          <p>Friend request sent to <strong>{{ toUserName(req.toUserId) }}</strong>.</p>
          <span class="pill pill-pending">Pending</span>
        </li>
      </ul>
    </section>

    <!-- Friends list -->
    <section class="board">
      <header class="board-header">
        <h2>My Friends</h2>
        <span>{{ friendsStore.friends.length }}</span>
      </header>
      <form v-if="friendsStore.friends.length > 3" class="case-form friend-filter" @submit.prevent>
        <label>
          <input v-model="friendSearchTerm" placeholder="Filter friends…" />
        </label>
      </form>
      <p v-if="!friendsStore.friends.length && !friendsStore.loading" class="notice">
        You haven't added any friends yet.
      </p>
      <p v-else-if="friendsStore.friends.length && !filteredFriends.length" class="notice">
        No friends match "{{ friendSearchTerm }}".
      </p>
      <ul v-else class="case-grid">
        <li v-for="friend in filteredFriends" :key="friend.id" class="case-card">
          <h3>{{ friend.displayName }}</h3>
          <p>@{{ friend.userName }}</p>
          <span class="pill">{{ friend.role }}</span>
          <div class="action-bar">
            <button type="button" class="action-btn danger" @click="removeFriend(friend.id)">
              Remove Friend
            </button>
          </div>
        </li>
      </ul>
    </section>
  </section>
</template>

<style scoped>
.search-results {
  margin-top: 1rem;
}

.friend-filter {
  margin-bottom: 1rem;
}

.pill-friend {
  background: #dcfce7;
  color: #14532d;
}

.pill-pending {
  background: #fef9c3;
  color: #713f12;
}

.pill-incoming {
  background: #e0f2fe;
  color: #0c4a6e;
}
</style>
