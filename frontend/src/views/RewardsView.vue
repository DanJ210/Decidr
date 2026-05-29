<script setup lang="ts">
import { useGroupedRewards } from '../composables/useGroupedRewards'

const { authStore, rewardsStore, groupedRewards } = useGroupedRewards()
</script>

<template>
  <section class="detail-shell">
    <p class="kicker">Reward Badges</p>
    <h1>{{ authStore.selectedUser?.displayName ?? 'No User Selected' }} Reward Board</h1>

    <p v-if="rewardsStore.loading" class="notice">Loading badges...</p>
    <p v-else-if="rewardsStore.error" class="notice error">{{ rewardsStore.error }}</p>
    <p v-else-if="!rewardsStore.rewards.length" class="notice">
      No badges awarded yet. Participate in posting and voting to unlock rewards.
    </p>

    <div v-else class="reward-groups">
      <section v-for="(rewards, tier) in groupedRewards" :key="tier" class="reward-group">
        <h2>{{ tier }} Tier</h2>
        <ul class="reward-list">
          <li v-for="reward in rewards" :key="`${reward.badgeCode}-${reward.awardedAtUtc}`" class="reward-item">
            <span class="pill">{{ reward.iconKey }}</span>
            <div>
              <strong>{{ reward.badgeLabel }}</strong>
              <p>{{ reward.reason }}</p>
            </div>
          </li>
        </ul>
      </section>
    </div>
  </section>
</template>
