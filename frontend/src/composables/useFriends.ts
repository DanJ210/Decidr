import { computed, onMounted, ref, watch } from 'vue'
import { useAuthStore } from '../stores/auth'
import { useFriendsStore } from '../stores/friends'
import type { AppUser } from '../types'

export type UserStatus = 'friend' | 'request-sent' | 'request-received' | 'none'

export interface UserWithStatus {
  user: AppUser
  status: UserStatus
  requestId?: string
}

export function useFriends() {
  const authStore = useAuthStore()
  const friendsStore = useFriendsStore()

  const userSearchTerm = ref('')
  const friendSearchTerm = ref('')

  async function loadAll(userId: string) {
    friendsStore.setActiveUser(userId)
    await friendsStore.loadFriends(userId)
    await friendsStore.loadFriendRequests(userId)
    await friendsStore.loadOutgoingRequests(userId)
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
      .filter(
        (u) =>
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
    friendsStore.setActiveUser(userId)
    await friendsStore.sendRequest(toUserId)
  }

  async function respondToRequest(requestId: string, accept: boolean) {
    const userId = authStore.selectedUser?.id
    if (!userId) return

    const ok = await friendsStore.respondToRequest(requestId, accept)
    if (ok) {
      await friendsStore.loadFriends(userId)
    }
  }

  async function removeFriend(friendUserId: string) {
    const userId = authStore.selectedUser?.id
    if (!userId) return

    friendsStore.setActiveUser(userId)
    await friendsStore.removeFriend(friendUserId)
  }

  return {
    friendsStore,
    userSearchTerm,
    friendSearchTerm,
    normalizedUserSearch,
    userSearchResults,
    filteredFriends,
    fromUserName,
    toUserName,
    sendRequest,
    respondToRequest,
    removeFriend,
  }
}
