// Opdracht 4: inloggen via OIDC, authorization code flow met PKCE.
// oidc-client-ts staat lokaal in lib/ en zet zichzelf op window.oidc.

const { UserManager, WebStorageStateStore } = window.oidc;

export const userManager = new UserManager({
    authority: window.location.origin + '/auth/realms/unbreakable',
    client_id: 'unbreakable-spa',
    redirect_uri: window.location.origin + '/callback.html',
    post_logout_redirect_uri: window.location.origin + '/',
    response_type: 'code',          // authorization code flow
    scope: 'openid profile email',  // PKCE (S256) staat standaard aan
    automaticSilentRenew: true,
    // Tokens in het geheugen van dit tabblad, niet in localStorage.
    // Beperkt de schade bij XSS (dreiging 13 in het threat model).
    userStore: new WebStorageStateStore({ store: window.sessionStorage }),
    stateStore: new WebStorageStateStore({ store: window.sessionStorage }),
});

let cached = await userManager.getUser();
if (cached && cached.expired) {
    cached = null;
}

export function currentUser() {
    return cached ? (cached.profile.preferred_username || cached.profile.sub) : null;
}

export function login() {
    return userManager.signinRedirect();
}

export function logout() {
    return userManager.signoutRedirect();
}

export async function accessToken() {
    const user = await userManager.getUser();
    if (!user || user.expired) {
        await userManager.signinRedirect();
        return null;
    }
    return user.access_token;
}
