import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { fetchCaseVoteStatus } from '../services/api'
import { useAuthStore } from '../stores/auth'
import { useCourtStore } from '../stores/court'

export function useCaseDetail() {
  const route = useRoute()
  const router = useRouter()
  const courtStore = useCourtStore()
  const authStore = useAuthStore()

  const sideBClaim = ref('')
  const hasVoted = ref(false)
  const checkingVoteStatus = ref(false)
  let caseStateRequestId = 0
  let voteStatusRequestId = 0

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
      const status = await fetchCaseVoteStatus(selected.id, user.id)
      if (isCurrentVoteStatusRequest(requestId, selected.id, user.id)) {
        hasVoted.value = status.hasVoted
      }
    } catch {} finally {
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
      }
      checkingVoteStatus.value = false
      return
    }

    await refreshVoteStatus()
  }

  watch(
    () => route.params.id,
    (id) => {
      hasVoted.value = false
      checkingVoteStatus.value = false
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
      void refreshVoteStatus()
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

  const votePermissionMessage = computed(() => {
    const selected = caseItem.value
    const user = activeUser.value
    if (!selected || selected.status !== 'Open') return ''
    if (!user) return 'Select an active user to vote on this case.'
    if (isParticipant.value) return 'You are a participant in this case and cannot vote.'
    if (hasVoted.value) return 'You have already voted on this case.'
    return ''
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

    const result = await courtStore.vote(selectedCase.id, selectedUser.id, side)
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

    const result = await courtStore.closeCase(selectedCase.id, user.id)
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

    const result = await courtStore.acceptInvitation(selectedCase.id, user.id, sideBClaim.value.trim())
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

    const success = await courtStore.declineInvitation(selectedCase.id, user.id)
    if (success) {
      await router.push('/')
    }
  }

  return {
    courtStore,
    sideBClaim,
    caseItem,
    totalVotes,
    isInvited,
    inviterName,
    isParticipant,
    canVote,
    hasVoted,
    canCloseCase,
    closePermissionMessage,
    votePermissionMessage,
    vote,
    closeCase,
    acceptInvitation,
    declineInvitation,
  }
}
