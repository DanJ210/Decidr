import { defineStore } from 'pinia'
import { acceptCaseInvitation, blockUser, castVote, closeCase, createCase, declineCaseInvitation, fetchCaseById, fetchCaseFeed, reportCase, recordPlaybackEvent } from '../services/api'
import type { ArgumentCase, CaseSide, CreateCaseRequest } from '../types'

interface CourtState {
  cases: ArgumentCase[]
  selectedCase: ArgumentCase | null
  loading: boolean
  mutating: boolean
  error: string | null
  selectedCaseRequestId: number
  feedCursor: string | null
  feedHasMore: boolean
  feedLoading: boolean
}

interface CaseMutationResult {
  success: boolean
  updatedCase?: ArgumentCase
  error?: string
}

export const useCourtStore = defineStore('court', {
  state: (): CourtState => ({
    cases: [],
    selectedCase: null,
    loading: false,
    mutating: false,
    error: null,
    selectedCaseRequestId: 0,
    feedCursor: null,
    feedHasMore: true,
    feedLoading: false,
  }),
  actions: {
    async loadCases() {
      this.loading = true
      this.error = null

      try {
        const page = await fetchCaseFeed()
        this.cases = page.items
        this.feedCursor = page.nextCursor
        this.feedHasMore = page.hasMore
      } catch {
        this.error = 'Unable to load arguments right now. Please try again.'
      } finally {
        this.loading = false
      }
    },
    async loadMoreCases() {
      if (this.feedLoading || !this.feedHasMore) return
      this.feedLoading = true
      try {
        const page = await fetchCaseFeed(this.feedCursor)
        const existingIds = new Set(this.cases.map((item) => item.id))
        this.cases.push(...page.items.filter((item) => !existingIds.has(item.id)))
        this.feedCursor = page.nextCursor
        this.feedHasMore = page.hasMore
      } finally {
        this.feedLoading = false
      }
    },
    async reportCase(caseId: string, reason: string) {
      await reportCase(caseId, reason)
    },
    async blockUser(userId: string) {
      await blockUser(userId)
      this.cases = this.cases.filter((item) => item.sideA.userId !== userId && item.sideB?.userId !== userId)
    },
    async recordPlaybackEvent(caseId: string, event: { side: CaseSide; event: string; positionSeconds: number }) {
      try { await recordPlaybackEvent(caseId, event) } catch { /* analytics must not interrupt playback */ }
    },
    async loadCase(id: string, options?: { clearSelectedCaseOnFailure?: boolean }) {
      const requestId = this.selectedCaseRequestId + 1
      this.selectedCaseRequestId = requestId
      this.loading = true
      this.error = null
      const clearSelectedCaseOnFailure = options?.clearSelectedCaseOnFailure ?? true

      try {
        const loaded = await fetchCaseById(id)
        if (this.selectedCaseRequestId !== requestId) {
          return null
        }

        this.selectedCase = loaded
        return loaded
      } catch {
        if (this.selectedCaseRequestId !== requestId) {
          return null
        }

        if (clearSelectedCaseOnFailure) {
          this.error = 'Unable to load this case right now.'
          this.selectedCase = null
        }
        else
        {
          this.error = null
        }
        return null
      } finally {
        if (this.selectedCaseRequestId === requestId) {
          this.loading = false
        }
      }
    },
    async createCase(request: CreateCaseRequest) {
      this.mutating = true
      this.error = null

      try {
        const created = await createCase(request)
        this.cases = [created, ...this.cases]
        this.selectedCase = created
        return created
      } catch {
        this.error = 'Unable to create this case right now.'
        return null
      } finally {
        this.mutating = false
      }
    },
    async vote(caseId: string, side: CaseSide) {
      this.mutating = true
      this.error = null

      try {
        const updated = await castVote(caseId, { side })
        this.replaceCase(updated)
        return { success: true, updatedCase: updated } satisfies CaseMutationResult
      } catch {
        return {
          success: false,
          error: 'Vote could not be submitted. You may have already voted or the case is closed.',
        } satisfies CaseMutationResult
      } finally {
        this.mutating = false
      }
    },
    async closeCase(caseId: string) {
      this.mutating = true
      this.error = null

      try {
        const updated = await closeCase(caseId)
        this.replaceCase(updated)
        return { success: true, updatedCase: updated } satisfies CaseMutationResult
      } catch {
        return {
          success: false,
          error: 'Unable to close this case. Only participants or moderators can close it.',
        } satisfies CaseMutationResult
      } finally {
        this.mutating = false
      }
    },
    async acceptInvitation(
      caseId: string,
      claim: string,
      video?: { sideBRecordUrl?: string | null; sideBDurationSeconds?: number | null }
    ) {
      this.mutating = true
      this.error = null

      try {
        const updated = await acceptCaseInvitation(caseId, {
          claim,
          sideBRecordUrl: video?.sideBRecordUrl ?? null,
          sideBDurationSeconds: video?.sideBDurationSeconds ?? null,
        })
        this.replaceCase(updated)
        return { success: true, updatedCase: updated } satisfies CaseMutationResult
      } catch {
        return {
          success: false,
          error: 'Unable to accept the invitation right now.',
        } satisfies CaseMutationResult
      } finally {
        this.mutating = false
      }
    },
    async declineInvitation(caseId: string) {
      this.mutating = true
      this.error = null

      try {
        await declineCaseInvitation(caseId)
        this.cases = this.cases.filter((c) => c.id !== caseId)
        if (this.selectedCase?.id === caseId) {
          this.selectedCase = null
        }
        return true
      } catch {
        this.error = 'Unable to decline the invitation right now.'
        return false
      } finally {
        this.mutating = false
      }
    },
    replaceCase(updated: ArgumentCase) {
      this.cases = this.cases.map((existing) => (existing.id === updated.id ? updated : existing))
    },
  },
})
