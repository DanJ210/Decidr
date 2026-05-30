import { computed, watchEffect } from 'vue'
import { useAuthStore } from '../stores/auth'
import { useRewardsStore } from '../stores/rewards'

export function useGroupedRewards() {
  const authStore = useAuthStore()
  const rewardsStore = useRewardsStore()

  watchEffect(() => {
    const userId = authStore.selectedUserId
    if (!userId) {
      rewardsStore.clearRewards()
      return
    }

    void rewardsStore.loadRewards(userId)
  })

  const groupedRewards = computed(() => {
    return rewardsStore.rewards.reduce<Record<string, typeof rewardsStore.rewards>>((acc, reward) => {
      if (!acc[reward.tier]) {
        acc[reward.tier] = []
      }

      acc[reward.tier].push(reward)
      return acc
    }, {})
  })

  return { authStore, rewardsStore, groupedRewards }
}
