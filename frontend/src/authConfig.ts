import {
  PublicClientApplication,
} from '@azure/msal-browser'
import type { AccountInfo, AuthenticationResult, SilentRequest } from '@azure/msal-browser'

const clientId = (import.meta.env.VITE_ENTRA_CLIENT_ID as string | undefined) ?? ''
const authority = (import.meta.env.VITE_ENTRA_AUTHORITY as string | undefined) ?? ''
const apiScope = (import.meta.env.VITE_ENTRA_API_SCOPE as string | undefined) ?? ''

export const entraConfigured = Boolean(clientId && authority && apiScope)

export const msalInstance = entraConfigured
  ? new PublicClientApplication({
      auth: {
        clientId,
        authority,
        redirectUri: window.location.origin,
        postLogoutRedirectUri: window.location.origin,
      },
      cache: {
        cacheLocation: 'sessionStorage',
      },
    })
  : null

let initialization: Promise<void> | null = null

export async function initializeMsal(): Promise<void> {
  if (!msalInstance) {
    return
  }

  initialization ??= msalInstance.initialize()
  await initialization
}

export async function signIn(): Promise<AuthenticationResult | null> {
  if (!msalInstance) {
    return null
  }

  await initializeMsal()
  const result = await msalInstance.loginPopup({
    scopes: [apiScope!],
  })
  msalInstance.setActiveAccount(result.account)
  return result
}

export async function signOut(): Promise<void> {
  if (!msalInstance) {
    return
  }

  await initializeMsal()
  await msalInstance.logoutPopup()
}

export async function getAccessToken(): Promise<string | null> {
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

  try {
    return (await msalInstance.acquireTokenSilent(request)).accessToken
  } catch {
    try {
      return (await msalInstance.acquireTokenPopup({ scopes: [apiScope!] })).accessToken
    } catch {
      return null
    }
  }
}

export function getActiveAccount(): AccountInfo | null {
  if (!msalInstance) {
    return null
  }

  return msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0] ?? null
}
