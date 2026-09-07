<script setup lang="ts">
import { computed, ref } from 'vue'
import { Camera, CircleStop, RotateCcw, Upload, Video } from '@lucide/vue'
import { useVideoRecorder, type RecordedClip } from '../composables/useVideoRecorder'

const props = withDefaults(defineProps<{
  modelValue: RecordedClip | null
  maxSeconds?: number
  sideLabel?: string
}>(), {
  maxSeconds: 30,
  sideLabel: 'your argument',
})

const emit = defineEmits<{ 'update:modelValue': [RecordedClip | null] }>()

const fileInput = ref<HTMLInputElement | null>(null)

const {
  phase,
  error,
  elapsedSeconds,
  remainingSeconds,
  setPreviewEl,
  clipUrl,
  isSupported,
  startPreview,
  startRecording,
  stopRecording,
  selectFile,
  retake,
  cancelPreview,
} = useVideoRecorder(props.maxSeconds, (clip) => emit('update:modelValue', clip))

const progressPercent = computed(() =>
  Math.min(100, Math.round((elapsedSeconds.value / props.maxSeconds) * 100)))

function onFileChange(event: Event) {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  if (file) {
    void selectFile(file)
  }
  target.value = ''
}
</script>

<template>
  <div class="recorder">
    <div class="recorder-stage" :class="{ live: phase === 'recording' }">
      <video
        v-show="phase === 'preview' || phase === 'recording'"
        :ref="(el) => setPreviewEl(el as HTMLVideoElement | null)"
        class="recorder-video"
        muted
        playsinline
        autoplay
      />

      <video
        v-if="phase === 'captured' && clipUrl"
        class="recorder-video"
        :src="clipUrl"
        controls
        playsinline
      />

      <div v-if="phase === 'idle'" class="recorder-placeholder">
        <Video :size="28" aria-hidden="true" />
        <p>Record {{ sideLabel }} in {{ maxSeconds }} seconds or less.</p>
      </div>

      <span v-if="phase === 'recording'" class="recorder-timer" role="status">
        <i aria-hidden="true"></i>{{ remainingSeconds }}s left
      </span>

      <div v-if="phase === 'recording'" class="recorder-progress" aria-hidden="true">
        <span :style="{ width: `${progressPercent}%` }"></span>
      </div>
    </div>

    <p v-if="error" class="recorder-error">{{ error }}</p>

    <p v-if="phase === 'captured'" class="recorder-meta">
      {{ modelValue?.durationSeconds ?? elapsedSeconds }}s clip ready.
    </p>

    <div class="recorder-actions">
      <button
        v-if="phase === 'idle'"
        type="button"
        class="action-btn"
        :disabled="!isSupported"
        @click="startPreview"
      >
        <Camera :size="16" /> Start camera
      </button>

      <template v-if="phase === 'preview'">
        <button type="button" class="action-btn" @click="startRecording">
          <Video :size="16" /> Record
        </button>
        <button type="button" class="text-button" @click="cancelPreview">Cancel</button>
      </template>

      <button v-if="phase === 'recording'" type="button" class="action-btn danger" @click="stopRecording">
        <CircleStop :size="16" /> Stop
      </button>

      <button v-if="phase === 'captured'" type="button" class="text-button" @click="retake">
        <RotateCcw :size="14" /> Retake
      </button>

      <button
        v-if="phase !== 'recording'"
        type="button"
        class="text-button"
        @click="fileInput?.click()"
      >
        <Upload :size="14" /> Upload a file
      </button>

      <input
        ref="fileInput"
        class="visually-hidden"
        type="file"
        accept="video/*"
        @change="onFileChange"
      />
    </div>
  </div>
</template>

<style scoped>
.recorder {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.recorder-stage {
  position: relative;
  aspect-ratio: 9 / 16;
  max-height: 420px;
  border-radius: 14px;
  overflow: hidden;
  background: #0d1016;
  border: 1px solid rgba(255, 255, 255, 0.12);
  display: flex;
  align-items: center;
  justify-content: center;
}

.recorder-stage.live {
  border-color: #e5484d;
}

.recorder-video {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.recorder-placeholder {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
  padding: 1.25rem;
  text-align: center;
  color: rgba(255, 255, 255, 0.72);
}

.recorder-timer {
  position: absolute;
  top: 0.75rem;
  left: 0.75rem;
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.25rem 0.6rem;
  border-radius: 999px;
  background: rgba(0, 0, 0, 0.6);
  color: #fff;
  font-size: 0.8rem;
}

.recorder-timer i {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #e5484d;
}

.recorder-progress {
  position: absolute;
  inset-inline: 0;
  bottom: 0;
  height: 3px;
  background: rgba(255, 255, 255, 0.2);
}

.recorder-progress span {
  display: block;
  height: 100%;
  background: #e5484d;
  transition: width 1s linear;
}

.recorder-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
}

.recorder-error {
  margin: 0;
  color: #e5484d;
  font-size: 0.85rem;
}

.recorder-meta {
  margin: 0;
  font-size: 0.85rem;
  opacity: 0.75;
}

.visually-hidden {
  position: absolute;
  width: 1px;
  height: 1px;
  clip: rect(0 0 0 0);
  clip-path: inset(50%);
  overflow: hidden;
  white-space: nowrap;
}

@media (prefers-reduced-motion: reduce) {
  .recorder-progress span {
    transition: none;
  }
}
</style>
