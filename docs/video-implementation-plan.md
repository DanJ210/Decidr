
## Product assessment

The concept fits Decidr well. The existing lifecycle already maps closely:

1. Prosecutor creates Side A.
2. A friend is invited.
3. Friend completes Side B.
4. Case becomes public.
5. Community votes.

The major change is replacing text arguments with short-form video and transforming the home page into an immersive, gesture-driven feed.

## Proposed MVP experience

### Case creation

1. Enter a short title, category, and optional context.
2. Record or upload a prosecutor video, maximum 30 seconds.
3. Preview, retake, and confirm the video.
4. Select an accepted friend.
5. Submit the case in `Pending` state.

### Defense response

1. Defender receives the existing case invitation.
2. They watch the prosecutor video.
3. They accept or decline.
4. On acceptance, they record a defense video under the same 30-second limit.
5. After confirmation, the case moves to `Open`.

### Public feed

- One full-screen case per vertical viewport.
- Swipe up/down to move between cases.
- Show prosecutor and defense videos sequentially, with clear side labels.
- Swipe right for the prosecutor and left for the defense.
- Provide visible vote buttons as an accessible alternative.
- Prevent participants and repeat voters from voting, preserving current rules.
- Include explicit playback, mute, captions, reporting, and case-detail controls.
- Do not reveal live totals until after voting to reduce social bias.

## Domain changes

Extend each argument with video metadata:

- Media identifier and playback URL
- Poster/thumbnail URL
- Duration
- Processing status
- Caption/transcript status
- MIME type and dimensions
- Creation timestamp

Add media lifecycle states such as uploading, processing, ready, rejected, and failed. A case must not enter the public feed until both videos are ready.

Keep `Pending`, `Open`, and `Closed`; they already represent the desired product lifecycle.

## Backend and storage direction

- Store video metadata in SQL, not video binaries.
- Use private object storage with controlled upload and playback access.
- Upload directly from the browser using short-lived authorization rather than routing large files through ASP.NET.
- Process uploads asynchronously for validation, transcoding, thumbnails, and captions.
- Serve playback through a CDN.
- Enforce duration, size, type, and ownership on the server.
- Add cleanup for abandoned, replaced, rejected, and declined-case media.

For Azure deployment, Blob Storage, CDN/Front Door, and an asynchronous media-processing worker are the natural fit.

## Frontend changes

- Add a reusable video recorder with permission handling, timer, preview, retake, upload progress, and fallback file selection.
- Replace the Side A claim field in the creation flow with the recorder.
- Replace the Side B response field with the same recorder.
- Introduce a dedicated full-screen feed rather than adapting the existing card list too heavily.
- Preserve the detailed case route for comments, evidence, moderation, results, and sharing.
- Use pointer/touch gestures without overriding normal scrolling or accessibility controls.
- Load only the current, previous, and next case media to control bandwidth.
- Pause videos when hidden and respect reduced-motion and data-saving preferences.

## API evolution

Add capabilities for:

- Starting an authorized media upload
- Finalizing and validating an upload
- Polling or receiving processing status
- Replacing an unsubmitted recording
- Completing Side A creation with ready media
- Completing Side B acceptance with ready media
- Fetching a paginated, cursor-based public feed
- Reporting inappropriate video
- Recording playback and completion analytics

Existing voting, invitation, friendship, rewards, and authentication APIs can remain conceptually intact.

## Safety and platform requirements

Video makes moderation a launch requirement:

- User reporting and blocking
- Content takedown and moderator review
- Rate limits and upload quotas
- Malware/file validation
- Explicit retention and deletion rules
- Consent and privacy messaging before recording
- Caption support
- Policy for minors, harassment, nudity, threats, impersonation, and copyrighted content
- No public exposure before processing and moderation checks complete

## Delivery phases

1. **Product prototype**
   - Validate recording, two-video playback, gesture voting, and the invitation journey with mocked media.

2. **Media foundation**
   - Introduce storage, upload authorization, processing states, validation, cleanup, and database migration.

3. **Two-sided video lifecycle**
   - Integrate prosecutor recording, friend invitation, defense recording, and publication gating.

4. **Public video feed**
   - Add cursor pagination, vertical navigation, sequential playback, swipe voting, accessible controls, and preload limits.

5. **Trust and reliability**
   - Add reporting, moderation, captions, observability, quotas, retry behavior, and media deletion.

6. **Limited beta**
   - Measure recording completion, invitation acceptance, defense completion, watch-through, voting, reports, latency, and storage cost before broad release.

## Key decisions before implementation

- Whether videos play sequentially or appear in a split/toggle presentation
- Whether creators may upload prerecorded media or must record in-app
- Whether the 30-second limit applies before or after transcoding
- Whether captions are mandatory before publication
- Whether live vote totals remain hidden until voting
- Whether declined and abandoned recordings are deleted immediately
- Initial moderation model: pre-publication review, automated checks, or report-driven review
- Geographic and age constraints for the first release

## Recommendation

Build a narrow prototype first: mobile recording, two sequential 30-second videos, vertical case navigation, and explicit plus swipe-based voting. Defer comments, evidence overlays, filters, and advanced creator tools until the core loop proves engaging.
