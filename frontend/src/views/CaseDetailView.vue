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
  evidenceLoading,
  evidenceError,
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
  maxEvidenceItemsPerSide,
  vote,
  closeCase,
  acceptInvitation,
  declineInvitation,
  submitComment,
} = useCaseDetail()

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

        <section class="verdict evidence-section">
          <h2>Review Side Evidence</h2>
          <p class="status-text">Review each side's supporting materials before you cast a vote.</p>

          <p v-if="evidenceLoading" class="notice">Loading side evidence...</p>
          <p v-else-if="evidenceError" class="notice error">{{ evidenceError }}</p>

          <div v-else class="evidence-grid">
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
                    v-if="item.type === 'Image'"
                    :src="item.resourceUrl"
                    :alt="item.title"
                    class="evidence-image"
                    loading="lazy"
                  />
                  <a :href="item.resourceUrl" target="_blank" rel="noopener noreferrer" class="case-link">
                    {{ item.type === 'Link' ? 'Open source link' : item.type === 'Image' ? 'Open image' : 'Open document' }}
                  </a>
                </li>
              </ul>

              <p v-if="sideAEvidenceAtLimit" class="status-text">
                Side A reached the maximum of {{ maxEvidenceItemsPerSide }} evidence items.
              </p>
              <div v-if="canAddEvidenceSideA" class="evidence-add-panel">
                <h4>Add Evidence to Side A</h4>
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
                    v-if="item.type === 'Image'"
                    :src="item.resourceUrl"
                    :alt="item.title"
                    class="evidence-image"
                    loading="lazy"
                  />
                  <a :href="item.resourceUrl" target="_blank" rel="noopener noreferrer" class="case-link">
                    {{ item.type === 'Link' ? 'Open source link' : item.type === 'Image' ? 'Open image' : 'Open document' }}
                  </a>
                </li>
              </ul>

              <p v-if="sideBEvidenceAtLimit" class="status-text">
                Side B reached the maximum of {{ maxEvidenceItemsPerSide }} evidence items.
              </p>
              <div v-if="canAddEvidenceSideB" class="evidence-add-panel">
                <h4>Add Evidence to Side B</h4>
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
            </section>
          </div>
        </section>

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
