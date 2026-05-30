<script setup lang="ts">
import { useHottestCases } from '../composables/useHottestCases'

const { courtStore, friendsStore, caseFeed } = useHottestCases()
</script>

<template>
  <!-- Pending invitations for the current user -->
  <section v-if="friendsStore.invitations.length" class="board">
    <header class="board-header">
      <h2>My Invitations</h2>
      <span class="pill">{{ friendsStore.invitations.length }}</span>
    </header>
    <ul class="case-grid">
      <li v-for="inv in friendsStore.invitations" :key="inv.id" class="case-card">
        <div class="top-row">
          <span class="pill">{{ inv.category }}</span>
          <span class="status-pill status-open">Awaiting You</span>
        </div>
        <h3>{{ inv.title }}</h3>
        <p>{{ inv.summary }}</p>
        <div class="participants">
          <span>⚖️ {{ inv.sideA.userName }}</span>
          <span>↔️ You</span>
        </div>
        <div class="card-actions">
          <RouterLink :to="`/cases/${inv.id}`" class="action-btn" style="text-align:center;text-decoration:none;">
            Respond to Invitation
          </RouterLink>
        </div>
      </li>
    </ul>
  </section>

  <!-- Case feed -->
  <section>
    <header class="feed-header">
      <h2 class="feed-title">🔥 Community Cases</h2>
      <span class="votes">{{ caseFeed.length }} case(s)</span>
    </header>

    <p v-if="courtStore.loading" class="notice">Loading live arguments...</p>
    <p v-else-if="courtStore.error" class="notice error">{{ courtStore.error }}</p>

    <ul v-else class="feed">
      <li v-for="item in caseFeed" :key="item.id" class="case-card">
        <div class="top-row">
          <span class="pill">{{ item.category }}</span>
          <span class="votes">{{ item.verdict.votesForSideA + item.verdict.votesForSideB }} votes</span>
        </div>
        <h3>{{ item.title }}</h3>
        <p>{{ item.summary }}</p>
        <div class="status-row">
          <span class="status-pill" :class="item.status === 'Closed' ? 'status-closed' : 'status-open'">
            {{ item.status }}
          </span>
          <span v-if="item.winnerSide" class="votes">🏆 Side {{ item.winnerSide }}</span>
        </div>
        <div class="participants">
          <span>🔵 {{ item.sideA.userName }}</span>
          <span v-if="item.sideB">🔴 {{ item.sideB.userName }}</span>
          <span v-else class="status-pill status-open">Awaiting Side B</span>
        </div>
        <div class="card-actions">
          <RouterLink :to="`/cases/${item.id}`" class="action-btn" style="text-align:center;text-decoration:none;">
            View &amp; Vote
          </RouterLink>
        </div>
      </li>
    </ul>
  </section>
</template>


