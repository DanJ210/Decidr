import { computed, onBeforeUnmount, ref, shallowRef } from 'vue'

export interface RecordedClip {
  url: string
  blob: Blob
  durationSeconds: number
  source: 'recorded' | 'uploaded'
}

export type RecorderPhase = 'idle' | 'preview' | 'recording' | 'captured'

const MAX_UPLOAD_BYTES = 64 * 1024 * 1024

// Ordered by preference; browsers ignore the ones they cannot produce.
const PREFERRED_MIME_TYPES = [
  'video/mp4;codecs=avc1',
  'video/webm;codecs=vp9,opus',
  'video/webm;codecs=vp8,opus',
  'video/webm',
]

function pickMimeType() {
  if (typeof MediaRecorder === 'undefined') return undefined
  return PREFERRED_MIME_TYPES.find((type) => MediaRecorder.isTypeSupported(type))
}

function readFileDuration(url: string) {
  return new Promise<number>((resolve) => {
    const probe = document.createElement('video')
    probe.preload = 'metadata'
    probe.onloadedmetadata = () => {
      const duration = Number.isFinite(probe.duration) ? Math.round(probe.duration) : Number.NaN
      probe.src = ''
      resolve(duration)
    }
    probe.onerror = () => {
      probe.src = ''
      resolve(Number.NaN)
    }
    probe.src = url
  })
}

export function useVideoRecorder(maxSeconds: number, onCaptured: (clip: RecordedClip | null) => void) {
  const phase = ref<RecorderPhase>('idle')
  const error = ref<string | null>(null)
  const elapsedSeconds = ref(0)
  const previewEl = ref<HTMLVideoElement | null>(null)
  const clipUrl = ref<string | null>(null)

  const stream = shallowRef<MediaStream | null>(null)
  const recorder = shallowRef<MediaRecorder | null>(null)
  let chunks: Blob[] = []
  let ticker: number | undefined

  function setPreviewEl(el: HTMLVideoElement | null) {
    previewEl.value = el
    if (el && stream.value) {
      el.srcObject = stream.value
    }
  }

  const isSupported = computed(() =>
    typeof navigator !== 'undefined'
    && !!navigator.mediaDevices?.getUserMedia
    && typeof MediaRecorder !== 'undefined')

  const remainingSeconds = computed(() => Math.max(0, maxSeconds - elapsedSeconds.value))

  function stopTicker() {
    if (ticker !== undefined) {
      window.clearInterval(ticker)
      ticker = undefined
    }
  }

  function releaseStream() {
    stream.value?.getTracks().forEach((track) => track.stop())
    stream.value = null
    if (previewEl.value) {
      previewEl.value.srcObject = null
    }
  }

  function releaseClip() {
    if (clipUrl.value) {
      URL.revokeObjectURL(clipUrl.value)
      clipUrl.value = null
    }
  }

  async function startPreview() {
    error.value = null

    if (!isSupported.value) {
      error.value = 'Recording is not supported in this browser. Upload a video file instead.'
      return
    }

    try {
      const media = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'user' },
        audio: true,
      })
      stream.value = media
      phase.value = 'preview'

      if (previewEl.value) {
        previewEl.value.srcObject = media
        await previewEl.value.play().catch(() => undefined)
      }
    } catch {
      error.value = 'Camera and microphone access was blocked. Allow access or upload a file instead.'
      phase.value = 'idle'
    }
  }

  function startRecording() {
    const media = stream.value
    if (!media || phase.value !== 'preview') return

    releaseClip()
    onCaptured(null)
    chunks = []
    elapsedSeconds.value = 0
    error.value = null

    const mimeType = pickMimeType()
    const instance = new MediaRecorder(media, mimeType ? { mimeType } : undefined)

    instance.ondataavailable = (event) => {
      if (event.data.size > 0) {
        chunks.push(event.data)
      }
    }

    instance.onstop = () => {
      stopTicker()
      const blob = new Blob(chunks, { type: instance.mimeType || 'video/webm' })
      chunks = []

      if (blob.size === 0) {
        error.value = 'That recording came back empty. Try again.'
        phase.value = 'preview'
        return
      }

      clipUrl.value = URL.createObjectURL(blob)
      phase.value = 'captured'
      releaseStream()
      onCaptured({
        url: clipUrl.value,
        blob,
        durationSeconds: Math.max(1, elapsedSeconds.value),
        source: 'recorded',
      })
    }

    recorder.value = instance
    instance.start()
    phase.value = 'recording'

    ticker = window.setInterval(() => {
      elapsedSeconds.value += 1
      if (elapsedSeconds.value >= maxSeconds) {
        stopRecording()
      }
    }, 1000)
  }

  function stopRecording() {
    stopTicker()
    if (recorder.value?.state === 'recording') {
      recorder.value.stop()
    }
    recorder.value = null
  }

  async function selectFile(file: File) {
    error.value = null

    if (!file.type.startsWith('video/')) {
      error.value = 'Choose a video file.'
      return
    }

    if (file.size > MAX_UPLOAD_BYTES) {
      error.value = 'That file is too large. Keep it under 64 MB.'
      return
    }

    releaseClip()
    releaseStream()

    const url = URL.createObjectURL(file)
    const duration = await readFileDuration(url)

    if (duration > maxSeconds) {
      URL.revokeObjectURL(url)
      error.value = `Keep it under ${maxSeconds} seconds. That clip is ${duration} seconds long.`
      return
    }

    clipUrl.value = url
    elapsedSeconds.value = duration
    phase.value = 'captured'
    onCaptured({ url, blob: file, durationSeconds: Math.max(1, duration), source: 'uploaded' })
  }

  async function retake() {
    releaseClip()
    elapsedSeconds.value = 0
    phase.value = 'idle'
    onCaptured(null)
    await startPreview()
  }

  function cancelPreview() {
    stopTicker()
    releaseStream()
    phase.value = 'idle'
  }

  onBeforeUnmount(() => {
    stopTicker()
    if (recorder.value?.state === 'recording') {
      recorder.value.stop()
    }
    recorder.value = null
    releaseStream()
    releaseClip()
  })

  return {
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
  }
}
