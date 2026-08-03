import { defineStore } from 'pinia'
import { entraConfigured, getActiveAccount, signIn, signOut } from '../authConfig'
import { fetchCurrentUser, fetchUsers } from '../services/api'
import type { AppUser } from '../types'

const selectedUserStorageKey = 'decidr-selected-user-id'

interface AuthState {
  users: AppUser[]
  selectedUserId: string | null
  loading: boolean
  error: string | null
  isAuthenticated: boolean
  configured: boolean
}

export const useAuthStore = defineStore('auth', {
  state: (): AuthState => ({
    users: [],
    selectedUserId: null,
    loading: false,
    error: null,
    isAuthenticated: false,
    configured: entraConfigured,
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
        if (this.configured) {
          if (!getActiveAccount()) {
            this.users = []
            this.selectedUserId = null
            this.isAuthenticated = false
            return
          }

          const currentUser = await fetchCurrentUser()
          this.users = [currentUser]
          this.selectedUserId = currentUser.id
          this.isAuthenticated = true
          return
        }

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
    async login() {
      this.loading = true
      this.error = null
      try {
        await signIn()
        await this.loadUsers()
      } catch {
        this.error = 'Unable to sign in right now.'
      } finally {
        this.loading = false
      }
    },
    async logout() {
      await signOut()
      this.users = []
      this.selectedUserId = null
      this.isAuthenticated = false
    },
    setSelectedUser(userId: string) {
      this.selectedUserId = userId
      localStorage.setItem(selectedUserStorageKey, userId)
    },
  },
})
