<script setup lang="ts">
import { useHottestCases } from '../composables/useHottestCases'

const { courtStore, friendsStore, hottestCases } = useHottestCases()
</script>

<template>
  <section class="hero">
    <p class="kicker">Community Court</p>
    <h1>Two sides. One verdict. Community decides.</h1>
    <p>
      Post a dispute, defend your side, and let the crowd deliver judgment.
      This scaffold includes starter routing, state, and API integration.
    </p>
    <RouterLink to="/cases/new" class="hero-cta">Create a New Case</RouterLink>
  </section>

  <!-- Pending invitations for the current user -->
  <section v-if="friendsStore.invitations.length" class="board">
    <header class="board-header">
      <h2>My Invitations</h2>
      <span>{{ friendsStore.invitations.length }} pending</span>
    </header>
    <ul class="case-grid">
      <li v-for="inv in friendsStore.invitations" :key="inv.id" class="case-card">
        <div class="top-row">
          <span class="pill">{{ inv.category }}</span>
          <span class="status-pill status-open">Awaiting Your Response</span>
        </div>
        <h3>{{ inv.title }}</h3>
        <p>{{ inv.summary }}</p>
        <p class="split">
          <span>Side A: {{ inv.sideA.userName }}</span>
          <span>Side B: You</span>
        </p>
        <RouterLink :to="`/cases/${inv.id}`" class="case-link">Respond to Invitation</RouterLink>
      </li>
    </ul>
  </section>

  <section class="board">
    <header class="board-header">
      <h2>Active Cases</h2>
      <span>{{ hottestCases.length }} case(s)</span>
    </header>

    <p v-if="courtStore.loading" class="notice">Loading live arguments...</p>
    <p v-else-if="courtStore.error" class="notice error">{{ courtStore.error }}</p>

    <ul v-else class="case-grid">
      <li v-for="item in hottestCases" :key="item.id" class="case-card">
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
          <span v-if="item.winnerSide">Winner: Side {{ item.winnerSide }}</span>
        </div>
        <div class="split">
          <span>Side A: {{ item.sideA.userName }}</span>
          <span>Side B: {{ item.sideB?.userName }}</span>
        </div>
        <RouterLink :to="`/cases/${item.id}`" class="case-link">View Case</RouterLink>
      </li>
    </ul>
  </section>
</template>

