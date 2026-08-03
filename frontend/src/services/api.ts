import axios from 'axios'
import { getAccessToken } from '../authConfig'
import type {
  AcceptInvitationRequest,
  AddCaseEvidenceLinkRequest,
  AppUser,
  ArgumentCase,
  CaseEvidenceCollection,
  CaseEvidenceItem,
  CaseSide,
  CaseVoteStatus,
  CaseComment,
  CastVoteRequest,
  CreateCaseRequest,
  CreateCaseCommentRequest,
  FriendRequest,
  UserRewardView,
} from '../types'

const apiClient = axios.create({
  baseURL: '/api',
  timeout: 10000,
})

apiClient.interceptors.request.use(async (config) => {
  const accessToken = await getAccessToken()
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`
  }
  return config
})

export async function fetchCurrentUser(): Promise<AppUser> {
  const { data } = await apiClient.get<AppUser>('/auth/me')
  return data
}

export async function fetchCases(): Promise<ArgumentCase[]> {
  const { data } = await apiClient.get<ArgumentCase[]>('/cases')
  return data
}

export async function fetchCaseById(id: string, userId?: string): Promise<ArgumentCase> {
  const { data } = await apiClient.get<ArgumentCase>(`/cases/${id}`, {
    params: userId ? { userId } : undefined,
  })
  return data
}

export async function fetchCaseVoteStatus(caseId: string, userId: string): Promise<CaseVoteStatus> {
  const { data } = await apiClient.get<CaseVoteStatus>(`/cases/${caseId}/vote-status`, {
    params: { userId },
  })
  return data
}

export async function createCase(request: CreateCaseRequest): Promise<ArgumentCase> {
  const { data } = await apiClient.post<ArgumentCase>('/cases', request)
  return data
}

export async function castVote(caseId: string, request: CastVoteRequest): Promise<ArgumentCase> {
  const { data } = await apiClient.post<ArgumentCase>(`/cases/${caseId}/vote`, request)
  return data
}

export async function closeCase(caseId: string): Promise<ArgumentCase> {
  const { data } = await apiClient.post<ArgumentCase>(`/cases/${caseId}/close`)
  return data
}

export async function fetchCaseComments(caseId: string): Promise<CaseComment[]> {
  const { data } = await apiClient.get<CaseComment[]>(`/cases/${caseId}/comments`)
  return data
}

export async function postCaseComment(caseId: string, request: CreateCaseCommentRequest): Promise<CaseComment> {
  const { data } = await apiClient.post<CaseComment>(`/cases/${caseId}/comments`, request)
  return data
}

export async function fetchCaseEvidence(caseId: string): Promise<CaseEvidenceCollection> {
  const { data } = await apiClient.get<CaseEvidenceCollection>(`/cases/${caseId}/evidence`)
  return data
}

export async function postCaseEvidenceLink(caseId: string, request: AddCaseEvidenceLinkRequest): Promise<CaseEvidenceItem> {
  const { data } = await apiClient.post<CaseEvidenceItem>(`/cases/${caseId}/evidence/link`, request)
  return data
}

export async function uploadCaseEvidenceFile(
  caseId: string,
  request: { side: CaseSide; title: string; file: File }
): Promise<CaseEvidenceItem> {
  const formData = new FormData()
  formData.append('side', request.side)
  formData.append('title', request.title)
  formData.append('file', request.file)

  const { data } = await apiClient.post<CaseEvidenceItem>(`/cases/${caseId}/evidence/upload`, formData)
  return data
}

export async function acceptCaseInvitation(caseId: string, request: AcceptInvitationRequest): Promise<ArgumentCase> {
  const { data } = await apiClient.post<ArgumentCase>(`/cases/${caseId}/accept`, request)
  return data
}

export async function declineCaseInvitation(caseId: string): Promise<void> {
  await apiClient.post(`/cases/${caseId}/decline`)
}

export async function fetchUsers(): Promise<AppUser[]> {
  const { data } = await apiClient.get<AppUser[]>('/users')
  return data
}

export async function fetchUserRewards(userId: string): Promise<UserRewardView[]> {
  const { data } = await apiClient.get<UserRewardView[]>(`/users/${userId}/rewards`)
  return data
}

export async function fetchFriends(userId: string): Promise<AppUser[]> {
  const { data } = await apiClient.get<AppUser[]>(`/users/${userId}/friends`)
  return data
}

export async function fetchFriendRequests(userId: string): Promise<FriendRequest[]> {
  const { data } = await apiClient.get<FriendRequest[]>(`/users/${userId}/friend-requests`)
  return data
}

export async function fetchOutgoingFriendRequests(userId: string): Promise<FriendRequest[]> {
  const { data, headers } = await apiClient.get<FriendRequest[]>(`/users/${userId}/sent-requests`)
  if (!Array.isArray(data)) {
    const contentType = headers['content-type'] ?? 'unknown'
    throw new Error(`Unexpected response for outgoing friend requests (${contentType}).`)
  }
  return data
}

export async function fetchInvitations(userId: string): Promise<ArgumentCase[]> {
  const { data } = await apiClient.get<ArgumentCase[]>(`/users/${userId}/invitations`)
  return data
}

export async function sendFriendRequest(toUserId: string): Promise<void> {
  await apiClient.post('/friends/request', { toUserId })
}

export async function respondToFriendRequest(requestId: string, accept: boolean): Promise<void> {
  const path = accept ? 'accept' : 'decline'
  await apiClient.post(`/friends/${requestId}/${path}`)
}

export async function removeFriend(friendUserId: string): Promise<void> {
  await apiClient.post('/friends/remove', { friendUserId })
}
