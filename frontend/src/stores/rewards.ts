import { defineStore } from 'pinia'
import { fetchUserRewards } from '../services/api'
import type { UserRewardView } from '../types'

interface RewardsState {
  rewards: UserRewardView[]
  loading: boolean
  error: string | null
  requestId: number
}

export const useRewardsStore = defineStore('rewards', {
  state: (): RewardsState => ({
    rewards: [],
    loading: false,
    error: null,
    requestId: 0,
  }),
  actions: {
    async loadRewards(userId: string) {
      const requestId = ++this.requestId
      this.loading = true
      this.error = null

      try {
        const rewards = await fetchUserRewards(userId)
        if (requestId !== this.requestId) return
        this.rewards = rewards
      } catch {
        if (requestId !== this.requestId) return
        this.error = 'Unable to load reward badges right now.'
      } finally {
        if (requestId !== this.requestId) return
        this.loading = false
      }
    },
    clearRewards() {
      this.requestId += 1
      this.rewards = []
    },
  },
})
