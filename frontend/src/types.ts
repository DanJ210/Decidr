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

export interface CaseVoteStatus {
  hasVoted: boolean
}

export interface CurrentUserVote {
  side: CaseSide
  castAtUtc: string
  changeLockedAtUtc: string
  canChange: boolean
}

export interface CaseComment {
  id: string
  caseId: string
  userId: string
  userName: string
  message: string
  createdAtUtc: string
}

export type CaseEvidenceType = 'Link' | 'Image' | 'Document'

export interface CaseEvidenceItem {
  id: string
  caseId: string
  side: CaseSide
  addedByUserId: string
  addedByUserName: string
  type: CaseEvidenceType
  title: string
  resourceUrl: string
  mimeType: string | null
  sizeBytes: number | null
  createdAtUtc: string
}

export interface CaseEvidenceCollection {
  sideA: CaseEvidenceItem[]
  sideB: CaseEvidenceItem[]
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
  currentUserVote: CurrentUserVote | null
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

export interface RemoveFriendDto {
  actorUserId: string
  friendUserId: string
}

export interface CastVoteRequest {
  userId: string
  side: CaseSide
}

export interface CloseCaseRequest {
  actorUserId: string
}

export interface CreateCaseCommentRequest {
  userId: string
  message: string
}

export interface AddCaseEvidenceLinkRequest {
  userId: string
  side: CaseSide
  title: string
  url: string
}

export interface UserRewardView {
  badgeCode: string
  badgeLabel: string
  iconKey: string
  tier: string
  reason: string
  awardedAtUtc: string
}
