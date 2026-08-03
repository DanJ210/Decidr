<script setup lang="ts">
import { computed } from 'vue'
import { Award, Crown, Feather, Gavel, Sparkles, Target, Trophy } from '@lucide/vue'
import { useGroupedRewards } from '../composables/useGroupedRewards'

const { authStore, rewardsStore, groupedRewards } = useGroupedRewards()

const tierRanks: Record<string, number> = { Gold: 3, Silver: 2, Bronze: 1 }
const rewardIcons = { jury: Gavel, target: Target, quill: Feather, crown: Crown }

const orderedRewardGroups = computed(() =>
  Object.entries(groupedRewards.value).sort(([leftTier], [rightTier]) =>
    (tierRanks[rightTier] ?? 0) - (tierRanks[leftTier] ?? 0),
  ),
)

const highestTier = computed(() => orderedRewardGroups.value[0]?.[0] ?? 'Unranked')

function rewardIcon(iconKey: string) {
  return rewardIcons[iconKey as keyof typeof rewardIcons] ?? Award
}

function formatAwardDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', year: 'numeric' }).format(new Date(value))
}
</script>

<template>
  <section class="detail-shell rewards-page">
    <header class="secondary-page-header rewards-header">
      <div>
        <p class="eyebrow">Recognition</p>
        <h1>{{ authStore.selectedUser?.displayName ?? 'Your' }} achievements</h1>
        <p>Every case joined and vote cast can leave a mark on your community record.</p>
      </div>
      <span class="rewards-header-mark" aria-hidden="true"><Trophy :size="22" /></span>
    </header>

    <p v-if="rewardsStore.loading" class="notice">Loading badges...</p>
    <p v-else-if="rewardsStore.error" class="notice error">{{ rewardsStore.error }}</p>

    <div v-else-if="!rewardsStore.rewards.length" class="rewards-empty-state">
      <span><Sparkles :size="22" /></span>
      <div>
        <h2>Your first badge is waiting.</h2>
        <p>Join a case or vote on an open debate to start your record.</p>
        <RouterLink to="/" class="case-card-link">Browse community cases</RouterLink>
      </div>
    </div>

    <template v-else>
      <section class="achievement-summary" aria-label="Achievement summary">
        <div>
          <strong>{{ rewardsStore.rewards.length }}</strong>
          <span>{{ rewardsStore.rewards.length === 1 ? 'badge earned' : 'badges earned' }}</span>
        </div>
        <div>
          <strong>{{ orderedRewardGroups.length }}</strong>
          <span>{{ orderedRewardGroups.length === 1 ? 'active tier' : 'active tiers' }}</span>
        </div>
        <div>
          <strong>{{ highestTier }}</strong>
          <span>highest tier</span>
        </div>
      </section>

      <div class="reward-groups">
        <section
          v-for="([tier, rewards], groupIndex) in orderedRewardGroups"
          :key="tier"
          class="reward-group"
          :class="`tier-${tier.toLowerCase()}`"
        >
          <header class="reward-tier-heading">
            <span class="tier-seal"><Crown v-if="tier === 'Gold'" :size="18" /><Award v-else :size="18" /></span>
            <div>
              <p class="eyebrow">Tier {{ groupIndex + 1 }}</p>
              <h2>{{ tier }}</h2>
            </div>
            <span class="count-badge">{{ rewards.length }}</span>
          </header>

          <ul class="reward-list">
            <li v-for="reward in rewards" :key="`${reward.badgeCode}-${reward.awardedAtUtc}`" class="reward-item">
              <span class="reward-icon" aria-hidden="true">
                <component :is="rewardIcon(reward.iconKey)" :size="21" />
              </span>
              <div class="reward-copy">
                <strong>{{ reward.badgeLabel }}</strong>
                <p>{{ reward.reason }}</p>
                <time :datetime="reward.awardedAtUtc">Earned {{ formatAwardDate(reward.awardedAtUtc) }}</time>
              </div>
            </li>
          </ul>
        </section>
      </div>
    </template>
  </section>
</template>
