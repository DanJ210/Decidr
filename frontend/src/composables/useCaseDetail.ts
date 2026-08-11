import axios from 'axios'
import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  deleteCaseEvidence,
  fetchCaseComments,
  fetchCaseEvidence,
  fetchCaseEvidenceFile,
  fetchCaseEvidenceStatus,
  fetchCaseVoteStatus,
  fetchPlayerRecord,
  postCaseComment,
  postCaseEvidenceLink,
  uploadCaseEvidenceFile,
} from '../services/api'
import { useAuthStore } from '../stores/auth'
import { useCourtStore } from '../stores/court'
import type {
  CaseComment,
  CaseEvidenceCollection,
  CaseEvidenceItem,
  CaseSide,
  EvidenceContentStatus,
  PlayerRecord
} from '../types'

const MAX_EVIDENCE_ITEMS_PER_SIDE = 20
const EVIDENCE_FILE_ACCEPT = '.jpg,.jpeg,.png,.webp,.gif,.pdf,.txt,.doc,.docx'
const EVIDENCE_STATUS_POLL_INTERVAL_MS = 3_000
const EVIDENCE_PREVIEW_MAX_ATTEMPTS = 3

interface SideEvidenceDraft {
  linkTitle: string
  linkUrl: string
  fileTitle: string
  file: File | null
}

