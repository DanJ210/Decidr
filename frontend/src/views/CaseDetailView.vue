<script setup lang="ts">
import { useCaseDetail } from '../composables/useCaseDetail'

const {
  courtStore,
  sideBClaim,
  commentMessage,
  comments,
  commentsLoading,
  commentsSubmitting,
  commentsError,
  caseItem,
  totalVotes,
  isInvited,
  inviterName,
  votePermissionMessage,
  canComment,
  canVoteSideA,
  canVoteSideB,
  canCloseCase,
  closePermissionMessage,
  vote,
  closeCase,
  acceptInvitation,
  declineInvitation,
  submitComment,
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
              :disabled="!canVoteSideA || courtStore.mutating"
              @click="vote('A')"
            >
              Vote Side A
            </button>
            <button
              type="button"
              class="action-btn"
              :disabled="!canVoteSideB || courtStore.mutating"
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

          <p v-if="votePermissionMessage" class="status-text">{{ votePermissionMessage }}</p>
          <p v-if="closePermissionMessage" class="status-text">{{ closePermissionMessage }}</p>
        </section>
      </template>

      <section class="verdict">
        <h2>Case Comments</h2>
        <p class="status-text">One shared comment pool for everyone, regardless of which side they voted for.</p>

        <p v-if="commentsLoading" class="notice">Loading comments...</p>
        <p v-else-if="commentsError" class="notice error">{{ commentsError }}</p>
        <p v-else-if="comments.length === 0" class="status-text">Be the first to comment on this case.</p>
        <ul v-else>
          <li v-for="comment in comments" :key="comment.id">
            <strong>@{{ comment.userName }}</strong> · {{ new Date(comment.createdAtUtc).toLocaleString() }}
            <p>{{ comment.message }}</p>
          </li>
        </ul>

        <label>
          Add a Comment
          <textarea
            v-model="commentMessage"
            rows="3"
            placeholder="Share your thoughts on this case..."
            maxlength="1024"
            :disabled="!canComment || commentsSubmitting"
          />
        </label>
        <p v-if="!canComment" class="status-text">Select an active user to join the discussion.</p>
        <div class="action-bar">
          <button
            type="button"
            class="action-btn"
            :disabled="!canComment || !commentMessage.trim() || commentsSubmitting"
            @click="submitComment"
          >
            Post Comment
          </button>
        </div>
      </section>

      <RouterLink to="/" class="case-link">Back to Cases</RouterLink>
    </article>
  </section>
</template>
