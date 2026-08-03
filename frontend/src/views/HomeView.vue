<script setup lang="ts">
import { computed, ref } from 'vue'
import { ArrowRight, Bell, Flame, Trophy } from '@lucide/vue'
import { useHottestCases } from '../composables/useHottestCases'
import type { ArgumentCase, CaseStatus } from '../types'

const { courtStore, friendsStore, caseFeed } = useHottestCases()

type FeedFilter = 'All' | Extract<CaseStatus, 'Open' | 'Closed'>

const filters: FeedFilter[] = ['All', 'Open', 'Closed']
const activeFilter = ref<FeedFilter>('All')
const filteredCaseFeed = computed(() => {
  if (activeFilter.value === 'All') return caseFeed.value
  return caseFeed.value.filter((item) => item.status === activeFilter.value)
})

function totalVotes(item: ArgumentCase) {
  return item.verdict.votesForSideA + item.verdict.votesForSideB
}

function sideAPercentage(item: ArgumentCase) {
  const total = totalVotes(item)
  return total ? Math.round((item.verdict.votesForSideA / total) * 100) : 50
}
</script>

<template>
  <section v-if="friendsStore.invitations.length" class="invitation-tray" aria-labelledby="invitation-heading">
    <header class="invitation-heading">
      <span class="invitation-icon" aria-hidden="true"><Bell :size="18" /></span>
      <div>
        <p class="eyebrow">Your turn</p>
        <h2 id="invitation-heading">Case invitations</h2>
      </div>
      <span class="count-badge">{{ friendsStore.invitations.length }}</span>
    </header>
    <RouterLink
      v-for="invitation in friendsStore.invitations"
      :key="invitation.id"
      :to="`/cases/${invitation.id}`"
      class="invitation-item"
    >
      <span>
        <strong>{{ invitation.title }}</strong>
        <small>@{{ invitation.sideA.userName }} is waiting for your response</small>
      </span>
      <ArrowRight :size="18" aria-hidden="true" />
    </RouterLink>
  </section>

  <section class="feed-section" aria-labelledby="feed-heading">
    <header class="feed-hero">
      <div>
        <p class="eyebrow"><Flame :size="14" aria-hidden="true" /> Live community</p>
        <h1 id="feed-heading">Community cases</h1>
      </div>
      <span class="case-count">{{ filteredCaseFeed.length }} {{ filteredCaseFeed.length === 1 ? 'case' : 'cases' }}</span>
    </header>

    <div class="feed-filters" role="group" aria-label="Filter cases by status">
      <button
        v-for="filter in filters"
        :key="filter"
        type="button"
        :class="{ active: activeFilter === filter }"
        :aria-pressed="activeFilter === filter"
        @click="activeFilter = filter"
      >
        {{ filter }}
      </button>
    </div>

    <p v-if="courtStore.loading" class="notice">Loading live arguments...</p>
    <p v-else-if="courtStore.error" class="notice error">{{ courtStore.error }}</p>

    <ul v-else class="feed">
      <li v-for="item in filteredCaseFeed" :key="item.id" class="case-card feed-case-card">
        <div class="case-meta-row">
          <span class="pill">{{ item.category }}</span>
          <span class="status-dot-label" :class="item.status.toLowerCase()">
            <span aria-hidden="true"></span>{{ item.status }}
          </span>
        </div>

        <div class="case-card-copy">
          <h2><RouterLink :to="`/cases/${item.id}`">{{ item.title }}</RouterLink></h2>
          <p>{{ item.summary }}</p>
        </div>

        <div class="matchup-preview">
          <div class="matchup-side side-a">
            <span class="side-monogram" aria-hidden="true">A</span>
            <div>
              <strong>@{{ item.sideA.userName }}</strong>
              <p>{{ item.sideA.claim }}</p>
            </div>
          </div>
          <div class="matchup-side side-b">
            <span class="side-monogram" aria-hidden="true">B</span>
            <div v-if="item.sideB">
              <strong>@{{ item.sideB.userName }}</strong>
              <p>{{ item.sideB.claim }}</p>
            </div>
            <div v-else>
              <strong>Waiting for Side B</strong>
              <p>The invited participant has not responded yet.</p>
            </div>
          </div>
        </div>

        <div class="vote-snapshot">
          <div class="vote-snapshot-labels">
            <span>{{ totalVotes(item) ? 'Community split' : 'No votes yet' }}</span>
            <span v-if="totalVotes(item)">{{ sideAPercentage(item) }}% A · {{ 100 - sideAPercentage(item) }}% B</span>
          </div>
          <div
            class="vote-meter"
            :class="{ empty: totalVotes(item) === 0 }"
            role="img"
            :aria-label="totalVotes(item) ? `${sideAPercentage(item)} percent Side A and ${100 - sideAPercentage(item)} percent Side B` : 'No votes yet'"
          >
            <span class="vote-meter-a" :style="{ width: `${sideAPercentage(item)}%` }"></span>
            <span class="vote-meter-b"></span>
          </div>
        </div>

        <div class="case-card-footer">
          <span v-if="item.winnerSide" class="winner-label"><Trophy :size="15" /> Side {{ item.winnerSide }} won</span>
          <span v-else class="vote-total">{{ totalVotes(item) }} {{ totalVotes(item) === 1 ? 'vote' : 'votes' }}</span>
          <RouterLink :to="`/cases/${item.id}`" class="case-card-link">
            Review case <ArrowRight :size="17" aria-hidden="true" />
          </RouterLink>
        </div>
      </li>
    </ul>

    <p v-if="!courtStore.loading && !courtStore.error && !filteredCaseFeed.length" class="notice">
      No {{ activeFilter === 'All' ? '' : activeFilter.toLowerCase() + ' ' }}cases right now.
    </p>
  </section>
</template>


