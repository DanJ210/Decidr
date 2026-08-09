import {
  InteractionRequiredAuthError,
  PublicClientApplication,
} from '@azure/msal-browser'
import type { AccountInfo, SilentRequest } from '@azure/msal-browser'

const clientId = (import.meta.env.VITE_ENTRA_CLIENT_ID as string | undefined) ?? ''
const authority = (import.meta.env.VITE_ENTRA_AUTHORITY as string | undefined) ?? ''
const apiScope = (import.meta.env.VITE_ENTRA_API_SCOPE as string | undefined) ?? ''
const authorityHost = (() => {
  if (!authority) return ''
  try {
    return new URL(authority).hostname
  } catch {
    return ''
  }
})()
const authCallbackPath = '/auth/callback'
const authenticationReturnPathKey = 'decidr-auth-return-path'

export const entraConfigured = Boolean(clientId && authority && authorityHost && apiScope)
export const msalInstance = entraConfigured
  ? new PublicClientApplication({
      auth: {
        clientId,
        authority,
        knownAuthorities: [authorityHost],
        redirectUri: `${window.location.origin}${authCallbackPath}`,
        postLogoutRedirectUri: window.location.origin,
      },
      cache: {
        cacheLocation: 'sessionStorage',
      },
    })
  : null

let initialization: Promise<void> | null = null

function getAuthenticationError(error: unknown): string {
  if (error instanceof Error && error.message) {
    return error.message
  }

  return 'Microsoft sign-in did not complete.'
}

function rememberAuthenticationReturnPath(): void {
  if (window.location.pathname === authCallbackPath || sessionStorage.getItem(authenticationReturnPathKey)) {
    return
  }

  sessionStorage.setItem(
    authenticationReturnPathKey,
    `${window.location.pathname}${window.location.search}${window.location.hash}`,
  )
}

export function takeAuthenticationReturnPath(): string {
  const returnPath = sessionStorage.getItem(authenticationReturnPathKey)
  sessionStorage.removeItem(authenticationReturnPathKey)
  return returnPath?.startsWith('/') && !returnPath.startsWith('//') ? returnPath : '/'
}

export async function initializeMsal(): Promise<void> {
  if (!msalInstance) {
    return
  }

  initialization ??= msalInstance.initialize()
  await initialization
  try {
    const result = await msalInstance.handleRedirectPromise({ navigateToLoginRequestUrl: false })
    if (result?.account) {
      msalInstance.setActiveAccount(result.account)
    }
  } catch (error) {
    sessionStorage.setItem('decidr-auth-error', getAuthenticationError(error))
  }
}

export function takeAuthenticationError(): string | null {
  const error = sessionStorage.getItem('decidr-auth-error')
  sessionStorage.removeItem('decidr-auth-error')
  return error
}

export async function signIn(): Promise<void> {
  if (!msalInstance) {
    return
  }

  await initializeMsal()
  rememberAuthenticationReturnPath()
  await msalInstance.loginRedirect({
    scopes: [apiScope!],
  })
}

export async function signOut(): Promise<void> {
  if (!msalInstance) {
    return
  }

  await initializeMsal()
  await msalInstance.logoutRedirect()
}

async function acquireAccessTokenSilently(): Promise<string | null> {
  if (!msalInstance) {
    return null
  }

  await initializeMsal()
  const account = msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0]
  if (!account) {
    return null
  }

  msalInstance.setActiveAccount(account)
  const request: SilentRequest = {
    account,
    scopes: [apiScope!],
  }

  return (await msalInstance.acquireTokenSilent(request)).accessToken
}

export async function getAccessToken(): Promise<string | null> {
  try {
    return await acquireAccessTokenSilently()
  } catch (error) {
    if (error instanceof InteractionRequiredAuthError) {
      return null
    }

    throw error
  }
}

export async function ensureAccessToken(): Promise<boolean> {
  try {
    return Boolean(await acquireAccessTokenSilently())
  } catch (error) {
    if (!(error instanceof InteractionRequiredAuthError) || !msalInstance) {
      throw error
    }

    rememberAuthenticationReturnPath()
    await msalInstance.acquireTokenRedirect({ scopes: [apiScope!] })
    return false
  }
}

export function getActiveAccount(): AccountInfo | null {
  if (!msalInstance) {
    return null
  }

  return msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0] ?? null
}