export function useCaseDetail() {
  const route = useRoute()
  const router = useRouter()
  const courtStore = useCourtStore()
  const authStore = useAuthStore()

  const sideBClaim = ref('')
  const commentMessage = ref('')
  const comments = ref<CaseComment[]>([])
  const commentsLoading = ref(false)
  const commentsSubmitting = ref(false)
  const commentsError = ref<string | null>(null)
  const hasVoted = ref(false)
  const checkingVoteStatus = ref(false)
  const evidence = ref<CaseEvidenceCollection>({ sideA: [], sideB: [] })
  const evidenceLoading = ref(false)
  const evidenceLoaded = ref(false)
  const evidenceMutating = ref(false)
  const evidenceMutatingSide = ref<CaseSide | null>(null)
  const evidenceMutatingType = ref<'link' | 'file' | null>(null)
  const evidenceError = ref<string | null>(null)
  const evidenceNotice = ref<string | null>(null)
  const evidencePreviewUrls = reactive<Record<string, string>>({})
  const evidenceStatuses = reactive<Record<string, EvidenceContentStatus>>({})
  const evidenceViewer = ref<{ item: CaseEvidenceItem; url: string } | null>(null)
  const evidenceViewerLoadingId = ref<string | null>(null)
  const evidenceRemovingId = ref<string | null>(null)
  const sideARecord = ref<PlayerRecord | null>(null)
  const sideBRecord = ref<PlayerRecord | null>(null)
  const evidenceDrafts = reactive<Record<CaseSide, SideEvidenceDraft>>({
    A: {
      linkTitle: '',
      linkUrl: '',
      fileTitle: '',
      file: null,
    },
    B: {
      linkTitle: '',
      linkUrl: '',
      fileTitle: '',
      file: null,
    },
  })
  let caseStateRequestId = 0
  let voteStatusRequestId = 0
  let evidenceRequestId = 0
  const evidenceStatusTimers = new Map<string, number>()
  const evidencePreviewAttempts = new Map<string, number>()

  const caseItem = computed(() => courtStore.selectedCase)
  const activeUser = computed(() => authStore.selectedUser)

  function isViewingCase(caseId: string) {
    return route.params.id === caseId
  }

  function isCurrentCaseStateRequest(requestId: number, caseId: string) {
    return requestId === caseStateRequestId && isViewingCase(caseId)
  }

  function isCurrentVoteStatusRequest(requestId: number, caseId: string, userId: string) {
    return requestId === voteStatusRequestId && caseItem.value?.id === caseId && activeUser.value?.id === userId
  }

  function isCurrentEvidenceRequest(requestId: number, caseId: string) {
    return requestId === evidenceRequestId && caseItem.value?.id === caseId && isViewingCase(caseId)
  }

  function resetEvidenceDraft(side: CaseSide) {
    evidenceDrafts[side].linkTitle = ''
    evidenceDrafts[side].linkUrl = ''
    evidenceDrafts[side].fileTitle = ''
    evidenceDrafts[side].file = null
  }

  function resetAllEvidenceDrafts() {
    resetEvidenceDraft('A')
    resetEvidenceDraft('B')
  }

  function getEvidenceBySide(side: CaseSide) {
    return side === 'A' ? evidence.value.sideA : evidence.value.sideB
  }

  function appendEvidenceItem(item: CaseEvidenceItem) {
    if (item.side === 'A') {
      evidence.value = {
        ...evidence.value,
        sideA: [item, ...evidence.value.sideA],
      }
      return
    }

    evidence.value = {
      ...evidence.value,
      sideB: [item, ...evidence.value.sideB],
    }
  }

  function hasEvidenceItem(evidenceId: string) {
    return evidence.value.sideA.some(item => item.id === evidenceId)
      || evidence.value.sideB.some(item => item.id === evidenceId)
  }

  function clearEvidencePreviewUrls() {
    for (const objectUrl of Object.values(evidencePreviewUrls)) {
      URL.revokeObjectURL(objectUrl)
    }
    for (const evidenceId of Object.keys(evidencePreviewUrls)) {
      delete evidencePreviewUrls[evidenceId]
    }
    evidencePreviewAttempts.clear()
  }

  function clearEvidenceStatusTimers() {
    for (const timer of evidenceStatusTimers.values()) {
      window.clearTimeout(timer)
    }
    evidenceStatusTimers.clear()
  }

  function clearEvidenceStatuses() {
    clearEvidenceStatusTimers()
    for (const evidenceId of Object.keys(evidenceStatuses)) {
      delete evidenceStatuses[evidenceId]
    }
  }

  async function loadEvidencePreview(item: CaseEvidenceItem, requestId: number) {
    if (item.type !== 'Image') return false

    try {
      const content = await fetchCaseEvidenceFile(item.caseId, item.id)
      const objectUrl = URL.createObjectURL(content)
      if (requestId !== evidenceRequestId || !isViewingCase(item.caseId) || !hasEvidenceItem(item.id)) {
        URL.revokeObjectURL(objectUrl)
        return true
      }
      evidencePreviewUrls[item.id] = objectUrl
      evidencePreviewAttempts.delete(item.id)
      return true
    } catch {
      const attempts = evidencePreviewAttempts.get(item.id) ?? 0
      evidencePreviewAttempts.set(item.id, attempts + 1)
      return false
    }
  }

  function getEvidencePreviewUrl(item: CaseEvidenceItem) {
    return evidencePreviewUrls[item.id] ?? ''
  }

  function getEvidenceStatus(item: CaseEvidenceItem): EvidenceContentStatus {
    return item.type === 'Link' ? 'Clean' : (evidenceStatuses[item.id] ?? 'PendingScan')
  }

  function getEvidenceStatusLabel(item: CaseEvidenceItem) {
    const status = getEvidenceStatus(item)
    if (status === 'Clean') return 'Ready'
    if (status === 'PendingScan') return 'Security review'
    if (status === 'Malicious') return 'Blocked'
    if (status === 'ScanFailed') return 'Review failed'
    return 'Unavailable'
  }

  function isEvidenceReady(item: CaseEvidenceItem) {
    return getEvidenceStatus(item) === 'Clean'
  }

  function scheduleEvidenceStatusRefresh(item: CaseEvidenceItem, requestId: number) {
    const currentTimer = evidenceStatusTimers.get(item.id)
    if (currentTimer !== undefined) {
      window.clearTimeout(currentTimer)
    }
    const timer = window.setTimeout(() => {
      evidenceStatusTimers.delete(item.id)
      void refreshEvidenceStatus(item, requestId)
    }, EVIDENCE_STATUS_POLL_INTERVAL_MS)
    evidenceStatusTimers.set(item.id, timer)
  }

  async function refreshEvidenceStatus(item: CaseEvidenceItem, requestId: number) {
    if (item.type === 'Link') return

    try {
      const response = await fetchCaseEvidenceStatus(item.caseId, item.id)
      if (requestId !== evidenceRequestId || !isViewingCase(item.caseId) || !hasEvidenceItem(item.id)) return

      evidenceStatuses[item.id] = response.status
      if (response.status === 'PendingScan') {
        scheduleEvidenceStatusRefresh(item, requestId)
        return
      }

      evidenceStatusTimers.delete(item.id)
      if (response.status === 'Clean' && item.type === 'Image' && !evidencePreviewUrls[item.id]) {
        const loaded = await loadEvidencePreview(item, requestId)
        if (
          !loaded
          && hasEvidenceItem(item.id)
          && (evidencePreviewAttempts.get(item.id) ?? 0) < EVIDENCE_PREVIEW_MAX_ATTEMPTS
        ) {
          scheduleEvidenceStatusRefresh(item, requestId)
        }
      }
    } catch (error) {
      if (requestId !== evidenceRequestId || !isViewingCase(item.caseId) || !hasEvidenceItem(item.id)) return

      const status = axios.isAxiosError(error) ? error.response?.status : undefined
      if (status === 404) {
        evidenceStatuses[item.id] = 'NotFound'
        evidenceStatusTimers.delete(item.id)
        return
      }

      const isNonRetryableClientError = status !== undefined
        && status >= 400
        && status < 500
        && status !== 408
        && status !== 429
      if (isNonRetryableClientError) {
        evidenceStatuses[item.id] = 'ScanFailed'
        evidenceStatusTimers.delete(item.id)
        return
      }

      scheduleEvidenceStatusRefresh(item, requestId)
    }
  }

  function closeEvidenceViewer() {
    if (evidenceViewer.value) {
      URL.revokeObjectURL(evidenceViewer.value.url)
      evidenceViewer.value = null
    }
  }

  function downloadEvidenceFile() {
    const viewer = evidenceViewer.value
    if (!viewer) return

    const anchor = document.createElement('a')
    anchor.href = viewer.url
    anchor.download = viewer.item.title
    anchor.click()
  }

  async function openEvidenceFile(item: CaseEvidenceItem) {
    evidenceError.value = null
    evidenceNotice.value = null
    if (!isEvidenceReady(item)) {
      evidenceError.value = 'This evidence file is not ready yet. Its status will update automatically.'
      return
    }

    evidenceViewerLoadingId.value = item.id
    try {
      const content = await fetchCaseEvidenceFile(item.caseId, item.id)
      closeEvidenceViewer()
      const objectUrl = URL.createObjectURL(content)
      if (!isSelectedCaseContext(item.caseId) || !hasEvidenceItem(item.id)) {
        URL.revokeObjectURL(objectUrl)
        return
      }
      evidenceViewer.value = { item, url: objectUrl }
    } catch (error) {
      const status = axios.isAxiosError(error) ? error.response?.status : undefined
      evidenceError.value = status === 423
        ? 'This evidence file is still being scanned. Try again shortly.'
        : status === 410
          ? 'This evidence file is unavailable because it failed security scanning.'
          : status === 503
            ? 'Security scanning could not be completed. Try again later.'
            : 'Unable to open this evidence file right now.'
    } finally {
      evidenceViewerLoadingId.value = null
    }
  }

  function canRemoveEvidence(item: CaseEvidenceItem) {
    return caseItem.value?.status === 'Open' && activeUser.value?.id === item.addedByUserId
  }

  async function removeEvidence(item: CaseEvidenceItem) {
    if (!canRemoveEvidence(item) || evidenceRemovingId.value) return
    if (!window.confirm(`Remove “${item.title}” from this case?`)) return

    evidenceRemovingId.value = item.id
    evidenceError.value = null
    evidenceNotice.value = null
    try {
      await deleteCaseEvidence(item.caseId, item.id)
      if (!isSelectedCaseContext(item.caseId)) return

      const timer = evidenceStatusTimers.get(item.id)
      if (timer !== undefined) {
        window.clearTimeout(timer)
        evidenceStatusTimers.delete(item.id)
      }
      const previewUrl = evidencePreviewUrls[item.id]
      if (previewUrl) {
        URL.revokeObjectURL(previewUrl)
        delete evidencePreviewUrls[item.id]
      }
      evidencePreviewAttempts.delete(item.id)
      delete evidenceStatuses[item.id]
      if (evidenceViewer.value?.item.id === item.id) {
        closeEvidenceViewer()
      }

      evidence.value = item.side === 'A'
        ? { ...evidence.value, sideA: evidence.value.sideA.filter(candidate => candidate.id !== item.id) }
        : { ...evidence.value, sideB: evidence.value.sideB.filter(candidate => candidate.id !== item.id) }
      evidenceNotice.value = 'Evidence removed.'
    } catch (error) {
      if (isSelectedCaseContext(item.caseId)) {
        const status = axios.isAxiosError(error) ? error.response?.status : undefined
        evidenceError.value = status === 403
          ? 'Only the owner of this evidence can remove it.'
          : 'Unable to remove this evidence right now.'
      }
    } finally {
      evidenceRemovingId.value = null
    }
  }

  function isSelectedCaseContext(caseId: string) {
    return isViewingCase(caseId) && caseItem.value?.id === caseId
  }

  async function loadComments(caseId: string) {
    commentsLoading.value = true
    commentsError.value = null

    try {
      comments.value = await fetchCaseComments(caseId)
    } catch {
      commentsError.value = 'Unable to load comments right now.'
      comments.value = []
    } finally {
      commentsLoading.value = false
    }
  }

  async function loadEvidence(caseId: string) {
    const requestId = ++evidenceRequestId
    evidenceLoading.value = true
    evidenceLoaded.value = false
    evidenceError.value = null
    evidenceNotice.value = null

    try {
      const loaded = await fetchCaseEvidence(caseId)
      if (isCurrentEvidenceRequest(requestId, caseId)) {
        clearEvidencePreviewUrls()
        clearEvidenceStatuses()
        evidence.value = loaded
        evidenceLoaded.value = true
        for (const item of [...loaded.sideA, ...loaded.sideB]) {
          if (item.type !== 'Link') {
            evidenceStatuses[item.id] = 'PendingScan'
            void refreshEvidenceStatus(item, requestId)
          }
        }
      }
    } catch {
      if (isCurrentEvidenceRequest(requestId, caseId)) {
        evidence.value = { sideA: [], sideB: [] }
        evidenceError.value = 'Unable to load side evidence right now.'
      }
    } finally {
      if (requestId === evidenceRequestId) {
        evidenceLoading.value = false
      }
    }
  }

  async function refreshVoteStatus() {
    const requestId = ++voteStatusRequestId
    const selected = caseItem.value
    const user = activeUser.value

    if (!selected || !user || selected.status !== 'Open') {
      checkingVoteStatus.value = false
      return
    }

    if (selected.sideA.userId === user.id || selected.sideB?.userId === user.id) {
      checkingVoteStatus.value = false
      return
    }

    checkingVoteStatus.value = true

    try {
      const status = await fetchCaseVoteStatus(selected.id)
      if (isCurrentVoteStatusRequest(requestId, selected.id, user.id)) {
        hasVoted.value = status.hasVoted
      }
    } catch {
    } finally {
      if (requestId === voteStatusRequestId) {
        checkingVoteStatus.value = false
      }
    }
  }

  async function loadCaseState(id: string, preserveCurrentCaseOnFailure = false) {
    const requestId = ++caseStateRequestId
    const loaded = await courtStore.loadCase(id, {
      clearSelectedCaseOnFailure: !preserveCurrentCaseOnFailure,
    })

    if (!isCurrentCaseStateRequest(requestId, id)) {
      return
    }

    if (!loaded || loaded.id !== id) {
      if (!preserveCurrentCaseOnFailure) {
        hasVoted.value = false
        comments.value = []
        evidence.value = { sideA: [], sideB: [] }
        evidenceLoaded.value = false
        evidenceError.value = null
        resetAllEvidenceDrafts()
      }
      checkingVoteStatus.value = false
      commentsLoading.value = false
      evidenceLoading.value = false
      sideARecord.value = null
      sideBRecord.value = null
      return
    }

    const recordRequest = loaded.status === 'Closed' && loaded.sideB
      ? Promise.all([
          fetchPlayerRecord(loaded.sideA.userId),
          fetchPlayerRecord(loaded.sideB.userId),
        ]).then(([sideA, sideB]) => {
          if (isCurrentCaseStateRequest(requestId, id)) {
            sideARecord.value = sideA
            sideBRecord.value = sideB
          }
        }).catch(() => {
          if (isCurrentCaseStateRequest(requestId, id)) {
            sideARecord.value = null
            sideBRecord.value = null
          }
        })
      : Promise.resolve()

    await Promise.all([
      refreshVoteStatus(),
      loadComments(id),
      loadEvidence(id),
      recordRequest,
    ])
  }

  watch(
    () => route.params.id,
    (id) => {
      hasVoted.value = false
      checkingVoteStatus.value = false
      commentsError.value = null
      comments.value = []
      evidenceError.value = null
      evidence.value = { sideA: [], sideB: [] }
      evidenceLoaded.value = false
      sideARecord.value = null
      sideBRecord.value = null
      resetAllEvidenceDrafts()
      if (typeof id === 'string') {
        void loadCaseState(id)
      }
    },
    { immediate: true }
  )

  watch(
    () => authStore.selectedUser?.id,
    () => {
      hasVoted.value = false
      checkingVoteStatus.value = false
      evidenceError.value = null
      const id = route.params.id
      if (typeof id === 'string') {
        void loadCaseState(id, true)
      }
    }
  )

  const totalVotes = computed(() => {
    const selected = courtStore.selectedCase
    if (!selected) return 0
    return selected.verdict.votesForSideA + selected.verdict.votesForSideB
  })

  const isInvited = computed(() => {
    const selected = caseItem.value
    const user = activeUser.value
    return (
      selected?.status === 'Pending' &&
      !!user &&
      selected.invitedUserId === user.id
    )
  })

  const inviterName = computed(() => {
    const selected = caseItem.value
    if (!selected) return ''
    return selected.sideA.userName
  })

  const isParticipant = computed(() => {
    const selected = caseItem.value
    const user = activeUser.value
    if (!selected || !user) return false
    return selected.sideA.userId === user.id || selected.sideB?.userId === user.id
  })

  const canVote = computed(() => {
    const selected = caseItem.value
    const user = activeUser.value
    return !!selected && !!user && selected.status === 'Open' && !isParticipant.value && !hasVoted.value && !checkingVoteStatus.value
  })

  const canVoteSideA = computed(() => canVote.value)
  const canVoteSideB = computed(() => canVote.value)

  const votePermissionMessage = computed(() => {
    const selected = caseItem.value
    const user = activeUser.value
    if (!selected || selected.status !== 'Open') return ''
    if (!user) return 'Select an active user to vote on this case.'
    if (isParticipant.value) return 'You are a participant in this case and cannot vote.'
    if (hasVoted.value) return 'You have already voted on this case.'
    return ''
  })

  const canComment = computed(() => !!caseItem.value && !!activeUser.value)

  const canCloseCase = computed(() => {
    const selected = caseItem.value
    const user = activeUser.value
    if (!selected || !user || selected.status !== 'Open') return false
    const isModerator = user.role === 'Moderator'
    return isParticipant.value || isModerator
  })

  const closePermissionMessage = computed(() => {
    const selected = caseItem.value
    const user = activeUser.value
    if (!selected || selected.status !== 'Open') return ''
    if (!user) return 'Select an active user to interact with this case.'
    if (canCloseCase.value) {
      return user.role === 'Moderator'
        ? 'You can close this case as a moderator.'
        : 'You can close this case because you are one of the participants.'
    }
    return 'Only case participants or moderators can close this case.'
  })

  function canAddEvidenceToSide(side: CaseSide) {
    const selected = caseItem.value
    const user = activeUser.value
    if (!selected || !user || selected.status !== 'Open') {
      return false
    }

    const sideOwnerUserId = side === 'A' ? selected.sideA.userId : selected.sideB?.userId
    if (!sideOwnerUserId || sideOwnerUserId !== user.id) {
      return false
    }

    return getEvidenceBySide(side).length < MAX_EVIDENCE_ITEMS_PER_SIDE
  }

  const sideAEvidence = computed(() => evidence.value.sideA)
  const sideBEvidence = computed(() => evidence.value.sideB)
  const canAddEvidenceSideA = computed(() => canAddEvidenceToSide('A'))
  const canAddEvidenceSideB = computed(() => canAddEvidenceToSide('B'))
  const sideAEvidenceAtLimit = computed(() => sideAEvidence.value.length >= MAX_EVIDENCE_ITEMS_PER_SIDE)
  const sideBEvidenceAtLimit = computed(() => sideBEvidence.value.length >= MAX_EVIDENCE_ITEMS_PER_SIDE)

  function isEvidenceLinkSubmitting(side: CaseSide) {
    return evidenceMutating.value && evidenceMutatingType.value === 'link' && evidenceMutatingSide.value === side
  }

  function isEvidenceFileSubmitting(side: CaseSide) {
    return evidenceMutating.value && evidenceMutatingType.value === 'file' && evidenceMutatingSide.value === side
  }

  function setEvidenceFile(side: CaseSide, event: Event) {
    const target = event.target as HTMLInputElement | null
    evidenceDrafts[side].file = target?.files?.[0] ?? null
    if (target) {
      target.value = ''
    }
  }

  function buildDefaultEvidenceTitle(fileName: string) {
    const extensionIndex = fileName.lastIndexOf('.')
    return extensionIndex > 0 ? fileName.slice(0, extensionIndex) : fileName
  }

  async function submitEvidenceLink(side: CaseSide) {
    const selectedCase = caseItem.value
    const user = activeUser.value
    if (!selectedCase || !user || !canAddEvidenceToSide(side)) return

    const draft = evidenceDrafts[side]
    const title = draft.linkTitle.trim()
    const url = draft.linkUrl.trim()
    if (!title || !url) {
      evidenceError.value = 'Provide both a title and URL before adding link evidence.'
      return
    }

    evidenceMutating.value = true
    evidenceMutatingSide.value = side
    evidenceMutatingType.value = 'link'
    evidenceError.value = null

    try {
      const created = await postCaseEvidenceLink(selectedCase.id, {
        side,
        title,
        url,
      })

      if (!isSelectedCaseContext(selectedCase.id)) {
        return
      }

      appendEvidenceItem(created)
      draft.linkTitle = ''
      draft.linkUrl = ''
    } catch {
      if (isSelectedCaseContext(selectedCase.id)) {
        evidenceError.value = 'Unable to add link evidence right now.'
      }
    } finally {
      evidenceMutating.value = false
      evidenceMutatingSide.value = null
      evidenceMutatingType.value = null
    }
  }

  async function submitEvidenceFile(side: CaseSide) {
    const selectedCase = caseItem.value
    const user = activeUser.value
    if (!selectedCase || !user || !canAddEvidenceToSide(side)) return

    const draft = evidenceDrafts[side]
    if (!draft.file) {
      evidenceError.value = 'Select a file before uploading evidence.'
      return
    }

    const title = draft.fileTitle.trim() || buildDefaultEvidenceTitle(draft.file.name)
    evidenceMutating.value = true
    evidenceMutatingSide.value = side
    evidenceMutatingType.value = 'file'
    evidenceError.value = null
    evidenceNotice.value = null

    try {
      const created = await uploadCaseEvidenceFile(selectedCase.id, {
        side,
        title,
        file: draft.file,
      })

      if (!isSelectedCaseContext(selectedCase.id)) {
        return
      }

      appendEvidenceItem(created)
      evidenceStatuses[created.id] = 'PendingScan'
      void refreshEvidenceStatus(created, evidenceRequestId)
      evidenceNotice.value = 'Upload complete. Security review is in progress; this page will update when the file is ready.'
      draft.fileTitle = ''
      draft.file = null
    } catch {
      if (isSelectedCaseContext(selectedCase.id)) {
        evidenceError.value = 'Unable to upload file evidence right now.'
      }
    } finally {
      evidenceMutating.value = false
      evidenceMutatingSide.value = null
      evidenceMutatingType.value = null
    }
  }

  onBeforeUnmount(() => {
    clearEvidencePreviewUrls()
    clearEvidenceStatusTimers()
    closeEvidenceViewer()
  })

  async function vote(side: 'A' | 'B') {
    const selectedUser = authStore.selectedUser
    const selectedCase = caseItem.value
    if (!selectedUser || !selectedCase) return

    const result = await courtStore.vote(selectedCase.id, side)
    if (result.success) {
      if (isViewingCase(selectedCase.id)) {
        if (result.updatedCase) {
          courtStore.selectedCase = result.updatedCase
        }
        hasVoted.value = true
        await loadCaseState(selectedCase.id, true)
      }
    } else {
      if (!isViewingCase(selectedCase.id)) {
        return
      }

      courtStore.error = result.error ?? 'Vote could not be submitted.'
      await refreshVoteStatus()
      if (hasVoted.value) {
        await loadCaseState(selectedCase.id, true)
        courtStore.error = null
      }
    }
  }

  async function closeCase() {
    const selectedCase = caseItem.value
    const user = activeUser.value
    if (!selectedCase || !user || !canCloseCase.value) return

    const result = await courtStore.closeCase(selectedCase.id)
    if (!isViewingCase(selectedCase.id)) {
      return
    }

    if (result.success) {
      if (result.updatedCase) {
        courtStore.selectedCase = result.updatedCase
      }
      await loadCaseState(selectedCase.id, true)
    } else {
      courtStore.error = result.error ?? 'Unable to close this case.'
    }
  }

  async function acceptInvitation() {
    const selectedCase = caseItem.value
    const user = activeUser.value
    if (!selectedCase || !user || !sideBClaim.value.trim()) return

    const result = await courtStore.acceptInvitation(selectedCase.id, sideBClaim.value.trim())
    if (!isViewingCase(selectedCase.id)) {
      return
    }

    if (result.success) {
      if (result.updatedCase) {
        courtStore.selectedCase = result.updatedCase
      }
      await loadCaseState(selectedCase.id, true)
      sideBClaim.value = ''
    } else {
      courtStore.error = result.error ?? 'Unable to accept the invitation right now.'
    }
  }

  async function declineInvitation() {
    const selectedCase = caseItem.value
    const user = activeUser.value
    if (!selectedCase || !user) return

    const success = await courtStore.declineInvitation(selectedCase.id)
    if (success) {
      await router.push('/')
    }
  }

  async function submitComment() {
    const selectedCase = caseItem.value
    const user = activeUser.value
    const trimmedMessage = commentMessage.value.trim()
    if (!selectedCase || !user || !trimmedMessage) return

    commentsSubmitting.value = true
    commentsError.value = null
    try {
      const created = await postCaseComment(selectedCase.id, {
        message: trimmedMessage,
      })
      comments.value = [...comments.value, created]
      commentMessage.value = ''
    } catch {
      commentsError.value = 'Unable to post this comment right now.'
    } finally {
      commentsSubmitting.value = false
    }
  }

  return {
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
    isParticipant,
    canVote,
    canVoteSideA,
    canVoteSideB,
    canComment,
    canCloseCase,
    closePermissionMessage,
    votePermissionMessage,
    evidence,
    evidenceLoading,
    evidenceLoaded,
    evidenceError,
    evidenceNotice,
    evidenceDrafts,
    sideAEvidence,
    sideBEvidence,
    sideARecord,
    sideBRecord,
    canAddEvidenceSideA,
    canAddEvidenceSideB,
    sideAEvidenceAtLimit,
    sideBEvidenceAtLimit,
    isEvidenceLinkSubmitting,
    isEvidenceFileSubmitting,
    setEvidenceFile,
    submitEvidenceLink,
    submitEvidenceFile,
    evidenceFileAccept: EVIDENCE_FILE_ACCEPT,
    getEvidencePreviewUrl,
    getEvidenceStatus,
    getEvidenceStatusLabel,
    isEvidenceReady,
    evidenceViewer,
    evidenceViewerLoadingId,
    evidenceRemovingId,
    openEvidenceFile,
    closeEvidenceViewer,
    downloadEvidenceFile,
    canRemoveEvidence,
    removeEvidence,
    maxEvidenceItemsPerSide: MAX_EVIDENCE_ITEMS_PER_SIDE,
    vote,
    closeCase,
    acceptInvitation,
    declineInvitation,
    submitComment,
  }
}
