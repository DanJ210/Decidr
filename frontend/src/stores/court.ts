import { defineStore } from 'pinia'
import { acceptCaseInvitation, castVote, closeCase, createCase, declineCaseInvitation, fetchCaseById, fetchCases } from '../services/api'
import type { ArgumentCase, CaseSide, CreateCaseRequest } from '../types'

interface CourtState {
  cases: ArgumentCase[]
  selectedCase: ArgumentCase | null
  loading: boolean
  mutating: boolean
  error: string | null
  selectedCaseRequestId: number
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
  }),
  actions: {
    async loadCases() {
      this.loading = true
      this.error = null

      try {
        this.cases = await fetchCases()
      } catch {
        this.error = 'Unable to load arguments right now. Please try again.'
      } finally {
        this.loading = false
      }
    },
    async loadCase(id: string, options?: { userId?: string; clearSelectedCaseOnFailure?: boolean }) {
      const requestId = this.selectedCaseRequestId + 1
      this.selectedCaseRequestId = requestId
      this.loading = true
      this.error = null
      const clearSelectedCaseOnFailure = options?.clearSelectedCaseOnFailure ?? true

      try {
        const loaded = await fetchCaseById(id, options?.userId)
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
    async acceptInvitation(caseId: string, claim: string) {
      this.mutating = true
      this.error = null

      try {
        const updated = await acceptCaseInvitation(caseId, { claim })
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
