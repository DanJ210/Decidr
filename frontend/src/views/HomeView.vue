<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import { ArrowLeft, ArrowRight, Bell, Info, Pause, Play, Volume2, VolumeX } from '@lucide/vue'
import { useHottestCases } from '../composables/useHottestCases'
import { useAuthStore } from '../stores/auth'
import type { ArgumentCase, ArgumentPost, CaseSide, CaseStatus } from '../types'

const { courtStore, friendsStore, caseFeed } = useHottestCases()
const authStore = useAuthStore()
type FeedFilter = 'All' | Extract<CaseStatus, 'Open' | 'Closed'>

const filters: FeedFilter[] = ['All', 'Open', 'Closed']
const activeFilter = ref<FeedFilter>('All')
const activeCaseIndex = ref(0)
const activeSide = ref<CaseSide>('A')
const feedViewport = ref<HTMLElement | null>(null)
const videoElements = new Map<string, HTMLVideoElement>()
const muted = ref(true)
const playing = ref(false)
const swipeStart = ref<{ x: number; y: number } | null>(null)
const feedback = ref('')

const filteredCaseFeed = computed(() => activeFilter.value === 'All'
  ? caseFeed.value
  : caseFeed.value.filter((item) => item.status === activeFilter.value))
const activeCase = computed(() => filteredCaseFeed.value[activeCaseIndex.value] ?? null)

function totalVotes(item: ArgumentCase) { return item.verdict.votesForSideA + item.verdict.votesForSideB }
function sideAPercentage(item: ArgumentCase) { const total = totalVotes(item); return total ? Math.round((item.verdict.votesForSideA / total) * 100) : 50 }
function postForSide(item: ArgumentCase, side: CaseSide): ArgumentPost | null { return side === 'A' ? item.sideA : item.sideB }
function shownSide(index: number): CaseSide { return index === activeCaseIndex.value ? activeSide.value : 'A' }
function visiblePost(item: ArgumentCase, index: number) { return postForSide(item, shownSide(index)) }
function shouldLoadMedia(index: number) { return Math.abs(index - activeCaseIndex.value) <= 1 }
function videoKey(caseId: string, side: CaseSide) { return `${caseId}-${side}` }
function hasSeenVerdict(item: ArgumentCase) { return item.currentUserVote !== null }

function setVideoElement(key: string, element: Element | null) {
  if (element instanceof HTMLVideoElement) videoElements.set(key, element)
  else videoElements.delete(key)
}

async function playActiveVideo() {
  await nextTick()
  const item = activeCase.value
  if (!item) return
  for (const [key, video] of videoElements) if (key !== videoKey(item.id, activeSide.value)) video.pause()
  const video = videoElements.get(videoKey(item.id, activeSide.value))
  if (!video) { playing.value = false; return }
  video.muted = muted.value
  try { await video.play(); playing.value = true } catch { playing.value = false }
}

function handleVideoEnded(item: ArgumentCase, side: CaseSide) {
  playing.value = false
  if (item.id === activeCase.value?.id && side === activeSide.value && side === 'A' && item.sideB) activeSide.value = 'B'
}

function togglePlayback() {
  const item = activeCase.value
  const video = item ? videoElements.get(videoKey(item.id, activeSide.value)) : undefined
  if (!video) return
  if (video.paused) void video.play().then(() => { playing.value = true })
  else { video.pause(); playing.value = false }
}

function toggleMute() { muted.value = !muted.value; for (const video of videoElements.values()) video.muted = muted.value }
function updateActiveCase() {
  const viewport = feedViewport.value
  if (!viewport) return
  const nextIndex = Math.round(viewport.scrollTop / viewport.clientHeight)
  if (nextIndex !== activeCaseIndex.value && filteredCaseFeed.value[nextIndex]) activeCaseIndex.value = nextIndex
}
function handlePointerDown(event: PointerEvent) { swipeStart.value = { x: event.clientX, y: event.clientY } }
function handlePointerUp(event: PointerEvent) {
  const start = swipeStart.value
  swipeStart.value = null
  if (!start) return
  const horizontal = event.clientX - start.x
  const vertical = event.clientY - start.y
  if (Math.abs(horizontal) >= 72 && Math.abs(horizontal) > Math.abs(vertical)) void vote(horizontal > 0 ? 'A' : 'B')
}
function canVote(item: ArgumentCase) {
  const userId = authStore.selectedUser?.id
  const isParticipant = userId === item.sideA.userId || userId === item.sideB?.userId
  return item.status === 'Open' && !!item.sideB && !!userId && !isParticipant && !item.currentUserVote && !courtStore.mutating
}
async function vote(side: CaseSide) {
  const item = activeCase.value
  if (!item) return
  if (!canVote(item)) { feedback.value = item.currentUserVote ? 'Your verdict is already recorded.' : 'This case is not available for your vote.'; return }
  const result = await courtStore.vote(item.id, side)
  feedback.value = result.success ? `You chose Side ${side}. Community results are now visible.` : result.error ?? 'Vote could not be submitted.'
}
function changeFilter(filter: FeedFilter) {
  activeFilter.value = filter
  activeCaseIndex.value = 0
  activeSide.value = 'A'
  feedViewport.value?.scrollTo({ top: 0, behavior: 'smooth' })
}

