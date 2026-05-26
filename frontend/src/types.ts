export type CaseSide = 'A' | 'B'
export type CaseStatus = 'Pending' | 'Open' | 'Closed'
export type UserRole = 'Member' | 'Moderator'
export type FriendRequestStatus = 'Pending' | 'Accepted' | 'Declined'

export interface AppUser {
  id: string
  userName: string
  displayName: string
  role: UserRole
}

export interface ArgumentPost {
  side: CaseSide
  userId: string
  userName: string
  claim: string
  postedAtUtc: string
}

export interface CommunityVerdict {
  votesForSideA: number
  votesForSideB: number
}

export interface ArgumentCase {
  id: string
  title: string
  category: string
  summary: string
  sideA: ArgumentPost
  sideB: ArgumentPost | null
  invitedUserId: string | null
  verdict: CommunityVerdict
  status: CaseStatus
  winnerSide: CaseSide | null
  createdAtUtc: string
}

export interface FriendRequest {
  id: string
  fromUserId: string
  toUserId: string
  status: FriendRequestStatus
  createdAtUtc: string
}

export interface CreateCaseRequest {
  title: string
  category: string
  summary: string
  sideAUserId: string
  sideAClaim: string
  invitedUserId: string
}

export interface AcceptInvitationRequest {
  userId: string
  claim: string
}

export interface DeclineInvitationRequest {
  userId: string
}

export interface SendFriendRequestDto {
  fromUserId: string
  toUserId: string
}

export interface RespondFriendRequestDto {
  actorUserId: string
}

export interface CastVoteRequest {
  userId: string
  side: CaseSide
}

export interface CloseCaseRequest {
  actorUserId: string
}

export interface UserRewardView {
  badgeCode: string
  badgeLabel: string
  iconKey: string
  tier: string
  reason: string
  awardedAtUtc: string
}
