import axios from 'axios'
import type {
  AppUser,
  ArgumentCase,
  CastVoteRequest,
  CloseCaseRequest,
  CreateCaseRequest,
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

export async function fetchUsers(): Promise<AppUser[]> {
  const { data } = await apiClient.get<AppUser[]>('/users')
  return data
}

export async function fetchUserRewards(userId: string): Promise<UserRewardView[]> {
  const { data } = await apiClient.get<UserRewardView[]>(`/users/${userId}/rewards`)
  return data
}
