<script setup lang="ts">
import { computed } from 'vue'
import { ArrowLeft, ChevronDown, ExternalLink, FileText, MessageCircle, Trophy } from '@lucide/vue'
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
  evidenceLoading,
  evidenceLoaded,
  evidenceError,
  evidenceNotice,
  evidenceDrafts,
  sideAEvidence,
  sideBEvidence,
  canAddEvidenceSideA,
  canAddEvidenceSideB,
  sideAEvidenceAtLimit,
  sideBEvidenceAtLimit,
  isEvidenceLinkSubmitting,
  isEvidenceFileSubmitting,
  setEvidenceFile,
  submitEvidenceLink,
  submitEvidenceFile,
  evidenceFileAccept,
  getEvidencePreviewUrl,
  openEvidenceFile,
  maxEvidenceItemsPerSide,
  vote,
  closeCase,
  acceptInvitation,
  declineInvitation,
  submitComment,
} = useCaseDetail()

const sideAPercentage = computed(() => {
  if (!caseItem.value || totalVotes.value === 0) return 50
  return Math.round((caseItem.value.verdict.votesForSideA / totalVotes.value) * 100)
})

function formatTimestamp(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(value))
}

function formatEvidenceSize(sizeBytes: number | null) {
  if (!sizeBytes) {
    return ''
  }
  if (sizeBytes >= 1024 * 1024) {
    return `${(sizeBytes / (1024 * 1024)).toFixed(2)} MB`
  }
  if (sizeBytes >= 1024) {
    return `${(sizeBytes / 1024).toFixed(1)} KB`
  }
  return `${sizeBytes} B`
}
</script>