watch(activeCaseIndex, () => { activeSide.value = 'A'; feedback.value = ''; void playActiveVideo() })
watch(activeSide, () => void playActiveVideo())
watch(filteredCaseFeed, (items) => { if (activeCaseIndex.value >= items.length) activeCaseIndex.value = Math.max(0, items.length - 1); void playActiveVideo() }, { flush: 'post' })
onBeforeUnmount(() => { for (const video of videoElements.values()) video.pause() })
</script>

<template>
  <section class="case-feed" aria-label="Community case feed">
    <header class="case-feed-header">
      <div class="case-feed-branding"><span>Community cases</span><strong>{{ filteredCaseFeed.length }} in the feed</strong></div>
      <div class="case-feed-filters" role="group" aria-label="Filter cases by status">
        <button v-for="filter in filters" :key="filter" type="button" :class="{ active: activeFilter === filter }" :aria-pressed="activeFilter === filter" @click="changeFilter(filter)">{{ filter }}</button>
      </div>
      <RouterLink v-if="friendsStore.invitations.length" :to="`/cases/${friendsStore.invitations[0].id}`" class="case-feed-invitation" :aria-label="`${friendsStore.invitations.length} pending case invitations`"><Bell :size="18" aria-hidden="true" /><span>{{ friendsStore.invitations.length }}</span></RouterLink>
    </header>

    <p v-if="courtStore.loading" class="case-feed-notice">Loading cases...</p>
    <p v-else-if="courtStore.error" class="case-feed-notice error">{{ courtStore.error }}</p>
    <div v-else-if="filteredCaseFeed.length" ref="feedViewport" class="case-feed-viewport" @scroll.passive="updateActiveCase" @pointerdown="handlePointerDown" @pointerup="handlePointerUp">
      <article v-for="(item, index) in filteredCaseFeed" :key="item.id" class="case-feed-item" :aria-label="item.title">
        <div class="case-feed-media" :class="`side-${shownSide(index).toLowerCase()}`">
          <video v-if="visiblePost(item, index)?.mediaUrl && shouldLoadMedia(index)" :ref="(element) => setVideoElement(videoKey(item.id, shownSide(index)), element as Element | null)" :src="visiblePost(item, index)?.mediaUrl ?? undefined" :poster="visiblePost(item, index)?.thumbnailUrl ?? undefined" :muted="muted" playsinline preload="metadata" @ended="handleVideoEnded(item, shownSide(index))"></video>
          <div v-else class="case-feed-no-video"><span>Side {{ shownSide(index) }}</span><p>{{ visiblePost(item, index)?.claim ?? 'The response has not been recorded yet.' }}</p></div>
          <div class="case-feed-shade"></div>
        </div>
        <div class="case-feed-content">
          <div class="case-feed-topline"><span>{{ item.category }}</span><span>{{ item.status }}</span></div>
          <div class="case-feed-copy"><p>Side {{ shownSide(index) }} <i></i> @{{ visiblePost(item, index)?.userName ?? 'Awaiting response' }}</p><h1>{{ item.title }}</h1><p>{{ item.summary }}</p></div>
          <div class="case-feed-sequence" aria-label="Argument playback sequence">
            <button type="button" :class="{ active: shownSide(index) === 'A' }" @click="index === activeCaseIndex && (activeSide = 'A')"><span>01</span> Side A</button><i></i>
            <button type="button" :class="{ active: shownSide(index) === 'B' }" :disabled="!item.sideB" @click="index === activeCaseIndex && (activeSide = 'B')"><span>02</span> Side B</button>
          </div>
          <div v-if="index === activeCaseIndex" class="case-feed-actions">
            <div class="case-feed-playback"><button type="button" :aria-label="'Play or pause Side ' + activeSide" @click="togglePlayback"><Pause v-if="playing" :size="20" aria-hidden="true" /><Play v-else :size="20" aria-hidden="true" /></button><button type="button" :aria-label="muted ? 'Unmute video' : 'Mute video'" @click="toggleMute"><VolumeX v-if="muted" :size="20" aria-hidden="true" /><Volume2 v-else :size="20" aria-hidden="true" /></button><RouterLink :to="`/cases/${item.id}`" aria-label="Open full case details"><Info :size="20" aria-hidden="true" /></RouterLink></div>
            <div class="case-feed-vote-area">
              <template v-if="hasSeenVerdict(item)"><div class="case-feed-verdict"><div><span>Side A</span><strong>{{ sideAPercentage(item) }}%</strong></div><div class="case-feed-meter" role="img" :aria-label="`${sideAPercentage(item)} percent Side A and ${100 - sideAPercentage(item)} percent Side B`"><span :style="{ width: `${sideAPercentage(item)}%` }"></span></div><div><strong>{{ 100 - sideAPercentage(item) }}%</strong><span>Side B</span></div></div><p class="case-feed-result-note">{{ totalVotes(item) }} community {{ totalVotes(item) === 1 ? 'vote' : 'votes' }}</p></template>
              <template v-else><button class="case-feed-vote side-a" type="button" :disabled="!canVote(item)" @click="vote('A')"><ArrowRight :size="18" aria-hidden="true" /><span>Side A</span></button><p>Choose your verdict</p><button class="case-feed-vote side-b" type="button" :disabled="!canVote(item)" @click="vote('B')"><span>Side B</span><ArrowLeft :size="18" aria-hidden="true" /></button></template>
            </div>
          </div>
        </div>
      </article>
    </div>
    <p v-else class="case-feed-notice">No {{ activeFilter === 'All' ? '' : activeFilter.toLowerCase() + ' ' }}cases right now.</p>
    <p class="sr-only" aria-live="polite">{{ feedback }}</p>
  </section>
