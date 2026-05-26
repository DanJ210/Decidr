import { defineStore } from 'pinia'
import { fetchUserRewards } from '../services/api'
import type { UserRewardView } from '../types'

interface RewardsState {
  rewards: UserRewardView[]
  loading: boolean
  error: string | null
}

export const useRewardsStore = defineStore('rewards', {
  state: (): RewardsState => ({
    rewards: [],
    loading: false,
    error: null,
  }),
  actions: {
    async loadRewards(userId: string) {
      this.loading = true
      this.error = null

      try {
        this.rewards = await fetchUserRewards(userId)
      } catch {
        this.error = 'Unable to load reward badges right now.'
      } finally {
        this.loading = false
      }
    },
    clearRewards() {
      this.rewards = []
    },
  },
})
