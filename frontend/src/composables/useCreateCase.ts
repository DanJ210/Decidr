import { computed, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useCourtStore } from '../stores/court'
import { useFriendsStore } from '../stores/friends'
import type { RecordedClip } from './useVideoRecorder'

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

  const sideARecording = ref<RecordedClip | null>(null)

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
    if (!authStore.selectedUser?.id || !form.invitedUserId) return

    const created = await courtStore.createCase({
      title: form.title,
      category: form.category,
      summary: form.summary,
      sideAClaim: form.sideAClaim,
      invitedUserId: form.invitedUserId,
      sideARecordUrl: sideARecording.value?.url ?? null,
      sideADurationSeconds: sideARecording.value?.durationSeconds ?? null,
    })

    if (created) {
      await router.push(`/cases/${created.id}`)
    }
  }

  return {
    authStore,
    courtStore,
    form,
    sideARecording,
    inviteCandidates,
    submit,
  }
}