</template>

<style scoped>
.case-feed {
  position: relative;
  height: calc(100dvh - 10.75rem);
  min-height: 34rem;
  margin: -1.25rem -1rem -1.5rem;
  background: #161b20;
  color: #fff;
}

.case-feed-header {
  position: absolute;
  inset: 0 0 auto;
  z-index: 2;
  display: flex;
  align-items: center;
  gap: .75rem;
  padding: 1rem;
  background: linear-gradient(#101419d9, transparent);
}

.case-feed-branding {
  display: grid;
  line-height: 1.1;
}

.case-feed-branding span {
  font-size: .72rem;
  font-weight: 700;
  text-transform: uppercase;
  opacity: .7;
}

.case-feed-branding strong {
  font-family: 'Newsreader', serif;
  font-size: 1.1rem;
}

.case-feed-filters {
  display: flex;
  gap: .25rem;
  margin-left: auto;
}

.case-feed-filters button,
.case-feed-invitation {
  min-width: 2.35rem;
  min-height: 2.35rem;
  border: 1px solid #ffffff5c;
  border-radius: 5px;
  background: #151a1db8;
  color: #fff;
  font-size: .74rem;
  font-weight: 700;
  cursor: pointer;
}

.case-feed-filters button.active {
  border-color: var(--signal);
  background: var(--signal);
  color: var(--ink);
}

.case-feed-invitation {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: .25rem;
  text-decoration: none;
}

.case-feed-viewport {
  height: 100%;
  overflow-y: auto;
  scroll-snap-type: y mandatory;
  overscroll-behavior-y: contain;
  scrollbar-width: none;
}

.case-feed-viewport::-webkit-scrollbar { display: none; }

.case-feed-item {
  position: relative;
  height: 100%;
  scroll-snap-align: start;
  scroll-snap-stop: always;
  overflow: hidden;
}

.case-feed-media,
.case-feed-media video,
.case-feed-no-video,
.case-feed-shade {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
}

.case-feed-media video {
  object-fit: cover;
  background: #202830;
}

.case-feed-no-video {
  display: grid;
  place-content: center;
  gap: .7rem;
  padding: 4rem 2rem;
  background: linear-gradient(140deg, #2557d6, #182a42 55%, #b83a36);
  text-align: center;
}

.case-feed-no-video span {
  font-size: .76rem;
  font-weight: 700;
  text-transform: uppercase;
}

.case-feed-no-video p {
  max-width: 28rem;
  margin: 0;
  font-family: 'Newsreader', serif;
  font-size: 1.6rem;
  line-height: 1.15;
}

.case-feed-shade {
  background: linear-gradient(180deg, #0d11162b 25%, #0d111659 48%, #0d1116f2 100%);
  pointer-events: none;
}

.case-feed-content {
  position: relative;
  z-index: 1;
  height: 100%;
  display: flex;
  flex-direction: column;
  justify-content: end;
  padding: 5.25rem 1rem 1rem;
}

.case-feed-topline { display: flex; justify-content: space-between; gap: 1rem; margin-bottom: auto; font-size: .72rem; font-weight: 700; text-transform: uppercase; }
.case-feed-topline span { padding: .28rem .45rem; border: 1px solid #ffffff73; border-radius: 4px; background: #11161a99; }
.case-feed-copy { max-width: 38rem; }
.case-feed-copy > p:first-child { display: flex; align-items: center; gap: .35rem; margin: 0 0 .35rem; font-size: .77rem; font-weight: 700; }
.case-feed-copy i { width: .45rem; height: .45rem; border-radius: 50%; background: var(--side-a); }
.side-b .case-feed-copy i { background: var(--side-b); }
.case-feed-copy h1 { margin: 0; font-family: 'Newsreader', serif; font-size: clamp(1.8rem, 8vw, 3rem); line-height: .98; }
.case-feed-copy > p:last-child { margin: .6rem 0 0; color: #ffffffd1; font-size: .88rem; }
.case-feed-sequence { display: grid; grid-template-columns: auto 1fr auto; align-items: center; gap: .5rem; margin: 1.05rem 0; }
.case-feed-sequence button { border: 0; padding: 0; background: transparent; color: #ffffffa8; cursor: pointer; font-size: .76rem; font-weight: 700; }
.case-feed-sequence button span { margin-right: .3rem; color: var(--signal); }
.case-feed-sequence button.active { color: #fff; }
.case-feed-sequence button:disabled { cursor: default; opacity: .45; }
.case-feed-sequence i { height: 1px; background: #ffffff75; }
.case-feed-actions { display: grid; gap: .8rem; }
.case-feed-playback { display: flex; gap: .45rem; }
.case-feed-playback button, .case-feed-playback a { display: inline-grid; place-items: center; width: 2.6rem; height: 2.6rem; border: 1px solid #ffffff6b; border-radius: 50%; background: #11161a9e; color: #fff; cursor: pointer; }

.case-feed-vote-area {
  display: grid;
  grid-template-columns: 1fr auto 1fr;
  align-items: center;
  gap: .55rem;
  min-height: 3.25rem;
}

.case-feed-vote {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: .4rem;
  min-height: 3.25rem;
  border: 0;
  border-radius: 5px;
  color: #fff;
  cursor: pointer;
  font-size: .82rem;
  font-weight: 800;
}

.case-feed-vote:disabled {
  cursor: not-allowed;
  opacity: .45;
}

.case-feed-vote.side-a { background: var(--side-a); }
.case-feed-vote.side-b { background: var(--side-b); }
.case-feed-vote-area > p { margin: 0; color: #ffffffbd; font-size: .69rem; font-weight: 700; text-align: center; text-transform: uppercase; }
.case-feed-verdict { grid-column: 1 / -1; display: grid; grid-template-columns: auto 1fr auto; gap: .55rem; align-items: center; }
.case-feed-verdict > div:first-child, .case-feed-verdict > div:last-child { display: grid; gap: .1rem; font-size: .68rem; }
.case-feed-verdict > div:last-child { text-align: right; }
.case-feed-verdict strong { font-size: 1.05rem; }
.case-feed-meter { height: .55rem; overflow: hidden; border-radius: 999px; background: var(--side-b); }
.case-feed-meter span { display: block; height: 100%; background: var(--side-a); }
.case-feed-result-note { grid-column: 1 / -1; margin: -.25rem 0 0; color: #ffffffc7; font-size: .7rem; text-align: center; }
.case-feed-notice { display: grid; place-items: center; height: 100%; margin: 0; padding: 2rem; color: #fff; text-align: center; }
.case-feed-notice.error { background: #5f2321; }

@media (min-width: 900px) {
  .case-feed {
    height: calc(100dvh - 7.75rem);
    margin: -1.5rem 0;
    border: 1px solid var(--line);
    border-radius: 8px;
  }

  .case-feed-content,
  .case-feed-header {
    padding-inline: 2rem;
  }
}
</style>


