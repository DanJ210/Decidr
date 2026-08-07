import { defineStore } from 'pinia'
import { ensureAccessToken, entraConfigured, getActiveAccount, initializeMsal, signIn, signOut, takeAuthenticationError } from '../authConfig'
import { fetchCurrentUser, fetchUsers } from '../services/api'
import type { AppUser } from '../types'

const selectedUserStorageKey = 'decidr-selected-user-id'
type AuthenticationStatus = 'signedOut' | 'authenticating' | 'accountPresent' | 'profileReady' | 'error'

interface AuthState {
  users: AppUser[]
  selectedUserId: string | null
  loading: boolean
  error: string | null
  authenticationStatus: AuthenticationStatus
  configured: boolean
}

export const useAuthStore = defineStore('auth', {
  state: (): AuthState => ({
    users: [],
    selectedUserId: null,
    loading: false,
    error: null,
    authenticationStatus: 'signedOut',
    configured: entraConfigured,
  }),
  getters: {
    isAuthenticated(state): boolean {
      return state.authenticationStatus === 'profileReady'
    },
    hasMicrosoftAccount(state): boolean {
      return state.authenticationStatus === 'accountPresent' || state.authenticationStatus === 'profileReady' ||
        (state.authenticationStatus === 'error' && getActiveAccount() !== null)
    },
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
          await initializeMsal()

          const authenticationError = takeAuthenticationError()
          if (authenticationError) {
            this.error = authenticationError
          }

          const account = getActiveAccount()
          if (!account) {
            this.users = []
            this.selectedUserId = null
            this.authenticationStatus = authenticationError ? 'error' : 'signedOut'
            return
          }

          this.authenticationStatus = 'accountPresent'
          if (!await ensureAccessToken()) {
            this.authenticationStatus = 'authenticating'
            return
          }

          const currentUser = await fetchCurrentUser()
          const users = await fetchUsers()
          this.users = users.some((user) => user.id === currentUser.id)
            ? users
            : [currentUser, ...users]
          this.selectedUserId = currentUser.id
          this.authenticationStatus = 'profileReady'
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
      } catch (error) {
        this.users = []
        this.selectedUserId = null
        this.authenticationStatus = 'error'
        this.error = error instanceof Error
          ? error.message
          : 'Unable to load the signed-in Decidr profile.'
      } finally {
        this.loading = false
      }
    },
    async login() {
      this.loading = true
      this.error = null
      this.authenticationStatus = 'authenticating'
      try {
        await signIn()
      } catch (error) {
        this.authenticationStatus = 'error'
        this.error = error instanceof Error
          ? error.message
          : 'Unable to sign in right now.'
      } finally {
        this.loading = false
      }
    },
    async logout() {
      this.users = []
      this.selectedUserId = null
      this.authenticationStatus = 'signedOut'
      await signOut()
    },
    setSelectedUser(userId: string) {
      this.selectedUserId = userId
      localStorage.setItem(selectedUserStorageKey, userId)
    },
  },
})
