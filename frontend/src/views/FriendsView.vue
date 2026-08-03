<script setup lang="ts">
import { Check, Clock3, Search, UserMinus, UserPlus, Users, X } from '@lucide/vue'
import { useFriends } from '../composables/useFriends'

const {
  friendsStore,
  userSearchTerm,
  friendSearchTerm,
  normalizedUserSearch,
  userSearchResults,
  filteredFriends,
  fromUserName,
  toUserName,
  sendRequest,
  respondToRequest,
  removeFriend,
} = useFriends()
</script>

<template>
  <section class="detail-shell social-page">
    <header class="secondary-page-header social-header">
      <div>
        <p class="eyebrow">Your circle</p>
        <h1>Friends</h1>
        <p>Find people to debate, respond to requests, and manage who can join your next case.</p>
      </div>
      <span class="social-count"><Users :size="18" /> {{ friendsStore.friends.length }}</span>
    </header>

    <p v-if="friendsStore.loading" class="notice">Loading...</p>
    <p v-if="friendsStore.error" class="notice error">{{ friendsStore.error }}</p>

    <section class="people-search-section" aria-labelledby="find-people-heading">
      <header class="social-section-heading">
        <div>
          <p class="eyebrow">Connect</p>
          <h2 id="find-people-heading">Find people</h2>
        </div>
      </header>

      <form class="search-field" role="search" @submit.prevent>
        <Search :size="18" aria-hidden="true" />
        <label for="people-search" class="visually-hidden">Search by name or username</label>
        <input
          id="people-search"
          v-model="userSearchTerm"
          placeholder="Search by name or username"
          autocomplete="off"
        />
      </form>

      <ul v-if="userSearchResults.length" class="people-list search-results">
        <li v-for="{ user, status, requestId } in userSearchResults" :key="user.id" class="people-row">
          <span class="person-avatar">{{ user.displayName.charAt(0) }}</span>
          <div class="person-copy">
            <strong>{{ user.displayName }}</strong>
            <span>@{{ user.userName }}</span>
          </div>

          <span v-if="status === 'friend'" class="relationship-status friend-status"><Check :size="14" /> Friend</span>
          <span v-else-if="status === 'request-sent'" class="relationship-status pending-status"><Clock3 :size="14" /> Sent</span>
          <div v-else-if="status === 'request-received' && requestId" class="row-actions">
            <button class="compact-action primary" title="Accept friend request" @click="respondToRequest(requestId, true)">
              <Check :size="16" /> Accept
            </button>
            <button class="icon-action danger-text" title="Decline friend request" aria-label="Decline friend request" @click="respondToRequest(requestId, false)">
              <X :size="18" />
            </button>
          </div>
          <button
            v-else
            class="compact-action"
            :disabled="friendsStore.loading"
            @click="sendRequest(user.id)"
          >
            <UserPlus :size="16" /> Add
          </button>
        </li>
      </ul>
      <p v-else-if="normalizedUserSearch" class="empty-list-state">
        No users found matching "{{ userSearchTerm }}".
      </p>
    </section>

    <section v-if="friendsStore.incomingRequests.length" class="social-section request-section">
      <header class="social-section-heading">
        <div>
          <p class="eyebrow">Needs attention</p>
          <h2>Friend requests</h2>
        </div>
        <span class="count-badge">{{ friendsStore.incomingRequests.length }}</span>
      </header>

      <ul class="people-list">
        <li v-for="request in friendsStore.incomingRequests" :key="request.id" class="people-row">
          <span class="person-avatar incoming-avatar">{{ fromUserName(request.fromUserId).charAt(0) }}</span>
          <div class="person-copy">
            <strong>{{ fromUserName(request.fromUserId) }}</strong>
            <span>Wants to connect</span>
          </div>
          <div class="row-actions">
            <button class="compact-action primary" @click="respondToRequest(request.id, true)">
              <Check :size="16" /> Accept
            </button>
            <button class="icon-action danger-text" title="Decline friend request" aria-label="Decline friend request" @click="respondToRequest(request.id, false)">
              <X :size="18" />
            </button>
          </div>
        </li>
      </ul>
    </section>

    <section v-if="friendsStore.outgoingRequests.length || friendsStore.outgoingError" class="social-section">
      <header class="social-section-heading">
        <div>
          <p class="eyebrow">Waiting</p>
          <h2>Sent requests</h2>
        </div>
        <span class="count-badge">{{ friendsStore.outgoingRequests.length }}</span>
      </header>
      <p v-if="friendsStore.outgoingError" class="notice error">{{ friendsStore.outgoingError }}</p>
      <ul class="people-list">
        <li v-for="request in friendsStore.outgoingRequests" :key="request.id" class="people-row">
          <span class="person-avatar pending-avatar">{{ toUserName(request.toUserId).charAt(0) }}</span>
          <div class="person-copy">
            <strong>{{ toUserName(request.toUserId) }}</strong>
            <span>Request sent</span>
          </div>
          <span class="relationship-status pending-status"><Clock3 :size="14" /> Pending</span>
        </li>
      </ul>
    </section>

    <section class="social-section friends-section">
      <header class="social-section-heading">
        <div>
          <p class="eyebrow">Connected</p>
          <h2>My friends</h2>
        </div>
        <span class="count-badge">{{ friendsStore.friends.length }}</span>
      </header>

      <form v-if="friendsStore.friends.length > 3" class="search-field friend-filter" role="search" @submit.prevent>
        <Search :size="17" aria-hidden="true" />
        <label for="friend-filter" class="visually-hidden">Filter friends</label>
        <input id="friend-filter" v-model="friendSearchTerm" placeholder="Filter friends" />
      </form>

      <p v-if="!friendsStore.friends.length && !friendsStore.loading" class="empty-list-state">
        Your circle is empty. Search above to find someone to debate.
      </p>
      <p v-else-if="friendsStore.friends.length && !filteredFriends.length" class="empty-list-state">
        No friends match "{{ friendSearchTerm }}".
      </p>
      <ul v-else class="people-list">
        <li v-for="friend in filteredFriends" :key="friend.id" class="people-row">
          <span class="person-avatar">{{ friend.displayName.charAt(0) }}</span>
          <div class="person-copy">
            <strong>{{ friend.displayName }}</strong>
            <span>@{{ friend.userName }} · {{ friend.role }}</span>
          </div>
          <button
            type="button"
            class="icon-action danger-text"
            :title="`Remove ${friend.displayName}`"
            :aria-label="`Remove ${friend.displayName} from friends`"
            @click="removeFriend(friend.id)"
          >
            <UserMinus :size="18" />
          </button>
        </li>
      </ul>
    </section>
  </section>
</template>
