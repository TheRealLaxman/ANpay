// WebAuthn JavaScript Interop
window.webauthn = {
    isAvailable: function () {
        return window.PublicKeyCredential !== undefined;
    },

    register: async function (options) {
        try {
            // Convert challenge and user.id from base64 to ArrayBuffer
            const challenge = base64ToArrayBuffer(options.challenge);
            const userId = stringToArrayBuffer(options.user.id);

            const createOptions = {
                publicKey: {
                    ...options,
                    challenge: challenge,
                    user: {
                        ...options.user,
                        id: userId
                    }
                }
            };

            const credential = await navigator.credentials.create(createOptions);

            return {
                id: credential.id,
                rawId: arrayBufferToBase64(credential.rawId),
                type: credential.type,
                response: {
                    attestationObject: arrayBufferToBase64(credential.response.attestationObject),
                    clientDataJSON: arrayBufferToBase64(credential.response.clientDataJSON)
                }
            };
        } catch (error) {
            throw new Error(error.message || 'WebAuthn registration failed');
        }
    },

    authenticate: async function (options) {
        try {
            const challenge = base64ToArrayBuffer(options.challenge);
            const allowCredentials = options.allowCredentials.map(cred => ({
                ...cred,
                id: base64ToArrayBuffer(cred.id)
            }));

            const getOptions = {
                publicKey: {
                    ...options,
                    challenge: challenge,
                    allowCredentials: allowCredentials
                }
            };

            const assertion = await navigator.credentials.get(getOptions);

            return {
                id: assertion.id,
                rawId: arrayBufferToBase64(assertion.rawId),
                type: assertion.type,
                response: {
                    authenticatorData: arrayBufferToBase64(assertion.response.authenticatorData),
                    clientDataJSON: arrayBufferToBase64(assertion.response.clientDataJSON),
                    signature: arrayBufferToBase64(assertion.response.signature),
                    userHandle: assertion.response.userHandle
                        ? arrayBufferToBase64(assertion.response.userHandle)
                        : null
                }
            };
        } catch (error) {
            throw new Error(error.message || 'WebAuthn authentication failed');
        }
    },

    getDeviceName: function () {
        const ua = navigator.userAgent;
        if (ua.includes('Windows')) return 'Windows PC';
        if (ua.includes('Mac')) return 'Mac';
        if (ua.includes('Linux')) return 'Linux PC';
        if (ua.includes('Android')) return 'Android Device';
        if (ua.includes('iPhone') || ua.includes('iPad')) return 'iOS Device';
        return 'Unknown Device';
    }
};

// Base64 helpers
function base64ToArrayBuffer(base64) {
    const binary = atob(base64.replace(/-/g, '+').replace(/_/g, '/'));
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }
    return bytes.buffer;
}

function arrayBufferToBase64(buffer) {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.byteLength; i++) {
        binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function stringToArrayBuffer(str) {
    const encoder = new TextEncoder();
    return encoder.encode(str).buffer;
}
