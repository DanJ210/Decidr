import { computed, reactive, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useCourtStore } from '../stores/court'
import { useFriendsStore } from '../stores/friends'

export function useCreateCase() {
  const courtStore = useCourtStore()
  const authStore = useAuthStore()
  const friendsStore = useFriendsStore()
  const router = useRouter()

  const form = reactive({
    title: '',
    category: '',
    summary: '',
    sideAClaim: '',
    invitedUserId: '',
  })

  async function loadData() {
    if (!authStore.users.length) {
      await authStore.loadUsers()
    }

    const userId = authStore.selectedUser?.id
    if (userId) {
      friendsStore.setActiveUser(userId)
      await friendsStore.loadFriends(userId)
    }

    if (!form.invitedUserId) {
      const firstFriend = friendsStore.friends[0]
      if (firstFriend) {
        form.invitedUserId = firstFriend.id
      }
    }
  }

  void loadData()

  const inviteCandidates = computed(() => friendsStore.friends)

  watch(inviteCandidates, (friends) => {
    if (!friends.length) {
      form.invitedUserId = ''
      return
    }

    if (!friends.some((friend) => friend.id === form.invitedUserId)) {
      form.invitedUserId = friends[0].id
    }
  })

  async function submit() {
    const userId = authStore.selectedUser?.id
    if (!userId || !form.invitedUserId) return

    const created = await courtStore.createCase({
      title: form.title,
      category: form.category,
      summary: form.summary,
      sideAUserId: userId,
      sideAClaim: form.sideAClaim,
      invitedUserId: form.invitedUserId,
    })

    if (created) {
      await router.push(`/cases/${created.id}`)
    }
  }

  return {
    authStore,
    courtStore,
    form,
    inviteCandidates,
    submit,
  }
}
