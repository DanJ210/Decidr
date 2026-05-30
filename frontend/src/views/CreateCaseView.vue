<script setup lang="ts">
import { useCreateCase } from '../composables/useCreateCase'

const { authStore, courtStore, form, inviteCandidates, submit } = useCreateCase()
</script>

<template>
  <section class="detail-shell">
    <p class="kicker">New Court Case</p>
    <h1>Start an Argument Duel</h1>

    <form class="case-form" @submit.prevent="submit">
      <label>
        Title
        <input v-model="form.title" required />
      </label>

      <label>
        Category
        <input v-model="form.category" required />
      </label>

      <label>
        Summary
        <textarea v-model="form.summary" required rows="3" />
      </label>

      <div class="arguments">
        <section>
          <h2>Your Side (Side A)</h2>
          <p class="notice">
            Playing as: <strong>{{ authStore.selectedUser?.displayName ?? '—' }}</strong>
          </p>
          <label>
            Your Claim
            <textarea v-model="form.sideAClaim" required rows="4" placeholder="State your argument…" />
          </label>
        </section>

        <section>
          <h2>Invite to Side B</h2>
          <p class="notice" role="status" aria-live="polite">Pick a connected friend to write the opposing side.</p>
          <label v-if="inviteCandidates.length">
            Invite User
            <select v-model="form.invitedUserId" required>
              <option value="" disabled>Choose a friend…</option>
              <option v-for="user in inviteCandidates" :key="user.id" :value="user.id">
                {{ user.displayName }} (@{{ user.userName }})
              </option>
            </select>
          </label>
          <p v-else class="notice">
            You need at least one friend connection before creating a case.
            <RouterLink to="/friends" class="case-link">Manage friends</RouterLink>
          </p>
          <p class="notice">
            They will receive an invitation to write their response before the case goes live.
            Add friends from the Friends page to invite more people.
          </p>
        </section>
      </div>

      <div class="action-bar">
        <button
          type="submit"
          class="action-btn"
          :disabled="courtStore.mutating || !form.invitedUserId"
          :aria-describedby="form.invitedUserId ? undefined : 'create-case-help'"
        >
          Send Invitation &amp; Create Case
        </button>
        <RouterLink to="/" class="case-link">Cancel</RouterLink>
      </div>
      <p v-if="!form.invitedUserId" id="create-case-help" class="notice">
        Select a connected friend before sending the invitation.
      </p>
    </form>

    <p v-if="courtStore.error" class="notice error">{{ courtStore.error }}</p>
  </section>
</template>
