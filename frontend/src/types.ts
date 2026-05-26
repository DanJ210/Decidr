export type CaseSide = 'A' | 'B'
export type CaseStatus = 'Open' | 'Closed'
export type UserRole = 'Member' | 'Moderator'

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
  sideB: ArgumentPost
  verdict: CommunityVerdict
  status: CaseStatus
  winnerSide: CaseSide | null
  createdAtUtc: string
}

export interface CreateCaseRequest {
  title: string
  category: string
  summary: string
  sideAUserId: string
  sideAClaim: string
  sideBUserId: string
  sideBClaim: string
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