<template>
  <section>
    <p v-if="courtStore.loading" class="notice">Loading case details...</p>
    <p v-else-if="courtStore.error" class="notice error">{{ courtStore.error }}</p>

    <article v-else-if="caseItem" class="detail-shell">
      <RouterLink to="/" class="detail-back-link"><ArrowLeft :size="17" /> Community cases</RouterLink>

      <header class="detail-header">
        <div class="detail-header-meta">
          <span class="pill">{{ caseItem.category }}</span>
          <span class="status-dot-label" :class="caseItem.status.toLowerCase()">
            <span aria-hidden="true"></span>{{ caseItem.status }}
          </span>
        </div>
        <h1>{{ caseItem.title }}</h1>
        <p class="detail-summary">{{ caseItem.summary }}</p>
        <div class="detail-participants">
          <span><i class="participant-dot side-a-dot"></i>@{{ caseItem.sideA.userName }}</span>
          <span v-if="caseItem.sideB"><i class="participant-dot side-b-dot"></i>@{{ caseItem.sideB.userName }}</span>
          <span v-else><i class="participant-dot pending-dot"></i>Awaiting Side B</span>
        </div>
      </header>

      <!-- Pending: invitation banner for the invited user -->
      <section v-if="caseItem.status === 'Pending'" class="detail-section invitation-detail">
        <div v-if="isInvited">
          <p class="eyebrow">Your response is needed</p>
          <h2>Take Side B</h2>
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
          <p class="eyebrow">Case pending</p>
          <h2>Awaiting a response</h2>
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
        <div class="argument-matchup" aria-label="Case arguments">
          <section class="argument-panel argument-side-a">
            <header>
              <span class="argument-side-label">Side A</span>
              <strong>@{{ caseItem.sideA.userName }}</strong>
            </header>
            <blockquote>{{ caseItem.sideA.claim }}</blockquote>
          </section>
          <section class="argument-panel argument-side-b">
            <header>
              <span class="argument-side-label">Side B</span>
              <strong>@{{ caseItem.sideB?.userName }}</strong>
            </header>
            <blockquote>{{ caseItem.sideB?.claim }}</blockquote>
          </section>
        </div>

        <section class="detail-section verdict-panel">
          <header class="section-heading">
            <span class="section-icon"><Trophy :size="19" /></span>
            <div>
              <p class="eyebrow">{{ totalVotes }} {{ totalVotes === 1 ? 'vote' : 'votes' }}</p>
              <h2>Community verdict</h2>
            </div>
            <span v-if="caseItem.winnerSide" class="winner-label">Side {{ caseItem.winnerSide }} won</span>
          </header>

          <div class="verdict-tallies">
            <span><i class="participant-dot side-a-dot"></i>Side A <strong>{{ caseItem.verdict.votesForSideA }}</strong></span>
            <span><strong>{{ caseItem.verdict.votesForSideB }}</strong> Side B<i class="participant-dot side-b-dot"></i></span>
          </div>
          <div
            class="vote-meter detail-vote-meter"
            :class="{ empty: totalVotes === 0 }"
            role="img"
            :aria-label="totalVotes ? `${sideAPercentage} percent Side A and ${100 - sideAPercentage} percent Side B` : 'No votes yet'"
          >
            <span class="vote-meter-a" :style="{ width: `${sideAPercentage}%` }"></span>
            <span class="vote-meter-b"></span>
          </div>
          <p v-if="totalVotes === 0" class="empty-verdict">No votes yet. The first vote sets the pace.</p>

          <div class="vote-action-bar desktop-vote-actions">
            <button
              type="button"
              class="vote-button vote-side-a"
              :disabled="!canVoteSideA || courtStore.mutating"
              @click="vote('A')"
            >
              <span>Vote</span><strong>Side A</strong>
            </button>
            <button
              type="button"
              class="vote-button vote-side-b"
              :disabled="!canVoteSideB || courtStore.mutating"
              @click="vote('B')"
            >
              <span>Vote</span><strong>Side B</strong>
            </button>
          </div>

          <p v-if="votePermissionMessage" class="status-text">{{ votePermissionMessage }}</p>
          <div v-if="canCloseCase" class="case-admin-actions">
            <button type="button" class="text-button danger-text" :disabled="courtStore.mutating" @click="closeCase">
              Close voting and declare a winner
            </button>
          </div>
          <p v-else-if="closePermissionMessage" class="status-text close-message">{{ closePermissionMessage }}</p>
        </section>

        <section class="detail-section evidence-section">
          <details class="section-disclosure">
            <summary>
              <span class="section-icon"><FileText :size="19" /></span>
              <span class="disclosure-title">
                <strong>Review evidence</strong>
                <small>{{ sideAEvidence.length + sideBEvidence.length }} supporting {{ sideAEvidence.length + sideBEvidence.length === 1 ? 'item' : 'items' }}</small>
              </span>
              <ChevronDown :size="19" class="disclosure-chevron" aria-hidden="true" />
            </summary>
            <div class="disclosure-content">
              <p class="status-text">Review each side's supporting materials before you cast a vote.</p>

              <p v-if="evidenceLoading" class="notice">Loading side evidence...</p>
              <p v-if="evidenceError" class="notice error" role="alert">{{ evidenceError }}</p>
              <p v-if="evidenceNotice" class="notice" aria-live="polite">{{ evidenceNotice }}</p>

              <div v-if="!evidenceLoading && evidenceLoaded" class="evidence-grid">
            <section class="evidence-column">
              <h3>
                Side A · {{ caseItem.sideA.userName }}
                <span class="pill evidence-count">{{ sideAEvidence.length }}</span>
              </h3>

              <p v-if="sideAEvidence.length === 0" class="status-text">No evidence has been added for Side A yet.</p>
              <ul v-else class="evidence-list">
                <li v-for="item in sideAEvidence" :key="item.id" class="evidence-item">
                  <div class="evidence-item-top">
                    <strong>{{ item.title }}</strong>
                    <span class="status-pill" :class="item.type === 'Link' ? 'status-open' : 'status-closed'">
                      {{ item.type }}
                    </span>
                  </div>
                  <p class="status-text">
                    Added by @{{ item.addedByUserName }} · {{ new Date(item.createdAtUtc).toLocaleString() }}
                    <span v-if="item.sizeBytes"> · {{ formatEvidenceSize(item.sizeBytes) }}</span>
                  </p>
                  <img
                    v-if="item.type === 'Image' && getEvidencePreviewUrl(item)"
                    :src="getEvidencePreviewUrl(item)"
                    :alt="item.title"
                    class="evidence-image"
                    loading="lazy"
                  />
                  <a
                    :href="item.resourceUrl"
                    target="_blank"
                    rel="noopener noreferrer"
                    class="case-link"
                    @click.prevent="openEvidenceFile(item)"
                  >
                    {{ item.type === 'Link' ? 'Open source link' : item.type === 'Image' ? 'Open image' : 'Open document' }}
                    <ExternalLink :size="14" aria-hidden="true" />
                  </a>
                </li>
              </ul>

              <p v-if="sideAEvidenceAtLimit" class="status-text">
                Side A reached the maximum of {{ maxEvidenceItemsPerSide }} evidence items.
              </p>
              <details v-if="canAddEvidenceSideA" class="evidence-add-panel">
                <summary>Add evidence to Side A</summary>
                <div class="evidence-add-fields">
                  <label>
                    Link Title
                    <input v-model="evidenceDrafts.A.linkTitle" placeholder="Source title" />
                  </label>
                  <label>
                    Link URL
                    <input v-model="evidenceDrafts.A.linkUrl" placeholder="https://example.com/source" />
                  </label>
                  <div class="action-bar">
                    <button
                      type="button"
                      class="action-btn"
                      :disabled="isEvidenceLinkSubmitting('A') || !evidenceDrafts.A.linkTitle.trim() || !evidenceDrafts.A.linkUrl.trim()"
                      @click="submitEvidenceLink('A')"
                    >
                      Add Link
                    </button>
                  </div>

                  <label>
                    File Title (optional)
                    <input v-model="evidenceDrafts.A.fileTitle" placeholder="Defaults to filename" />
                  </label>
                  <label>
                    Upload File
                    <input :accept="evidenceFileAccept" type="file" @change="setEvidenceFile('A', $event)" />
                  </label>
                  <p v-if="evidenceDrafts.A.file" class="selected-file">
                    Selected: <strong>{{ evidenceDrafts.A.file.name }}</strong>
                  </p>
                  <div class="action-bar">
                    <button
                      type="button"
                      class="action-btn"
                      :disabled="isEvidenceFileSubmitting('A') || !evidenceDrafts.A.file"
                      @click="submitEvidenceFile('A')"
                    >
                      Upload File
                    </button>
                  </div>
                </div>
              </details>
            </section>

            <section class="evidence-column">
              <h3>
                Side B · {{ caseItem.sideB?.userName ?? 'Pending Side B' }}
                <span class="pill evidence-count">{{ sideBEvidence.length }}</span>
              </h3>

              <p v-if="sideBEvidence.length === 0" class="status-text">No evidence has been added for Side B yet.</p>
              <ul v-else class="evidence-list">
                <li v-for="item in sideBEvidence" :key="item.id" class="evidence-item">
                  <div class="evidence-item-top">
                    <strong>{{ item.title }}</strong>
                    <span class="status-pill" :class="item.type === 'Link' ? 'status-open' : 'status-closed'">
                      {{ item.type }}
                    </span>
                  </div>
                  <p class="status-text">
                    Added by @{{ item.addedByUserName }} · {{ new Date(item.createdAtUtc).toLocaleString() }}
                    <span v-if="item.sizeBytes"> · {{ formatEvidenceSize(item.sizeBytes) }}</span>
                  </p>
                  <img
                    v-if="item.type === 'Image' && getEvidencePreviewUrl(item)"
                    :src="getEvidencePreviewUrl(item)"
                    :alt="item.title"
                    class="evidence-image"
                    loading="lazy"
                  />
                  <a
                    :href="item.resourceUrl"
                    target="_blank"
                    rel="noopener noreferrer"
                    class="case-link"
                    @click.prevent="openEvidenceFile(item)"
                  >
                    {{ item.type === 'Link' ? 'Open source link' : item.type === 'Image' ? 'Open image' : 'Open document' }}
                    <ExternalLink :size="14" aria-hidden="true" />
                  </a>
                </li>
              </ul>

              <p v-if="sideBEvidenceAtLimit" class="status-text">
                Side B reached the maximum of {{ maxEvidenceItemsPerSide }} evidence items.
              </p>
              <details v-if="canAddEvidenceSideB" class="evidence-add-panel">
                <summary>Add evidence to Side B</summary>
                <div class="evidence-add-fields">
                  <label>
                    Link Title
                    <input v-model="evidenceDrafts.B.linkTitle" placeholder="Source title" />
                  </label>
                  <label>
                    Link URL
                    <input v-model="evidenceDrafts.B.linkUrl" placeholder="https://example.com/source" />
                  </label>
                  <div class="action-bar">
                    <button
                      type="button"
                      class="action-btn"
                      :disabled="isEvidenceLinkSubmitting('B') || !evidenceDrafts.B.linkTitle.trim() || !evidenceDrafts.B.linkUrl.trim()"
                      @click="submitEvidenceLink('B')"
                    >
                      Add Link
                    </button>
                  </div>

                  <label>
                    File Title (optional)
                    <input v-model="evidenceDrafts.B.fileTitle" placeholder="Defaults to filename" />
                  </label>
                  <label>
                    Upload File
                    <input :accept="evidenceFileAccept" type="file" @change="setEvidenceFile('B', $event)" />
                  </label>
                  <p v-if="evidenceDrafts.B.file" class="selected-file">
                    Selected: <strong>{{ evidenceDrafts.B.file.name }}</strong>
                  </p>
                  <div class="action-bar">
                    <button
                      type="button"
                      class="action-btn"
                      :disabled="isEvidenceFileSubmitting('B') || !evidenceDrafts.B.file"
                      @click="submitEvidenceFile('B')"
                    >
                      Upload File
                    </button>
                  </div>
                </div>
              </details>
            </section>
              </div>
            </div>
          </details>
        </section>

      </template>

      <section class="detail-section comment-section">
        <header class="section-heading">
          <span class="section-icon neutral-icon"><MessageCircle :size="19" /></span>
          <div>
            <p class="eyebrow">{{ comments.length }} {{ comments.length === 1 ? 'reply' : 'replies' }}</p>
            <h2>Discussion</h2>
          </div>
        </header>

        <p v-if="commentsLoading" class="notice">Loading comments...</p>
        <p v-else-if="commentsError" class="notice error">{{ commentsError }}</p>
        <p v-else-if="comments.length === 0" class="status-text">Be the first to comment on this case.</p>
        <ul v-else class="comment-list">
          <li v-for="comment in comments" :key="comment.id" class="comment-item">
            <div class="comment-meta">
              <strong>@{{ comment.userName }}</strong>
              <time :datetime="comment.createdAtUtc">{{ formatTimestamp(comment.createdAtUtc) }}</time>
            </div>
            <p>{{ comment.message }}</p>
          </li>
        </ul>

        <form class="comment-composer" @submit.prevent="submitComment">
          <label for="case-comment">Add a comment</label>
          <textarea
            id="case-comment"
            v-model="commentMessage"
            rows="3"
            placeholder="Share your thoughts on this case..."
            maxlength="1024"
            :disabled="!canComment || commentsSubmitting"
          />
          <button
            type="submit"
            class="action-btn"
            :disabled="!canComment || !commentMessage.trim() || commentsSubmitting"
          >
            Post Comment
          </button>
          <p v-if="!canComment" class="status-text">Select an active user to join the discussion.</p>
        </form>
      </section>

      <div
        v-if="caseItem.status === 'Open' && (canVoteSideA || canVoteSideB)"
        class="mobile-vote-tray"
        aria-label="Vote on this case"
      >
        <button
          type="button"
          class="vote-button vote-side-a"
          :disabled="!canVoteSideA || courtStore.mutating"
          @click="vote('A')"
        >
          Vote <strong>A</strong>
        </button>
        <button
          type="button"
          class="vote-button vote-side-b"
          :disabled="!canVoteSideB || courtStore.mutating"
          @click="vote('B')"
        >
          Vote <strong>B</strong>
        </button>
      </div>
    </article>
  </section>
</template>
