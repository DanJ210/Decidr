<script setup lang="ts">
import { ArrowLeft, ArrowRight, Scale, Send, UserRound } from '@lucide/vue'
import { useCreateCase } from '../composables/useCreateCase'

const { authStore, courtStore, form, inviteCandidates, submit } = useCreateCase()
</script>

<template>
  <section class="detail-shell create-page">
    <RouterLink to="/" class="detail-back-link"><ArrowLeft :size="17" /> Community cases</RouterLink>

    <header class="secondary-page-header">
      <p class="eyebrow">New case</p>
      <h1>Put it up for debate.</h1>
      <p>Frame the disagreement, make your opening claim, and invite a friend to take the other side.</p>
    </header>

    <form class="case-form create-case-form" @submit.prevent="submit">
      <section class="composer-section">
        <header class="composer-section-heading">
          <span class="step-number">1</span>
          <div>
            <h2>Frame the case</h2>
            <p>Give the community enough context to understand the disagreement.</p>
          </div>
        </header>

        <div class="form-row">
          <label class="field-group field-grow">
            <span>Case title</span>
            <input v-model="form.title" required placeholder="What should the community decide?" />
          </label>

          <label class="field-group category-field">
            <span>Category</span>
            <input v-model="form.category" required placeholder="Relationships" />
          </label>
        </div>

        <label class="field-group">
          <span>Shared context</span>
          <textarea
            v-model="form.summary"
            required
            rows="4"
            placeholder="Describe what happened without arguing either side yet."
          />
        </label>
      </section>

      <section class="composer-section side-a-composer">
        <header class="composer-section-heading">
          <span class="step-number side-a-step"><Scale :size="17" /></span>
          <div>
            <p class="eyebrow">Step 2 · Side A</p>
            <h2>Make your opening claim</h2>
          </div>
        </header>

        <div class="composer-identity">
          <span class="identity-avatar">{{ authStore.selectedUser?.displayName?.charAt(0) ?? '?' }}</span>
          <span>
            <strong>{{ authStore.selectedUser?.displayName ?? 'No profile selected' }}</strong>
            <small>You are arguing Side A</small>
          </span>
        </div>

        <label class="field-group">
          <span>Your claim</span>
          <textarea
            v-model="form.sideAClaim"
            required
            rows="6"
            placeholder="State what you believe and the strongest reason why."
          />
        </label>
      </section>

      <section class="composer-section opponent-composer">
        <header class="composer-section-heading">
          <span class="step-number side-b-step"><UserRound :size="17" /></span>
          <div>
            <p class="eyebrow">Step 3 · Side B</p>
            <h2>Choose your opponent</h2>
          </div>
        </header>

        <label v-if="inviteCandidates.length" class="field-group">
          <span>Invite a friend</span>
          <select v-model="form.invitedUserId" required>
            <option value="" disabled>Choose a friend</option>
            <option v-for="user in inviteCandidates" :key="user.id" :value="user.id">
              {{ user.displayName }} (@{{ user.userName }})
            </option>
          </select>
          <small>They will write Side B before the case appears in the community feed.</small>
        </label>

        <div v-else class="empty-inline-state">
          <UserRound :size="20" aria-hidden="true" />
          <span>
            <strong>You need a friend to start a case.</strong>
            <RouterLink to="/friends" class="case-link">Find people <ArrowRight :size="14" /></RouterLink>
          </span>
        </div>
      </section>

      <footer class="composer-actions">
        <RouterLink to="/" class="text-link">Cancel</RouterLink>
        <button
          type="submit"
          class="action-btn create-submit-button"
          :disabled="courtStore.mutating || !form.invitedUserId"
          :aria-describedby="form.invitedUserId ? undefined : 'create-case-help'"
        >
          <Send :size="17" aria-hidden="true" />
          {{ courtStore.mutating ? 'Creating case...' : 'Send invitation' }}
        </button>
      </footer>
      <p v-if="!form.invitedUserId" id="create-case-help" class="visually-hidden">
        Select a connected friend before sending the invitation.
      </p>
    </form>

    <p v-if="courtStore.error" class="notice error">{{ courtStore.error }}</p>
  </section>
</template>
