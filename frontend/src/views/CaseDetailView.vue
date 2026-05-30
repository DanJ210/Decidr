<script setup lang="ts">
import { useCaseDetail } from '../composables/useCaseDetail'

const {
  courtStore,
  sideBClaim,
  caseItem,
  totalVotes,
  isInvited,
  inviterName,
  canVote,
  canCloseCase,
  closePermissionMessage,
  isParticipant,
  vote,
  closeCase,
  acceptInvitation,
  declineInvitation,
} = useCaseDetail()
</script>

<template>
  <section>
    <p v-if="courtStore.loading" class="notice">Loading case details...</p>
    <p v-else-if="courtStore.error" class="notice error">{{ courtStore.error }}</p>

    <article v-else-if="caseItem" class="detail-shell">
      <header>
        <p class="kicker">{{ caseItem.category }}</p>
        <h1>{{ caseItem.title }}</h1>
        <p>{{ caseItem.summary }}</p>
      </header>

      <!-- Pending: invitation banner for the invited user -->
      <section v-if="caseItem.status === 'Pending'" class="verdict">
        <div v-if="isInvited">
          <h2>You've Been Invited!</h2>
          <p>
            <strong>@{{ inviterName }}</strong> has invited you to argue the opposing side of this case.
            Write your response below to make this case go live.
          </p>

          <div class="arguments">
            <section>
              <h2>Side A · {{ caseItem.sideA.userName }}</h2>
              <p>{{ caseItem.sideA.claim }}</p>
            </section>
            <section>
              <h2>Your Side (Side B)</h2>
              <label>
                Your Claim
                <textarea v-model="sideBClaim" rows="4" placeholder="State your opposing argument…" required />
              </label>
            </section>
          </div>

          <div class="action-bar">
            <button
              type="button"
              class="action-btn"
              :disabled="!sideBClaim.trim() || courtStore.mutating"
              @click="acceptInvitation"
            >
              Accept &amp; Go Live
            </button>
            <button
              type="button"
              class="action-btn danger"
              :disabled="courtStore.mutating"
              @click="declineInvitation"
            >
              Decline Invitation
            </button>
          </div>
        </div>
        <div v-else>
          <h2>Awaiting Response</h2>
          <p>
            <strong>@{{ caseItem.sideA.userName }}</strong> has started this case and is waiting for
            the invited user to write their side before it goes live.
          </p>
          <div class="arguments">
            <section>
              <h2>Side A · {{ caseItem.sideA.userName }}</h2>
              <p>{{ caseItem.sideA.claim }}</p>
            </section>
            <section>
              <h2>Side B</h2>
              <p class="notice">Pending — waiting for the invited user to respond.</p>
            </section>
          </div>
        </div>
      </section>

      <!-- Open / Closed: full case view -->
      <template v-else>
        <div class="arguments">
          <section>
            <h2>Side A · {{ caseItem.sideA.userName }}</h2>
            <p>{{ caseItem.sideA.claim }}</p>
          </section>
          <section>
            <h2>Side B · {{ caseItem.sideB?.userName }}</h2>
            <p>{{ caseItem.sideB?.claim }}</p>
          </section>
        </div>

        <section class="verdict">
          <h2>Community Verdict</h2>
          <p>Total votes: {{ totalVotes }}</p>
          <ul>
            <li>Side A: {{ caseItem.verdict.votesForSideA }}</li>
            <li>Side B: {{ caseItem.verdict.votesForSideB }}</li>
          </ul>

          <p class="status-text">
            Status: <strong>{{ caseItem.status }}</strong>
            <span v-if="caseItem.winnerSide"> · Winner: Side {{ caseItem.winnerSide }}</span>
          </p>

          <div class="action-bar">
            <button
              type="button"
              class="action-btn"
              :disabled="!canVote || courtStore.mutating"
              @click="vote('A')"
            >
              Vote Side A
            </button>
            <button
              type="button"
              class="action-btn"
              :disabled="!canVote || courtStore.mutating"
              @click="vote('B')"
            >
              Vote Side B
            </button>
            <button
              type="button"
              class="action-btn danger"
              :disabled="!canCloseCase || courtStore.mutating"
              @click="closeCase"
            >
              Close Case
            </button>
          </div>

          <p v-if="caseItem.status === 'Open' && isParticipant" class="status-text">
            You are a participant in this case and cannot vote.
          </p>
          <p v-if="closePermissionMessage" class="status-text">{{ closePermissionMessage }}</p>
        </section>
      </template>

      <RouterLink to="/" class="case-link">Back to Cases</RouterLink>
    </article>
  </section>
</template>

