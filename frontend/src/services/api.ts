import axios from 'axios'
import type {
  AcceptInvitationRequest,
  AppUser,
  ArgumentCase,
  CastVoteRequest,
  CloseCaseRequest,
  CreateCaseRequest,
  FriendRequest,
  RespondFriendRequestDto,
  SendFriendRequestDto,
  UserRewardView,
} from '../types'

const apiClient = axios.create({
  baseURL: '/api',
  timeout: 10000,
})

export async function fetchCases(): Promise<ArgumentCase[]> {
  const { data } = await apiClient.get<ArgumentCase[]>('/cases')
  return data
}

export async function fetchCaseById(id: string): Promise<ArgumentCase> {
  const { data } = await apiClient.get<ArgumentCase>(`/cases/${id}`)
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

export async function closeCase(caseId: string, request: CloseCaseRequest): Promise<ArgumentCase> {
  const { data } = await apiClient.post<ArgumentCase>(`/cases/${caseId}/close`, request)
  return data
}

export async function acceptCaseInvitation(caseId: string, request: AcceptInvitationRequest): Promise<ArgumentCase> {
  const { data } = await apiClient.post<ArgumentCase>(`/cases/${caseId}/accept`, request)
  return data
}

export async function declineCaseInvitation(caseId: string, userId: string): Promise<void> {
  await apiClient.post(`/cases/${caseId}/decline`, { userId })
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

export async function fetchInvitations(userId: string): Promise<ArgumentCase[]> {
  const { data } = await apiClient.get<ArgumentCase[]>(`/users/${userId}/invitations`)
  return data
}

export async function sendFriendRequest(dto: SendFriendRequestDto): Promise<void> {
  await apiClient.post('/friends/request', dto)
}

export async function respondToFriendRequest(requestId: string, dto: RespondFriendRequestDto, accept: boolean): Promise<void> {
  const path = accept ? 'accept' : 'decline'
  await apiClient.post(`/friends/${requestId}/${path}`, dto)
}
