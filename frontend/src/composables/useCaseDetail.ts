import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { fetchCaseComments, postCaseComment } from '../services/api'
import { useAuthStore } from '../stores/auth'
import { useCourtStore } from '../stores/court'
import type { CaseComment } from '../types'

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
  const nowTimestamp = ref(Date.now())
  let clockHandle: ReturnType<typeof setInterval> | null = null

  function isVoteChangeWindowOpen(changeLockedAtUtc: string) {
    return nowTimestamp.value < new Date(changeLockedAtUtc).getTime()
  }

  async function loadCurrentCase() {
    const id = route.params.id
    if (typeof id !== 'string') return
    await courtStore.loadCase(id, activeUser.value?.id)
    await loadComments(id)
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

  onMounted(() => {
    clockHandle = setInterval(() => {
      nowTimestamp.value = Date.now()
    }, 30000)
    void loadCurrentCase()
  })

  onUnmounted(() => {
    if (clockHandle) {
      clearInterval(clockHandle)
      clockHandle = null
    }
  })

  const caseItem = computed(() => courtStore.selectedCase)
  const activeUser = computed(() => authStore.selectedUser)

  watch(
    [() => route.params.id, () => activeUser.value?.id],
    () => {
      void loadCurrentCase()
    },
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
    return !!selected && !!user && selected.status === 'Open' && !isParticipant.value
  })

  const currentUserVote = computed(() => caseItem.value?.currentUserVote ?? null)
  const canComment = computed(() => !!caseItem.value && !!activeUser.value)

  const canVoteSideA = computed(() => {
    if (!canVote.value) return false

    const vote = currentUserVote.value
    if (!vote) return true
    if (!isVoteChangeWindowOpen(vote.changeLockedAtUtc)) return false
    return vote.side !== 'A'
  })

  const canVoteSideB = computed(() => {
    if (!canVote.value) return false

    const vote = currentUserVote.value
    if (!vote) return true
    if (!isVoteChangeWindowOpen(vote.changeLockedAtUtc)) return false
    return vote.side !== 'B'
  })

  const voteStatusMessage = computed(() => {
    const vote = currentUserVote.value
    if (!vote) return ''

    if (isVoteChangeWindowOpen(vote.changeLockedAtUtc)) {
      const lockAt = new Date(vote.changeLockedAtUtc).toLocaleString()
      return `You voted for Side ${vote.side}. You can switch sides until ${lockAt}.`
    }

    return `Your vote for Side ${vote.side} is locked and can no longer be changed.`
  })

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

  async function vote(side: 'A' | 'B') {
    const selectedUser = authStore.selectedUser
    const selectedCase = caseItem.value
    if (!selectedUser || !selectedCase) return
    if ((side === 'A' && !canVoteSideA.value) || (side === 'B' && !canVoteSideB.value)) return

    const success = await courtStore.vote(selectedCase.id, selectedUser.id, side)
    if (success) {
      await courtStore.loadCase(selectedCase.id, selectedUser.id)
    }
  }

  async function closeCase() {
    const selectedCase = caseItem.value
    const user = activeUser.value
    if (!selectedCase || !user || !canCloseCase.value) return

    const success = await courtStore.closeCase(selectedCase.id, user.id)
    if (success) {
      await courtStore.loadCase(selectedCase.id, user.id)
    }
  }

  async function acceptInvitation() {
    const selectedCase = caseItem.value
    const user = activeUser.value
    if (!selectedCase || !user || !sideBClaim.value.trim()) return

    const success = await courtStore.acceptInvitation(selectedCase.id, user.id, sideBClaim.value.trim())
    if (success) {
      await courtStore.loadCase(selectedCase.id, user.id)
      sideBClaim.value = ''
    }
  }

  async function declineInvitation() {
    const selectedCase = caseItem.value
    const user = activeUser.value
    if (!selectedCase || !user) return

    const success = await courtStore.declineInvitation(selectedCase.id, user.id)
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
        userId: user.id,
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
    canComment,
    canVoteSideA,
    canVoteSideB,
    currentUserVote,
    voteStatusMessage,
    canCloseCase,
    closePermissionMessage,
    vote,
    closeCase,
    acceptInvitation,
    declineInvitation,
    submitComment,
  }
}
