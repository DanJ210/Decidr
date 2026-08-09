import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  fetchCaseComments,
  fetchCaseEvidence,
  fetchCaseEvidenceFile,
  fetchCaseVoteStatus,
  postCaseComment,
  postCaseEvidenceLink,
  uploadCaseEvidenceFile,
} from '../services/api'
import { useAuthStore } from '../stores/auth'
import { useCourtStore } from '../stores/court'
import type { CaseComment, CaseEvidenceCollection, CaseEvidenceItem, CaseSide } from '../types'

const MAX_EVIDENCE_ITEMS_PER_SIDE = 20
const EVIDENCE_FILE_ACCEPT = '.jpg,.jpeg,.png,.webp,.gif,.pdf,.txt,.doc,.docx'

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
  const evidenceMutating = ref(false)
  const evidenceMutatingSide = ref<CaseSide | null>(null)
  const evidenceMutatingType = ref<'link' | 'file' | null>(null)
  const evidenceError = ref<string | null>(null)
  const evidencePreviewUrls = reactive<Record<string, string>>({})
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

  function clearEvidencePreviewUrls() {
    for (const objectUrl of Object.values(evidencePreviewUrls)) {
      URL.revokeObjectURL(objectUrl)
    }
    for (const evidenceId of Object.keys(evidencePreviewUrls)) {
      delete evidencePreviewUrls[evidenceId]
    }
  }

  async function loadEvidencePreview(item: CaseEvidenceItem, requestId: number) {
    if (item.type !== 'Image') return

    try {
      const content = await fetchCaseEvidenceFile(item.caseId, item.id)
      const objectUrl = URL.createObjectURL(content)
      if (requestId !== evidenceRequestId || !isViewingCase(item.caseId)) {
        URL.revokeObjectURL(objectUrl)
        return
      }
      evidencePreviewUrls[item.id] = objectUrl
    } catch {
      // The file link remains available if an inline preview cannot be loaded.
    }
  }

  function getEvidencePreviewUrl(item: CaseEvidenceItem) {
    return evidencePreviewUrls[item.id] ?? ''
  }

  async function openEvidenceFile(item: CaseEvidenceItem) {
    if (item.type === 'Link') {
      window.open(item.resourceUrl, '_blank', 'noopener,noreferrer')
      return
    }

    evidenceError.value = null
    try {
      const content = await fetchCaseEvidenceFile(item.caseId, item.id)
      const objectUrl = URL.createObjectURL(content)
      const link = document.createElement('a')
      link.href = objectUrl
      link.target = '_blank'
      link.rel = 'noopener noreferrer'
      link.click()
      window.setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000)
    } catch {
      evidenceError.value = 'Unable to open this evidence file right now.'
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
    evidenceError.value = null

    try {
      const loaded = await fetchCaseEvidence(caseId)
      if (isCurrentEvidenceRequest(requestId, caseId)) {
        clearEvidencePreviewUrls()
        evidence.value = loaded
        for (const item of [...loaded.sideA, ...loaded.sideB]) {
          void loadEvidencePreview(item, requestId)
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
        evidenceError.value = null
        resetAllEvidenceDrafts()
      }
      checkingVoteStatus.value = false
      commentsLoading.value = false
      evidenceLoading.value = false
      return
    }

    await Promise.all([
      refreshVoteStatus(),
      loadComments(id),
      loadEvidence(id),
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
      await loadEvidencePreview(created, evidenceRequestId)
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

  onBeforeUnmount(clearEvidencePreviewUrls)

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
    evidenceFileAccept: EVIDENCE_FILE_ACCEPT,
    getEvidencePreviewUrl,
    openEvidenceFile,
    maxEvidenceItemsPerSide: MAX_EVIDENCE_ITEMS_PER_SIDE,
    vote,
    closeCase,
    acceptInvitation,
    declineInvitation,
    submitComment,
  }
}
