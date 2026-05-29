import { defineStore } from 'pinia'
import {
  fetchFriendRequests,
  fetchFriends,
  fetchInvitations,
  fetchOutgoingFriendRequests,
  removeFriend as removeFriendApi,
  respondToFriendRequest,
  sendFriendRequest,
} from '../services/api'
import type { AppUser, ArgumentCase, FriendRequest } from '../types'

interface FriendsState {
  friends: AppUser[]
  incomingRequests: FriendRequest[]
  outgoingRequests: FriendRequest[]
  invitations: ArgumentCase[]
  activeUserId: string | null
  loading: boolean
  error: string | null
  outgoingError: string | null
}

export const useFriendsStore = defineStore('friends', {
  state: (): FriendsState => ({
    friends: [],
    incomingRequests: [],
    outgoingRequests: [],
    invitations: [],
    activeUserId: null,
    loading: false,
    error: null,
    outgoingError: null,
  }),
  actions: {
    setActiveUser(userId: string | null) {
      this.activeUserId = userId
    },
    async loadFriends(userId: string) {
      const requestUserId = userId
      this.loading = true
      this.error = null

      try {
        const friends = await fetchFriends(userId)
        if (this.activeUserId !== requestUserId) return
        this.friends = friends
      } catch {
        if (this.activeUserId !== requestUserId) return
        this.error = 'Unable to load friends right now.'
      } finally {
        if (this.activeUserId === requestUserId) {
          this.loading = false
        }
      }
    },
    async loadFriendRequests(userId: string) {
      const requestUserId = userId
      this.loading = true
      this.error = null

      try {
        const requests = await fetchFriendRequests(userId)
        if (this.activeUserId !== requestUserId) return
        this.incomingRequests = requests
      } catch {
        if (this.activeUserId !== requestUserId) return
        this.error = 'Unable to load friend requests right now.'
      } finally {
        if (this.activeUserId === requestUserId) {
          this.loading = false
        }
      }
    },
    async loadOutgoingRequests(userId: string) {
      const requestUserId = userId
      this.loading = true
      this.outgoingError = null

      try {
        const data = await fetchOutgoingFriendRequests(userId)
        if (this.activeUserId !== requestUserId) return
        this.outgoingRequests = data
      } catch {
        if (this.activeUserId !== requestUserId) return
        this.outgoingError = 'Unable to load sent friend requests right now.'
      } finally {
        if (this.activeUserId === requestUserId) {
          this.loading = false
        }
      }
    },
    async loadInvitations(userId: string) {
      this.loading = true
      this.error = null

      try {
        this.invitations = await fetchInvitations(userId)
      } catch {
        this.error = 'Unable to load invitations right now.'
      } finally {
        this.loading = false
      }
    },
    async sendRequest(fromUserId: string, toUserId: string): Promise<boolean> {
      this.error = null

      try {
        await sendFriendRequest({ fromUserId, toUserId })
        // Optimistically add to outgoing list so the UI updates immediately
        this.outgoingRequests = [
          ...this.outgoingRequests,
          {
            id: `temp-${Date.now()}`,
            fromUserId,
            toUserId,
            status: 'Pending' as const,
            createdAtUtc: new Date().toISOString(),
          },
        ]
        // Then sync with the server to get the real ID
        await this.loadOutgoingRequests(fromUserId)
        return true
      } catch {
        this.error = 'Unable to send friend request right now.'
        return false
      }
    },
    async respondToRequest(requestId: string, actorUserId: string, accept: boolean): Promise<boolean> {
      this.error = null

      try {
        await respondToFriendRequest(requestId, { actorUserId }, accept)
        this.incomingRequests = this.incomingRequests.filter((r) => r.id !== requestId)
        return true
      } catch {
        this.error = 'Unable to respond to this request right now.'
        return false
      }
    },
    async removeFriend(actorUserId: string, friendUserId: string): Promise<boolean> {
      this.error = null

      try {
        await removeFriendApi({ actorUserId, friendUserId })
        this.friends = this.friends.filter((f) => f.id !== friendUserId)
        return true
      } catch {
        this.error = 'Unable to remove this friend right now.'
        return false
      }
    },
    clearAll() {
      this.friends = []
      this.incomingRequests = []
      this.outgoingRequests = []
      this.invitations = []
      this.loading = false
      this.error = null
      this.outgoingError = null
    },
  },
})
