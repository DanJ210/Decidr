<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { LockKeyhole, Scale, Trophy } from '@lucide/vue'
import { fetchPlayerRecords } from '../services/api'
import { useAuthStore } from '../stores/auth'
import type { PlayerRecord } from '../types'

const authStore = useAuthStore()
const records = ref<PlayerRecord[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const selectedUserId = ref<string | null>(null)

const ranked = computed(() => records.value.filter((record) => record.isQualified))
const provisional = computed(() => records.value.filter((record) => !record.isQualified))
const selectedRecord = computed(() =>
  records.value.find((record) => record.userId === selectedUserId.value) ?? null
)

function markerPosition(index: number) {
  if (ranked.value.length <= 1) return 50
  return 8 + (index / (ranked.value.length - 1)) * 84
}

function formatWinRate(record: PlayerRecord) {
  return `${Math.round(record.winRate * 100)}%`
}

onMounted(async () => {
  try {
    records.value = await fetchPlayerRecords()
    selectedUserId.value = authStore.selectedUserId ?? ranked.value[0]?.userId ?? null
  } catch {
    error.value = 'Unable to load court standings right now.'
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <section class="standings-page" aria-labelledby="standings-heading">
    <header class="secondary-page-header standings-header">
      <p class="eyebrow"><Trophy :size="14" aria-hidden="true" /> Court standing</p>
      <h1 id="standings-heading">From top verdict to hard time.</h1>
      <p>
        Placement is based on win rate after at least three completed cases. Ties count as
        completed cases, never as losses.
      </p>
    </header>

    <p v-if="loading" class="notice">Loading court records...</p>
    <p v-else-if="error" class="notice error" role="alert">{{ error }}</p>

    <template v-else>
      <section v-if="ranked.length" class="standing-spectrum" aria-labelledby="spectrum-heading">
        <h2 id="spectrum-heading" class="visually-hidden">Qualified player standing spectrum</h2>
        <div class="spectrum-canvas" :style="{ minWidth: `${Math.max(42, ranked.length * 6)}rem` }">
          <div class="spectrum-end spectrum-champion" aria-hidden="true">
            <Scale :size="25" />
            <span>Top verdict</span>
          </div>
          <div class="spectrum-end spectrum-jail" aria-hidden="true">
            <LockKeyhole :size="25" />
            <span>Holding cell</span>
          </div>
          <div class="spectrum-track" aria-hidden="true"></div>
          <button
            v-for="(record, index) in ranked"
            :key="record.userId"
            type="button"
            class="standing-marker"
            :class="{
              current: record.userId === authStore.selectedUserId,
              selected: record.userId === selectedUserId,
              alternate: index % 2 === 1,
            }"
            :style="{ left: `${markerPosition(index)}%` }"
            :aria-label="`${record.displayName}, rank ${record.rank}, ${record.wins} wins, ${record.losses} losses, ${record.ties} ties`"
            @click="selectedUserId = record.userId"
          >
            <span class="marker-rank">#{{ record.rank }}</span>
            <span class="marker-avatar">{{ record.displayName.charAt(0).toUpperCase() }}</span>
            <span class="marker-name">{{ record.displayName }}</span>
          </button>
        </div>
      </section>

      <p v-else class="notice">
        No one has completed three cases yet. The first qualified standings will appear here.
      </p>

      <aside v-if="selectedRecord" class="selected-standing" aria-live="polite">
        <span class="marker-avatar">{{ selectedRecord.displayName.charAt(0).toUpperCase() }}</span>
        <div>
          <p class="eyebrow">{{ selectedRecord.isQualified ? `Rank #${selectedRecord.rank}` : 'Provisional' }}</p>
          <h2>{{ selectedRecord.displayName }}</h2>
          <p>
            {{ selectedRecord.wins }} won · {{ selectedRecord.losses }} lost ·
            {{ selectedRecord.ties }} tied · {{ formatWinRate(selectedRecord) }} win rate
          </p>
        </div>
      </aside>

      <section class="standings-list-section" aria-labelledby="ranked-list-heading">
        <h2 id="ranked-list-heading">Ranked records</h2>
        <div class="standings-table-wrap">
          <table class="standings-table">
            <thead>
              <tr>
                <th scope="col">Rank</th>
                <th scope="col">Player</th>
                <th scope="col">Won</th>
                <th scope="col">Lost</th>
                <th scope="col">Tied</th>
                <th scope="col">Win rate</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="record in ranked"
                :key="record.userId"
                :class="{ 'current-user-row': record.userId === authStore.selectedUserId }"
              >
                <td>#{{ record.rank }}</td>
                <th scope="row">{{ record.displayName }} <small>@{{ record.userName }}</small></th>
                <td>{{ record.wins }}</td>
                <td>{{ record.losses }}</td>
                <td>{{ record.ties }}</td>
                <td>{{ formatWinRate(record) }}</td>
              </tr>
              <tr v-if="!ranked.length">
                <td colspan="6">No qualified records yet.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section class="provisional-section" aria-labelledby="provisional-heading">
        <h2 id="provisional-heading">Provisional players</h2>
        <p>Complete three cases to join the spectrum.</p>
        <ul class="provisional-list">
          <li v-for="record in provisional" :key="record.userId">
            <strong>{{ record.displayName }}</strong>
            <span>
              {{ record.wins }}W–{{ record.losses }}L–{{ record.ties }}T ·
              {{ record.completedCases }}/3 complete
            </span>
          </li>
        </ul>
      </section>
    </template>
  </section>
</template>
