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
  loading: boolean
  error: string | null
}

export const useFriendsStore = defineStore('friends', {
  state: (): FriendsState => ({
    friends: [],
    incomingRequests: [],
    outgoingRequests: [],
    invitations: [],
    loading: false,
    error: null,
  }),
  actions: {
    async loadFriends(userId: string) {
      this.loading = true
      this.error = null

      try {
        this.friends = await fetchFriends(userId)
      } catch {
        this.error = 'Unable to load friends right now.'
      } finally {
        this.loading = false
      }
    },
    async loadFriendRequests(userId: string) {
      this.loading = true
      this.error = null

      try {
        this.incomingRequests = await fetchFriendRequests(userId)
      } catch {
        this.error = 'Unable to load friend requests right now.'
      } finally {
        this.loading = false
      }
    },
    async loadOutgoingRequests(userId: string) {
      this.loading = true
      this.error = null

      try {
        const data = await fetchOutgoingFriendRequests(userId)
        this.outgoingRequests = Array.isArray(data) ? data : []
      } catch {
        this.error = 'Unable to load sent friend requests right now.'
      } finally {
        this.loading = false
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
    },
  },
})
