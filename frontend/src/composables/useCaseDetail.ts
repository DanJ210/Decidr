import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useCourtStore } from '../stores/court'

export function useCaseDetail() {
  const route = useRoute()
  const router = useRouter()
  const courtStore = useCourtStore()
  const authStore = useAuthStore()

  const sideBClaim = ref('')

  onMounted(() => {
    const id = route.params.id
    if (typeof id === 'string') {
      void courtStore.loadCase(id)
    }
  })

  const caseItem = computed(() => courtStore.selectedCase)
  const activeUser = computed(() => authStore.selectedUser)

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

    const success = await courtStore.vote(selectedCase.id, selectedUser.id, side)
    if (success) {
      await courtStore.loadCase(selectedCase.id)
    }
  }

  async function closeCase() {
    const selectedCase = caseItem.value
    const user = activeUser.value
    if (!selectedCase || !user || !canCloseCase.value) return

    const success = await courtStore.closeCase(selectedCase.id, user.id)
    if (success) {
      await courtStore.loadCase(selectedCase.id)
    }
  }

  async function acceptInvitation() {
    const selectedCase = caseItem.value
    const user = activeUser.value
    if (!selectedCase || !user || !sideBClaim.value.trim()) return

    const success = await courtStore.acceptInvitation(selectedCase.id, user.id, sideBClaim.value.trim())
    if (success) {
      await courtStore.loadCase(selectedCase.id)
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

  return {
    courtStore,
    sideBClaim,
    caseItem,
    activeUser,
    totalVotes,
    isInvited,
    inviterName,
    isParticipant,
    canVote,
    canCloseCase,
    closePermissionMessage,
    vote,
    closeCase,
    acceptInvitation,
    declineInvitation,
  }
}
