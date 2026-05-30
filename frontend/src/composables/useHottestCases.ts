import { computed, onMounted, watch } from 'vue'
import { useAuthStore } from '../stores/auth'
import { useCourtStore } from '../stores/court'
import { useFriendsStore } from '../stores/friends'

export function useHottestCases() {
  const courtStore = useCourtStore()
  const authStore = useAuthStore()
  const friendsStore = useFriendsStore()

  async function loadInvitations() {
    const userId = authStore.selectedUser?.id
    if (userId) {
      await friendsStore.loadInvitations(userId)
    }
  }

  onMounted(async () => {
    if (!courtStore.cases.length) {
      void courtStore.loadCases()
    }

    await loadInvitations()
  })

  watch(() => authStore.selectedUserId, loadInvitations)

  const hottestCases = computed(() => {
    return [...courtStore.cases]
      .sort(
        (a, b) =>
          b.verdict.votesForSideA + b.verdict.votesForSideB -
          (a.verdict.votesForSideA + a.verdict.votesForSideB),
      )
      .slice(0, 6)
  })

  return { courtStore, friendsStore, hottestCases }
}
