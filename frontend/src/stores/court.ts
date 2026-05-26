import { defineStore } from 'pinia'
import { acceptCaseInvitation, castVote, closeCase, createCase, declineCaseInvitation, fetchCaseById, fetchCases } from '../services/api'
import type { ArgumentCase, CaseSide, CreateCaseRequest } from '../types'

interface CourtState {
  cases: ArgumentCase[]
  selectedCase: ArgumentCase | null
  loading: boolean
  mutating: boolean
  error: string | null
}

export const useCourtStore = defineStore('court', {
  state: (): CourtState => ({
    cases: [],
    selectedCase: null,
    loading: false,
    mutating: false,
    error: null,
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
    async loadCase(id: string) {
      this.loading = true
      this.error = null

      try {
        this.selectedCase = await fetchCaseById(id)
      } catch {
        this.error = 'Unable to load this case right now.'
        this.selectedCase = null
      } finally {
        this.loading = false
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
    async vote(caseId: string, userId: string, side: CaseSide) {
      this.mutating = true
      this.error = null

      try {
        const updated = await castVote(caseId, { userId, side })
        this.replaceCase(updated)
        this.selectedCase = updated
        return true
      } catch {
        this.error = 'Vote could not be submitted. You may have already voted or the case is closed.'
        return false
      } finally {
        this.mutating = false
      }
    },
    async closeCase(caseId: string, actorUserId: string) {
      this.mutating = true
      this.error = null

      try {
        const updated = await closeCase(caseId, { actorUserId })
        this.replaceCase(updated)
        this.selectedCase = updated
        return true
      } catch {
        this.error = 'Unable to close this case. Only participants or moderators can close it.'
        return false
      } finally {
        this.mutating = false
      }
    },
    async acceptInvitation(caseId: string, userId: string, claim: string) {
      this.mutating = true
      this.error = null

      try {
        const updated = await acceptCaseInvitation(caseId, { userId, claim })
        this.replaceCase(updated)
        this.selectedCase = updated
        return true
      } catch {
        this.error = 'Unable to accept the invitation right now.'
        return false
      } finally {
        this.mutating = false
      }
    },
    async declineInvitation(caseId: string, userId: string) {
      this.mutating = true
      this.error = null

      try {
        await declineCaseInvitation(caseId, userId)
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
