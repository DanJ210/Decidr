<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useAuthStore } from '../stores/auth'
import { useFriendsStore } from '../stores/friends'

const authStore = useAuthStore()
const friendsStore = useFriendsStore()

const addFriendId = ref('')
const searchTerm = ref('')

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

const normalizedSearch = computed(() => searchTerm.value.trim().toLowerCase())

const filteredUsers = computed(() =>
  authStore.users
    .filter((u) => u.id !== authStore.selectedUser?.id)
    .filter((u) => {
      if (!normalizedSearch.value) return true
      return (
        u.displayName.toLowerCase().includes(normalizedSearch.value) ||
        u.userName.toLowerCase().includes(normalizedSearch.value)
      )
    }),
)

const friendIds = computed(() => new Set(friendsStore.friends.map((f) => f.id)))
const incomingRequesterIds = computed(() => new Set(friendsStore.incomingRequests.map((r) => r.fromUserId)))
const outgoingRequesteeIds = computed(() => new Set(friendsStore.outgoingRequests.map((r) => r.toUserId)))

const searchableCandidates = computed(() =>
  filteredUsers.value.filter(
    (u) =>
      !friendIds.value.has(u.id) &&
      !incomingRequesterIds.value.has(u.id) &&
      !outgoingRequesteeIds.value.has(u.id),
  ),
)

const fromUserName = computed(() => {
  return (fromUserId: string) =>
    authStore.users.find((u) => u.id === fromUserId)?.displayName ?? fromUserId
})

const toUserName = computed(() => {
  return (toUserId: string) =>
    authStore.users.find((u) => u.id === toUserId)?.displayName ?? toUserId
})

async function sendRequest() {
  const userId = authStore.selectedUser?.id
  if (!userId || !addFriendId.value) return

  const ok = await friendsStore.sendRequest(userId, addFriendId.value)
  if (ok) {
    addFriendId.value = ''
    await friendsStore.loadFriendRequests(userId)
  }
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

    <section class="board">
      <header class="board-header">
        <h2>Search Users</h2>
      </header>
      <form class="case-form" @submit.prevent>
        <label>
          Name or username
          <input v-model="searchTerm" placeholder="Search users..." />
        </label>
      </form>
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
    <section v-if="friendsStore.outgoingRequests.length" class="board">
      <header class="board-header">
        <h2>Sent Requests</h2>
        <span>{{ friendsStore.outgoingRequests.length }} pending</span>
      </header>
      <ul class="case-grid">
        <li v-for="req in friendsStore.outgoingRequests" :key="req.id" class="case-card">
          <p>Friend request sent to <strong>{{ toUserName(req.toUserId) }}</strong>.</p>
          <span class="pill">Pending</span>
        </li>
      </ul>
    </section>

    <!-- Friends list -->
    <section class="board">
      <header class="board-header">
        <h2>My Friends</h2>
        <span>{{ friendsStore.friends.length }}</span>
      </header>
      <p v-if="!friendsStore.friends.length && !friendsStore.loading" class="notice">
        You haven't added any friends yet.
      </p>
      <ul v-else class="case-grid">
        <li v-for="friend in friendsStore.friends" :key="friend.id" class="case-card">
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

    <!-- Add friend -->
    <section class="board">
      <header class="board-header">
        <h2>Add a Friend</h2>
      </header>
      <form class="case-form" @submit.prevent="sendRequest">
        <label>
          Select User
          <select v-model="addFriendId" required>
            <option value="" disabled>Choose a user…</option>
            <option v-for="user in searchableCandidates" :key="user.id" :value="user.id">
              {{ user.displayName }} (@{{ user.userName }})
            </option>
          </select>
        </label>
        <div class="action-bar">
          <button type="submit" class="action-btn" :disabled="!addFriendId">
            Send Friend Request
          </button>
        </div>
      </form>
      <p v-if="!searchableCandidates.length" class="notice">
        No users available to add for this search.
      </p>
    </section>
  </section>
</template>
