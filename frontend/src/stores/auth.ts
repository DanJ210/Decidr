import { defineStore } from 'pinia'
import { fetchUsers } from '../services/api'
import type { AppUser } from '../types'

const selectedUserStorageKey = 'decidr-selected-user-id'

interface AuthState {
  users: AppUser[]
  selectedUserId: string | null
  loading: boolean
  error: string | null
}

export const useAuthStore = defineStore('auth', {
  state: (): AuthState => ({
    users: [],
    selectedUserId: null,
    loading: false,
    error: null,
  }),
  getters: {
    selectedUser(state): AppUser | null {
      return state.users.find((user) => user.id === state.selectedUserId) ?? null
    },
  },
  actions: {
    async loadUsers() {
      this.loading = true
      this.error = null

      try {
        this.users = await fetchUsers()
        const cachedUserId = localStorage.getItem(selectedUserStorageKey)
        const cachedUserExists = this.users.some((user) => user.id === cachedUserId)
        this.selectedUserId = cachedUserExists
          ? cachedUserId
          : this.users.length
            ? this.users[0].id
            : null

        if (this.selectedUserId) {
          localStorage.setItem(selectedUserStorageKey, this.selectedUserId)
        }
      } catch {
        this.error = 'Unable to load users right now.'
      } finally {
        this.loading = false
      }
    },
    setSelectedUser(userId: string) {
      this.selectedUserId = userId
      localStorage.setItem(selectedUserStorageKey, userId)
    },
  },
})
